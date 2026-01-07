import os
import random
import datetime

POSTS_DIR = os.path.join(os.getcwd(), 'posts')
if not os.path.exists(POSTS_DIR):
    os.makedirs(POSTS_DIR)

# 主题词库
TOPICS = ['AI', 'Quantum', 'Coffee', 'Music', 'Design', 'Space', 'Cat', 'GameDev', 'Cyberpunk', 'React']
ACTIONS = ['Thinking', 'Building', 'Loving', 'Hating', 'Fixing', 'Dreaming', 'Exploring']

def create_post(i, all_titles):
    topic = random.choice(TOPICS)
    action = random.choice(ACTIONS)
    title = f"{action} about {topic} {i}"
    filename = f"post_{i}.md"
    
    # 随机生成引用
    links = []
    if all_titles:
        # 引用 1-3 个已存在的文章
        num_links = random.randint(1, 4)
        targets = random.sample(all_titles, k=min(num_links, len(all_titles)))
        for t in targets:
            # 混合使用 [[WikiLink]] 和 [MarkdownLink]
            if random.random() > 0.5:
                links.append(f"- 参考资料：[[{t.replace('.md', '')}]]")
            else:
                links.append(f"- 延伸阅读：[{t} 的详情]({t})")
    
    content = f"""---
title: {title}
date: {datetime.date.today()}
tags: [{topic.lower()}, diary]
---

# {title}

这里是关于 **{topic}** 的一些想法。

## 思考
在这个充满熵增的宇宙中，我们需要更多的 **{random.choice(TOPICS)}**。

### 关联文章
{chr(10).join(links)}

> 这是一个自动生成的测试片段。
"""
    
    with open(os.path.join(POSTS_DIR, filename), 'w', encoding='utf-8') as f:
        f.write(content)
    
    return filename

# 生成 20 篇文章
existing_files = [f for f in os.listdir(POSTS_DIR) if f.endswith('.md')]
print("Generating test posts...")

for i in range(1, 21):
    new_file = create_post(i, existing_files)
    existing_files.append(new_file)

print("Done! Generated 20 linked posts.")
