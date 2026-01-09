# 文件位置: scripts/build_data.py
import os
import json
import re
import sys
import urllib.parse

sys.dont_write_bytecode = True

# --- 调试开关 ---
DEBUG_MODE = True

# --- 动态计算路径 ---
# 修正适配目录结构: scripts/build_data.py
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
# 上一级是项目根目录 (包含 index.html 和 posts 文件夹)
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)

# 输出文件路径 (指向根目录，与 index.html 同级)
OUTPUT_DIR = PROJECT_ROOT
OUTPUT_FILE = os.path.join(OUTPUT_DIR, 'posts_data.json')

def log(msg):
    if DEBUG_MODE:
        print(f"[DEBUG] {msg}")

def parse_front_matter(content):
    metadata = {}
    markdown_content = content
    match = re.match(r'^---\s*\n(.*?)\n---\s*\n', content, re.DOTALL)
    if match:
        yaml_text = match.group(1)
        markdown_content = content[match.end():]
        for line in yaml_text.split('\n'):
            if ':' in line:
                key, val = line.split(':', 1)
                key = key.strip()
                val = val.strip()
                if val.startswith('[') and val.endswith(']'):
                    val = [x.strip() for x in val[1:-1].split(',')]
                metadata[key] = val
    return metadata, markdown_content

def fix_image_paths(content, file_path):
    """
    究极通用的链接路径修复函数 (v5.0)
    统一处理 Markdown 图片/链接 和 HTML href/src
    核心能力:
    1. 路径自动回退: 当前目录找不到 -> 尝试项目根目录
    2. 符号增强支持: 支持文件名包含 () [] 等特殊符号
    3. 统一清洗: 解码 -> 修复 -> 编码
    """
    
    # --- 1. 统一路径计算核心 ---
    def resolve_url(url_raw):
        if not url_raw: return ""
        
        # A. 预处理：去空，分离 Title (针对 MD: url "title")
        url_main = url_raw.strip()
        title_suffix = ""
        # 简单检测 url 和 title 的分隔符 (空格+引号)
        # Markdown 标准: [link](url "title")
        match_title = re.match(r'(^.*?)(\s+["\'].*?["\']\s*)$', url_main)
        if match_title:
            url_main = match_title.group(1)
            title_suffix = match_title.group(2)

        # B. 分离锚点和查询参数
        anchor_suffix = ""
        if '#' in url_main:
            url_main, anchor = url_main.split('#', 1)
            anchor_suffix = '#' + anchor
        elif '?' in url_main:
            url_main, query = url_main.split('?', 1)
            anchor_suffix = '?' + query

        # C. 忽略协议头
        if url_main.lower().startswith(('http:', 'https:', '//', 'mailto:', 'javascript:', 'data:', 'vb:', 'tel:')):
            return url_raw # 原样返回 (含 title)

        # D. 解码 (Handle %20 etc, to find file on disk)
        try:
            url_decoded = urllib.parse.unquote(url_main)
        except:
            url_decoded = url_main

        # E. 绝对路径与存在性检查
        # e.1 基于当前 MD 文件的路径
        md_dir = os.path.dirname(file_path)
        abs_path_local = os.path.join(md_dir, url_decoded)
        abs_path_local = os.path.normpath(abs_path_local)

        # e.2 基于项目根目录的路径 (Fallback)
        abs_path_root = os.path.join(PROJECT_ROOT, url_decoded) # url_decoded 若以 / 开头，join 会丢弃 PROJECT_ROOT，需注意
        if url_decoded.startswith('/') or url_decoded.startswith('\\'):
             abs_path_root = os.path.join(PROJECT_ROOT, url_decoded.lstrip('/\\'))
        abs_path_root = os.path.normpath(abs_path_root)

        final_abs_path = abs_path_local
        
        # F. 判定逻辑
        # 如果本地找不到，且根目录能找到，则切换到根目录路径
        if not os.path.exists(abs_path_local) and os.path.exists(abs_path_root):
            final_abs_path = abs_path_root
        
        # G. 计算相对路径 (相对于 HTML 输出目录)
        try:
            rel_path = os.path.relpath(final_abs_path, OUTPUT_DIR)
            new_url = rel_path.replace('\\', '/')
        except:
            new_url = url_decoded # 跨盘符等异常，保持原样

        # H. 安全编码 (Space -> %20)
        # 仅对路径部分编码，保留 /
        # safe_url = urllib.parse.quote(new_url) # quote 会转义 / 导致链接失效
        safe_url = new_url.replace(' ', '%20')
        
        return f"{safe_url}{anchor_suffix}{title_suffix}"

    # --- 2. HTML 属性替换 (href/src) ---
    # 匹配: src="val" 或 href='val'
    # Group 1: 属性前缀 (e.g. src=")
    # Group 2: 值
    # Group 3: 属性后缀 (")
    html_pattern = r'(?i)(\b(?:src|href)\s*=\s*["\'])(.*?)(["\'])'
    
    def html_replacer(match):
        prefix = match.group(1)
        url = match.group(2)
        suffix = match.group(3)
        return f"{prefix}{resolve_url(url)}{suffix}"

    content = re.sub(html_pattern, html_replacer, content)

    # --- 3. Markdown 通用替换 (链接 & 图片) ---
    # 匹配: [Text](Url) 或 ![Text](Url)
    # 难点: 支持 Text 里的 [] 和 Url 里的 () 嵌套
    
    # 构造支持 2 层嵌套括号的正则片段
    # Level 0: 非括号字符
    # Level 1: ( Level 0 )
    # Level 2: ( Level 0 | Level 1 )*
    no_paren = r'[^()]*'
    level1_paren = r'\(' + no_paren + r'\)'
    level2_paren = r'(?:' + no_paren + r'|' + level1_paren + r')*'
    
    # 匹配 [ ... ] 部分，由 !? 开始
    # 支持文本中包含匹配的 []
    md_text_part = r'(!?\[(?:\[[^\]]*\]|[^\[\]])*\])'
    
    # 匹配 ( ... ) 部分
    md_url_part = r'\(' + f'({level2_paren})' + r'\)'
    
    md_pattern = md_text_part + md_url_part
    
    def md_replacer(match):
        prefix = match.group(1) # ![alt] 或 [text]
        url_body = match.group(2) # url part inside ()
        
        fixed_url = resolve_url(url_body)
        
        # 针对普通链接 [text]，对 text 内容进行 safe 处理 (避免 [ ] 破坏结构)
        # 图片 ![alt] 通常不需要严格转义，且转义可能破坏显示
        if not prefix.startswith('!'):
            # 剥离外层 []
            inner_text = prefix[1:-1]
            # 转义内部的 [ ]
            safe_text = inner_text.replace('[', '\\[').replace(']', '\\]')
            prefix = f'[{safe_text}]'
            
        return f"{prefix}({fixed_url})"

    content = re.sub(md_pattern, md_replacer, content)

    return content

def build_data():
    print(f"--- reference build_data (v4.3 增强版) ---")
    print(f"根目录: {PROJECT_ROOT}")
    
    ignore_dirs = {'.git', '.obsidian', '.idea', '.vscode', '__pycache__', 'node_modules', 'bin', 'obj'}
    files_full_paths = []
    
    # 修改：扫描整个 PROJECT_ROOT
    for root, dirs, files in os.walk(PROJECT_ROOT):
        # 排除忽略目录
        dirs[:] = [d for d in dirs if d not in ignore_dirs]
        
        for f in files:
            if f.endswith('.md'):
                files_full_paths.append(os.path.join(root, f))
    
    nodes = []
    links = []
    id_map = {}
    tag_to_files = {} # 1. 初始化标签映射

    print(f"扫描到 {len(files_full_paths)} 个文件，开始处理路径...")

    # 1. 解析节点
    for filepath in files_full_paths:
        try:
            filename = os.path.basename(filepath)
            with open(filepath, 'r', encoding='utf-8') as f:
                raw_text = f.read()
            
            meta, raw_content = parse_front_matter(raw_text)
            
            # --- 调用新的双重修复函数 ---
            content = fix_image_paths(raw_content, filepath)
            
            file_id = os.path.splitext(filename)[0]
            id_map[filename] = file_id
            id_map[file_id] = file_id 
            
            # 2. 升级正则：支持中文、横杠等
            # 形式支持: #标签 #我的-笔记
            inline_tags = re.findall(r'(?:^|\s)#([\w\u4e00-\u9fa5\-]+)', content)
            combined_tags = []
            seen = set()
            for t in inline_tags:
                if t and t not in seen:
                    combined_tags.append(t)
                    seen.add(t)
            
            # 3. 收集 Tag -> 多个文件ID 的关系
            for t in combined_tags:
                if t not in tag_to_files:
                    tag_to_files[t] = []
                # 注意：记录的是 file_id
                tag_to_files[t].append(file_id)

            primary_group = combined_tags[0] if combined_tags else 'default'

            nodes.append({
                "id": file_id,
                "label": meta.get('title', file_id),
                "group": primary_group,
                "all_tags": combined_tags,
                "content": content,
                "val": 1
            })
        except Exception as e:
            print(f"[Warning] 处理 {filename} 失败: {e}")

    # 2. 解析连接 (仅处理 MD 链接，忽略 Wiki 链接)
    link_pattern_md = re.compile(r'\[.*?\]\((.*?)\)')
    
    for node in nodes:
        for match in link_pattern_md.findall(node['content']):
            # 1. 解码 URL (解决中文路径 %E6%96... 无法匹配的问题)
            match = urllib.parse.unquote(match)
            # 2. 去除锚点和参数 (#section, ?query)
            match = match.split('#')[0].split('?')[0]
            
            if match.startswith('http') or match.startswith('//') or match.startswith('mailto:'):
                continue
                
            # 3. 统一路径分隔符并提取文件名
            target_filename = match.replace('\\', '/').split('/')[-1]
            target_id = os.path.splitext(target_filename)[0]
            
            if target_id in id_map and id_map[target_id] != node['id']:
                links.append({"source": node['id'], "target": id_map[target_id], "type": "md"})

    # 4. 根据 Tag 生成文件之间的"链式连接" (避免生成中心节点)
    for tag_name, fids in tag_to_files.items():
        count = len(fids)
        if count < 2:
            continue
            
        # 将该 Tag 下的所有文件连成一条链
        for i in range(count - 1):
            links.append({
                "source": fids[i],
                "target": fids[i+1],
                "type": "tag_group" # 标记为同组连接
            })

    unique_links = []
    seen = set()
    for l in links:
        key = f"{l['source']}->{l['target']}:{l['type']}"
        if key not in seen:
            seen.add(key)
            unique_links.append(l)

    data = {"nodes": nodes, "links": unique_links}
    
    # 3. 写入
    try:
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
        print(f"[Success] 构建完成，已处理 HTML 和 MD 图片路径。")
        return data  # 返回 data 供外部调用
    except Exception as e:
        print(f"[Error] 写入失败: {e}")
        return None

if __name__ == '__main__':
    build_data()