const fs = require('fs');
const path = require('path');

const POSTS_DIR = path.join(__dirname, '../posts');
const OUTPUT_FILE = path.join(__dirname, '../posts_data.json');

// --- 1. 递归获取所有文件 (复刻 Python 的 os.walk) ---
function getAllFiles(dirPath, arrayOfFiles) {
    const files = fs.readdirSync(dirPath);
    arrayOfFiles = arrayOfFiles || [];

    files.forEach(function(file) {
        const fullPath = path.join(dirPath, file);
        if (fs.statSync(fullPath).isDirectory()) {
            arrayOfFiles = getAllFiles(fullPath, arrayOfFiles);
        } else {
            if (file.endsWith('.md')) {
                arrayOfFiles.push(fullPath);
            }
        }
    });

    return arrayOfFiles;
}

// 解析 FrontMatter
function parseFrontMatter(content) {
    const match = content.match(/^---\r?\n([\s\S]*?)\r?\n---/);
    if (match) {
        const yaml = match[1];
        const metadata = {};
        yaml.split('\n').forEach(line => {
            const parts = line.split(':');
            if (parts.length >= 2) {
                const key = parts[0].trim();
                const value = parts.slice(1).join(':').trim();
                if (value.startsWith('[') && value.endsWith(']')) {
                    metadata[key] = value.slice(1, -1).split(',').map(s => s.trim());
                } else {
                    metadata[key] = value;
                }
            }
        });
        return { metadata, content: content.slice(match[0].length) };
    }
    return { metadata: {}, content: content };
}

function processPosts() {
    console.log(`[Build] Scanning posts in ${POSTS_DIR}...`);
    
    if (!fs.existsSync(POSTS_DIR)) {
        console.error('ERROR: Posts directory not found!');
        process.exit(1);
    }

    // --- 改动：使用递归函数获取所有路径 ---
    const filePaths = getAllFiles(POSTS_DIR);
    console.log(`[Build] Found ${filePaths.length} markdown files.`);

    const nodes = [];
    const links = [];
    const idMap = new Map(); 

    // 第一遍扫描：建立节点和 ID 索引
    filePaths.forEach(filePath => {
        const rawContent = fs.readFileSync(filePath, 'utf-8');
        const filename = path.basename(filePath); // 获取文件名 (如 a.md)
        const fileId = filename.replace('.md', ''); // 获取 ID (如 a)

        const { metadata, content } = parseFrontMatter(rawContent);
        
        // 建立索引
        idMap.set(filename, fileId);
        idMap.set(fileId, fileId);
        // 为了兼容大小写不敏感的引用，存一份小写的
        idMap.set(filename.toLowerCase(), fileId);
        idMap.set(fileId.toLowerCase(), fileId);

        // --- 改动：复刻 Python 的 #Tag 提取逻辑 ---
        // 匹配规则：空格或行首开头，#号后跟文字/数字/下划线
        const tagRegex = /(?:^|\s)#(\w+)/g;
        const inlineTags = [];
        let match;
        // JS正则有点不一样，必须循环匹配
        while ((match = tagRegex.exec(content)) !== null) {
            // match[1] 是捕获组内容（即标签名）
            if (!inlineTags.includes(match[1])) {
                inlineTags.push(match[1]);
            }
        }

        // 确定主分组：优先用正文里的第一个 #Tag，没有则用 default
        const primaryGroup = inlineTags.length > 0 ? inlineTags[0] : 'default';

        nodes.push({
            id: fileId,
            label: metadata.title || fileId,
            group: primaryGroup,
            content: rawContent,
            val: 1
        });
    });

    // 第二遍扫描：建立连接
    nodes.forEach(node => {
        const content = node.content;
        
        // 1. Wiki Links [[Target]]
        // 这里的正则要小心，JS不支持 Python 的 (?i) 标志，要手动转小写匹配
        const wikiLinks = content.match(/\[\[(.*?)\]\]/g);
        if (wikiLinks) {
            wikiLinks.forEach(link => {
                // 提取 [[ target | alias ]] 中的 target
                const rawInside = link.slice(2, -2);
                const targetRaw = rawInside.split('|')[0].trim();
                
                // 尝试查找 ID (转小写去匹配)
                const targetLower = targetRaw.toLowerCase();
                let targetId = null;

                if (idMap.has(targetLower)) {
                    targetId = idMap.get(targetLower);
                } else if (idMap.has(targetLower + '.md')) {
                    targetId = idMap.get(targetLower + '.md');
                }

                if (targetId && targetId !== node.id) {
                    links.push({
                        source: node.id,
                        target: targetId
                    });
                }
            });
        }

        // 2. Markdown Links [Text](./Target.md)
        const mdLinks = content.match(/\[.*?\]\((.*?)\)/g);
        if (mdLinks) {
            mdLinks.forEach(link => {
                const match = /\[.*?\]\((.*?)\)/.exec(link);
                if (match && match[1]) {
                    const targetPath = match[1];
                    if (targetPath.startsWith('http')) return; // 忽略外链

                    // 提取文件名
                    const targetFile = path.basename(targetPath);
                    // 尝试匹配
                    const targetFileLower = targetFile.toLowerCase();
                    
                    if (idMap.has(targetFileLower)) {
                        links.push({
                            source: node.id,
                            target: idMap.get(targetFileLower)
                        });
                    }
                }
            });
        }
    });

    // 去重 (防止重复连线)
    const uniqueLinks = [];
    const linkSet = new Set();
    links.forEach(l => {
        const key = `${l.source}->${l.target}`;
        if (!linkSet.has(key)) {
            linkSet.add(key);
            uniqueLinks.push(l);
        }
    });

    const data = { nodes, links: uniqueLinks };
    fs.writeFileSync(OUTPUT_FILE, JSON.stringify(data, null, 2));
    console.log(`[Build] Generated ${nodes.length} nodes and ${uniqueLinks.length} links.`);
}

processPosts();