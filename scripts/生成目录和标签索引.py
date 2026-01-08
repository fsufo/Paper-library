import os
import re

def generate_markdown_indexes():
    # 1. 基础配置
    root_dir = os.getcwd()
    
    # 定义输出文件夹和文件名
    output_folder_name = "笔记目录[脚本生成]"
    output_dir = os.path.join(root_dir, output_folder_name)
    
    output_outline_file = os.path.join(output_dir, "All_Notes_Outline.md")
    output_tag_file = os.path.join(output_dir, "Tag_Index.md")

    # 2. 如果文件夹不存在,则创建
    if not os.path.exists(output_dir):
        print(f"创建目录: {output_folder_name}")
        os.makedirs(output_dir)

    # 正则表达式
    tag_pattern = re.compile(r'(?<![#&(\[])#([\w\u4e00-\u9fa5\-]+)')

    all_files_data = []
    tag_to_files = {}

    print("正在扫描工程目录...")

    # 3. 遍历文件
    for dirpath, dirnames, filenames in os.walk(root_dir):
        
        # 优化:如果当前遍历的目录是生成的输出目录,直接跳过
        if output_folder_name in dirpath:
            continue

        for filename in filenames:
            if not filename.endswith(".md"):
                continue
            
            file_path = os.path.join(dirpath, filename)
            
            try:
                with open(file_path, 'r', encoding='utf-8') as f:
                    content = f.read()
            except Exception as e:
                print(f"无法读取文件 {filename}: {e}")
                continue

            # 提取标签
            tags = tag_pattern.findall(content)
            unique_tags = sorted(list(set(tags)))

            # 计算相对于"输出文件夹"的路径
            rel_path = os.path.relpath(file_path, output_dir)
            rel_path = rel_path.replace(os.sep, '/')

            file_info = {
                'name': filename,
                'path': rel_path,
                'tags': unique_tags
            }
            
            all_files_data.append(file_info)

            for tag in unique_tags:
                if tag not in tag_to_files:
                    tag_to_files[tag] = []
                tag_to_files[tag].append(file_info)

    # 4. 生成全部笔记大纲
    with open(output_outline_file, 'w', encoding='utf-8') as f:
        f.write("# 全部笔记大纲\n\n")
        
        # --- 修改点 A: 写入自身的Tag ---
        f.write("#目录\n\n")
        # ----------------------------
        
        f.write(f"> 自动生成位置: {output_folder_name}\n\n")
        
        for info in all_files_data:
            f.write(f"### [{info['name']}]({info['path']})\n")
            if info['tags']:
                tag_line = " ".join([f"#{t}" for t in info['tags']])
                f.write(f"{tag_line}\n")
            else:
                f.write("*(无标签)*\n")
            f.write("\n---\n\n")

    # 5. 生成标签索引
    with open(output_tag_file, 'w', encoding='utf-8') as f:
        f.write("# 标签索引\n\n")
        
        # --- 修改点 B: 写入自身的Tag ---
        f.write("#目录\n\n")
        # ----------------------------

        sorted_tags = sorted(tag_to_files.keys())
        
        if not sorted_tags:
            f.write("未扫描到任何标签.\n")
        
        for tag in sorted_tags:
            f.write(f"## #{tag}\n")
            files = tag_to_files[tag]
            for info in files:
                f.write(f"- [{info['name']}]({info['path']})\n")
            f.write("\n")

    print(f"处理完成!")
    print(f"文件已生成在目录: {output_folder_name}")
    print(f"生成的md文件已添加 #目录 标签")

if __name__ == "__main__":
    generate_markdown_indexes()