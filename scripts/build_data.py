# 文件位置: scripts/build_data.py
import os
import json
import re
import sys

# --- 调试开关 ---
DEBUG_MODE = True

# --- 动态计算路径 ---
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)
POSTS_DIR = os.path.join(PROJECT_ROOT, 'posts')
OUTPUT_FILE = os.path.join(PROJECT_ROOT, 'posts_data.json')
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
    同时修复 Markdown 图片 ![]() 和 HTML 图片 <img src=""> 的路径
    """
    # 计算当前文件相对于项目根目录的路径 (例如: posts/Tech)
    current_dir_rel = os.path.relpath(os.path.dirname(file_path), PROJECT_ROOT)

    # --- 核心路径处理逻辑 ---
    def process_url(url):
        # 如果是网络图片或绝对路径，不处理
        if url.startswith(('http://', 'https://', '//', 'data:', '/')):
            return url
        
        # 拼接路径：当前目录 + 图片相对路径
        new_path = os.path.join(current_dir_rel, url)
        # 标准化并转为 Web 斜杠
        new_path = os.path.normpath(new_path).replace('\\', '/')
        return new_path

    # 1. 修复 Markdown 语法: ![alt](url)
    def replace_md(match):
        alt = match.group(1)
        url = match.group(2)
        new_url = process_url(url)
        if new_url != url:
            log(f"Markdown图修复: '{url}' -> '{new_url}'")
        return f'![{alt}]({new_url})'
    
    # 正则: ! [ ... ] ( ... )
    content = re.sub(r'!\[(.*?)\]\((.*?)(?:\s+".*?")?\)', replace_md, content)

    # 2. 修复 HTML 语法: <img ... src="url" ...>
    def replace_html(match):
        prefix = match.group(1) # 捕获 <img ... src="
        url = match.group(2)    # 捕获 url
        suffix = match.group(3) # 捕获 结束引号 "
        
        new_url = process_url(url)
        if new_url != url:
            log(f"HTML图修复: '{url}' -> '{new_url}'")
        
        return f'{prefix}{new_url}{suffix}'

    # 正则说明:
    # (<img\s+[^>]*?src=["\'])  -> 匹配 <img 开头，中间任意字符，直到 src=" 或 src='，并捕获为组1
    # (.*?)                     -> 捕获 URL 内容，组2
    # (["\'])                   -> 捕获闭合的引号，组3
    html_pattern = r'(<img\s+[^>]*?src=["\'])(.*?)(["\'])'
    content = re.sub(html_pattern, replace_html, content, flags=re.IGNORECASE)

    return content

def build_data():
    print(f"--- 开始构建 (v4.3 增强版) ---")
    print(f"根目录: {PROJECT_ROOT}")
    
    if not os.path.exists(POSTS_DIR):
        print(f"[Error] 找不到 posts 目录")
        return False
        
    files_full_paths = []
    for root, dirs, files in os.walk(POSTS_DIR):
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
            if match.startswith('http') or match.startswith('//'):
                continue
            target_filename = match.split('/')[-1]
            target_id = os.path.splitext(target_filename)[0]
            if target_id in id_map and id_map[target_id] != node['id']:
                links.append({"source": node['id'], "target": id_map[target_id], "type": "md"})

    # 4. 根据 Tag 生成文件之间的"链式连接" (避免生成中心节点)
    # 效果: A-B-C-D 连在一起
    for tag_name, fids in tag_to_files.items():
        count = len(fids)
        if count < 2:
            continue
            
        # 将该 Tag 下的所有文件连成一条链
        # 这样它们会聚在一起，但没有中心节点
        for i in range(count - 1):
            links.append({
                "source": fids[i],
                "target": fids[i+1],
                "type": "tag_group" # 标记为同组连接
            })
            
        # 可选: 如果希望闭合成环 (A-B-C-A)，解开下面注释
        # if count > 2:
        #    links.append({"source": fids[-1], "target": fids[0], "type": "tag_group"})

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
        return True
    except Exception as e:
        print(f"[Error] 写入失败: {e}")
        return False

if __name__ == '__main__':
    build_data()