const fs = require('fs');
const path = require('path');

const POSTS_DIR = path.join(__dirname, '../posts');
const OUTPUT_FILE = path.join(__dirname, '../posts_data.json');

// 简单解析 FrontMatter
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
                // 简单的数组解析 [a, b]
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
    if (!fs.existsSync(POSTS_DIR)) {
        console.error('Posts directory not found!');
        return;
    }

    const files = fs.readdirSync(POSTS_DIR).filter(file => file.endsWith('.md'));
    const nodes = [];
    const links = [];
    const idMap = new Map(); // filename -> id

    // 第一遍扫描：建立 ID 映射和节点
    files.forEach(file => {
        const filePath = path.join(POSTS_DIR, file);
        const rawContent = fs.readFileSync(filePath, 'utf-8');
        const { metadata, content } = parseFrontMatter(rawContent);
        
        const id = file.replace('.md', '');
        idMap.set(file, id);
        idMap.set(id, id); // 支持直接引用 id

        nodes.push({
            id: id,
            label: metadata.title || id,
            group: (metadata.tags && metadata.tags[0]) || 'default',
            content: rawContent, // 包含原始内容供前端渲染
            val: 1 // 权重/大小
        });
    });

    // 第二遍扫描：建立连接
    nodes.forEach(node => {
        const content = node.content;
        
        // 匹配 [[WikiLink]]
        const wikiLinks = content.match(/\[\[(.*?)\]\]/g);
        if (wikiLinks) {
            wikiLinks.forEach(link => {
                const targetRaw = link.slice(2, -2).split('|')[0].trim(); // 去掉 [[ ]] 和可能的别名
                // 尝试匹配目标
                // 目标可能是 "hello-world" 或者 "hello-world.md"
                let targetId = null;
                if (idMap.has(targetRaw)) {
                    targetId = idMap.get(targetRaw);
                } else {
                    // 尝试不区分大小写查找
                     for (const [key, val] of idMap.entries()) {
                         if (key.toLowerCase() === targetRaw.toLowerCase() || 
                             key.replace('.md', '').toLowerCase() === targetRaw.toLowerCase()) {
                             targetId = val;
                             break;
                         }
                     }
                }

                if (targetId && targetId !== node.id) {
                    links.push({
                        source: node.id,
                        target: targetId
                    });
                }
            });
        }

        // 匹配 [Link](target.md)
        const mdLinks = content.match(/\[.*?\]\((.*?)\)/g);
        if (mdLinks) {
            mdLinks.forEach(link => {
                const match = link.match(/\[.*?\]\((.*?)\)/);
                if (match && match[1]) {
                    const targetPath = match[1];
                    // 假设相对路径引用
                    const targetFile = path.basename(targetPath);
                    if (idMap.has(targetFile)) {
                         links.push({
                            source: node.id,
                            target: idMap.get(targetFile)
                        });
                    }
                }
            });
        }
    });

    const data = { nodes, links };
    fs.writeFileSync(OUTPUT_FILE, JSON.stringify(data, null, 2));
    console.log(`Generated ${nodes.length} nodes and ${links.length} links.`);
}

processPosts();
