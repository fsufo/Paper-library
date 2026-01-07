# 文件位置: scripts/build_data.py
import os
import json
import re
import time
import sys

# --- 动态计算路径 ---
# 获取当前脚本所在目录 (scripts/)
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
# 获取项目根目录 (scripts 的上一级)
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)

POSTS_DIR = os.path.join(PROJECT_ROOT, 'posts')
OUTPUT_FILE = os.path.join(PROJECT_ROOT, 'posts_data.json')
ENABLE_WIKI_LINKS = True 

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
        # 如果是 CI 环境，没有 posts 目录应该报错
        print(f"[Error] Posts directory not found at {POSTS_DIR}")
        return False
        
    files_full_paths = []
    for root, dirs, files in os.walk(POSTS_DIR):
        for f in files:
            if f.endswith('.md'):
                files_full_paths.append(os.path.join(root, f))
    
    nodes = []
    links = []
    id_map = {}

    # 1. 解析节点
    for filepath in files_full_paths:
        try:
            filename = os.path.basename(filepath)
            with open(filepath, 'r', encoding='utf-8') as f:
                meta, content = parse_front_matter(f.read())
            
            file_id = os.path.splitext(filename)[0]
            id_map[filename] = file_id
            id_map[file_id] = file_id 
            
            # 提取正文 #Tag
            inline_tags = re.findall(r'(?:^|\s)#(\w+)', content)
            combined_tags = []
            seen = set()
            for t in inline_tags:
                if t and t not in seen:
                    combined_tags.append(t)
                    seen.add(t)
            
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
            print(f"[Warning] Failed to read {filename}: {e}")

    # 2. 解析连接
    link_pattern_wiki = re.compile(r'\[\[(.*?)\]\]')
    link_pattern_md = re.compile(r'\[.*?\]\((.*?)\)')
    
    for node in nodes:
        if ENABLE_WIKI_LINKS:
            for match in link_pattern_wiki.findall(node['content']):
                target = match.split('|')[0].strip()
                for k, v in id_map.items():
                    if k.lower() == target.lower() or k.replace('.md','').lower() == target.lower():
                        if v != node['id']: 
                            links.append({"source": node['id'], "target": v, "type": "wiki"})
                        break
        
        for match in link_pattern_md.findall(node['content']):
            if match.startswith('http') or match.startswith('//'):
                continue
            target_filename = match.split('/')[-1]
            target_id = os.path.splitext(target_filename)[0]
            if target_id in id_map and id_map[target_id] != node['id']:
                links.append({"source": node['id'], "target": id_map[target_id], "type": "md"})

    # 3. 去重
    unique_links = []
    seen = set()
    for l in links:
        key = f"{l['source']}->{l['target']}:{l['type']}"
        if key not in seen:
            seen.add(key)
            unique_links.append(l)

    data = {"nodes": nodes, "links": unique_links}
    
    # 4. 写入文件
    try:
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
        print(f"[Build] Success! {len(nodes)} posts indexed. Output: {OUTPUT_FILE}")
        return True
    except Exception as e:
        print(f"[Error] Failed to write output: {e}")
        return False

# 这段代码让它可以被 python scripts/build_data.py 直接调用
if __name__ == '__main__':
    success = build_data()
    if not success:
        sys.exit(1) # 如果失败，告诉 GitHub Action 报错