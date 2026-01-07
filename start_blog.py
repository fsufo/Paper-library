import os
import json
import re
import http.server
import socketserver
import webbrowser
import sys
import socket
import subprocess
import time

# --- 配置 ---
VERSION = "4.1 (Link Types)"
POSTS_DIR = os.path.join(os.getcwd(), 'posts')
OUTPUT_FILE = os.path.join(os.getcwd(), 'posts_data.json')
SERVER_PORT = 8080
ENABLE_WIKI_LINKS = True # 是否开启双链 [[...]]

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

def build_data():
    print(f"[Build] Scanning posts in {POSTS_DIR}...")
    if not os.path.exists(POSTS_DIR):
        os.makedirs(POSTS_DIR)
        
    # 修改为递归扫描所有子文件夹中的 .md 文件
    files_full_paths = []
    for root, dirs, files in os.walk(POSTS_DIR):
        for f in files:
            if f.endswith('.md'):
                files_full_paths.append(os.path.join(root, f))
    
    nodes = []
    links = []
    id_map = {}

    for filepath in files_full_paths:
        try:
            filename = os.path.basename(filepath)
            with open(filepath, 'r', encoding='utf-8') as f:
                meta, content = parse_front_matter(f.read())
            
            file_id = os.path.splitext(filename)[0]
            id_map[filename] = file_id
            id_map[file_id] = file_id 
            
            # --- Tag Processing Start ---
            # 仅识别正文中的 #Tag 格式
            # 规则：#开头，前有空格或行首，\w+ 匹配所有文字(含中文)和数字、下划线
            inline_tags = re.findall(r'(?:^|\s)#(\w+)', content)
            
            # 去重 (保持顺序)
            combined_tags = []
            seen = set()
            for t in inline_tags:
                if t and t not in seen:
                    combined_tags.append(t)
                    seen.add(t)
            
            # 取第一个作为主分组，如果没有则为 default
            primary_group = combined_tags[0] if combined_tags else 'default'
            # --- Tag Processing End ---

            nodes.append({
                "id": file_id,
                "label": meta.get('title', file_id),
                "group": primary_group,     # 用于决定颜色的主标签
                "all_tags": combined_tags,  # 存储完整的多标签列表
                "content": content, 
                "val": 1
            })
        except Exception as e:
            print(f"[Warning] Failed to read {filename}: {e}")

    # Links
    link_pattern_wiki = re.compile(r'\[\[(.*?)\]\]')
    link_pattern_md = re.compile(r'\[.*?\]\((.*?)\)')
    
    for node in nodes:
        content_lower = node['content'].lower()
        # 简单优化：不需要每次都 compile
        
        # 1. Wiki Links [[Target]]
        if ENABLE_WIKI_LINKS:
            for match in link_pattern_wiki.findall(node['content']):
                target = match.split('|')[0].strip()
                for k, v in id_map.items():
                    if k.lower() == target.lower() or k.replace('.md','').lower() == target.lower():
                        if v != node['id']: 
                            links.append({"source": node['id'], "target": v, "type": "wiki"})
                        break
        
        # 2. Markdown Links [Title](./Target.md)
        for match in link_pattern_md.findall(node['content']):
            if match.startswith('http') or match.startswith('//'):
                continue
                
            # match is the URL part, e.g. "./posts/Target.md" or "Target.md"
            target_filename = match.split('/')[-1] # Extract filename
            target_id = os.path.splitext(target_filename)[0] # Remove extension
            
            # SimpleID Match
            if target_id in id_map and id_map[target_id] != node['id']:
                links.append({"source": node['id'], "target": id_map[target_id], "type": "md"})

    # Remove duplicates
    unique_links = []
    seen = set()
    for l in links:
        # 修正：将 type 也加入去重 Key，防止 Wiki Link 覆盖了同目标的 MD Link
        key = f"{l['source']}->{l['target']}:{l['type']}"
        if key not in seen:
            seen.add(key)
            unique_links.append(l)

    data = {"nodes": nodes, "links": unique_links}
    
    # 写入重试
    for i in range(3):
        try:
            with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
            print(f"[Build] Success! {len(nodes)} posts indexed.")
            break
        except Exception as e:
            time.sleep(0.5)

def is_port_in_use(port):
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        return s.connect_ex(('localhost', port)) == 0

def serve_forever_in_new_window():
    """
    弹出一个新的控制台窗口来运行服务器。
    这样主脚本就能退出，VSCode 终端就不会被卡住了。
    """
    print(f"[Server] Launching dedicated server on port {SERVER_PORT}...")
    
    # 构造命令： python -m http.server 8080
    cmd = [sys.executable, "-m", "http.server", str(SERVER_PORT)]
    
    # Windows 特定标志，用于打开新窗口
    CREATE_NEW_CONSOLE = 0x00000010
    
    try:
        subprocess.Popen(cmd, creationflags=CREATE_NEW_CONSOLE, cwd=os.getcwd())
        print(f"[Done] Server window opened. You can close it anytime to stop the server.")
    except Exception as e:
        print(f"[Error] Failed to launch server window: {e}")

def main():
    os.chdir(os.path.dirname(os.path.abspath(__file__)))
    print(f"=== Galactic Blog System {VERSION} ===")
    
    # 1. 更新数据
    build_data()
    
    # 2. 检查服务器状态
    url = f"http://localhost:{SERVER_PORT}"
    
    if is_port_in_use(SERVER_PORT):
        print(f"[Check] Server is already running.")
        print(f"[Refreh] Data updated. Please refresh your browser: {url}")
        # 尝试打开浏览器（如果还没打开）
        webbrowser.open(url)
    else:
        # 启动新服务器窗口
        serve_forever_in_new_window()
        # 给它一点时间启动
        time.sleep(1)
        webbrowser.open(url)
        
    print("=== Execution Finished (VSCode terminal is free now) ===")

if __name__ == '__main__':
    main()