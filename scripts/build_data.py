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
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)
POSTS_DIR = os.path.join(PROJECT_ROOT, 'posts')
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
    同时修复 Markdown 图片 ![]() 和 HTML 图片 <img src=""> 的路径
    """
    # 计算当前文件相对于项目根目录的路径 (例如: posts/Tech)
    current_dir_rel = os.path.relpath(os.path.dirname(file_path), PROJECT_ROOT)

    # --- 核心路径处理逻辑 ---
    def process_url(url):
        # Url decode
        url = urllib.parse.unquote(url)
        # 如果是网络图片或绝对路径，不处理
        if url.startswith(('http://', 'https://', '//', 'data:', '/')):
            return url
        
        # 修正逻辑：计算相对于 HTML 文件 (OUTPUT_DIR) 的路径
        # 1. 获取图片绝对路径 (基于当前 Markdown 文件所在目录)
        md_dir = os.path.dirname(file_path)
        abs_img_path = os.path.join(md_dir, url)
        abs_img_path = os.path.normpath(abs_img_path)

        # 2. 计算从 HTML 目录 (Tools/星空笔记本) 到图片的相对路径
        # 结果类似于: ../../posts/Tech/image.png
        try:
            rel_to_html = os.path.relpath(abs_img_path, OUTPUT_DIR)
            new_path = rel_to_html.replace('\\', '/')
            return new_path
        except Exception as e:
            # 如果路径计算出错（比如跨盘符），回退到原始路径
            return url

    # 1. 修复 Markdown 语法: ![alt](url)
    def replace_md(match):
        alt = match.group(1)
        url = match.group(2)
        new_url = process_url(url)
        # if new_url != url:
        #     log(f"Markdown图修复: '{url}' -> '{new_url}'")
        return f'![{alt}]({new_url})'
    
    # 正则: ! [ ... ] ( ... )
    content = re.sub(r'!\[(.*?)\]\((.*?)(?:\s+".*?")?\)', replace_md, content)

    # 2. 修复 HTML 语法: <img ... src="url" ...>
    def replace_html(match):
        prefix = match.group(1) # 捕获 <img ... src="
        url = match.group(2)    # 捕获 url
        suffix = match.group(3) # 捕获 结束引号 "
        
        new_url = process_url(url)
        # if new_url != url:
        #     log(f"HTML图修复: '{url}' -> '{new_url}'")
        
        return f'{prefix}{new_url}{suffix}'

    # 正则说明:
    html_pattern = r'(<img\s+[^>]*?src=["\'])(.*?)(["\'])'
    content = re.sub(html_pattern, replace_html, content, flags=re.IGNORECASE)

    # 3. 修复 PDF 链接: [text](path/to/file.pdf)
    def replace_pdf(match):
        text = match.group(1)
        url = match.group(2)
        # 如果是 web 链接或锚点，忽略
        if url.startswith(('http://', 'https://', '//', '#', 'mailto:')):
            return match.group(0)
        
        new_url = process_url(url)
        
        # 修复 Text 中的方括号，避免 Markdown 解析错误
        # 例如: [[2017]Name] -> [\[2017\]Name]
        safe_text = text.replace('[', '\\[').replace(']', '\\]')

        # 对 URL 进行安全处理
        # 如果 URL 包含空格或括号，最好用 <> 包裹，但并非所有解析器都支持 <>
        # 稳妥做法：使用 URL 编码，但保留 / 等路径分隔符
        # safe_url = urllib.parse.quote(new_url, safe="/:.")
        
        # 这里暂时只返回修正后的路径，如果路径里有空格，应使用 %20
        safe_url = new_url.replace(' ', '%20')
        
        # 另外，如果 URL 包含未平衡的括号，Markdown 也会通过。
        # 只要确保返回正确格式即可。
        return f'[{safe_text}]({safe_url})'

    # 正则: [text](url.pdf) 
    # 使用 .*?\.pdf 强制匹配到 .pdf 后缀，从而跨越文件名中间可能存在的括号 () [] {}
    # flag=re.IGNORECASE 使得也能匹配 .PDF
    # 这里的关键是 (.*?) 能够跨越嵌套的 []
    pdf_pattern = r'\[(.*?)\]\((.*?\.pdf)\)'
    content = re.sub(pdf_pattern, replace_pdf, content, flags=re.IGNORECASE)

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