# 文件位置: main.py (根目录)
import os
import sys
import http.server
import socket
import subprocess
import webbrowser
import time

# --- 禁止生成 .pyc 缓存文件 ---
sys.dont_write_bytecode = True  # <--- 加上这一行

# --- 导入构建脚本 ---
# 将 scripts 目录加入搜索路径，以便能 import build_data
sys.path.append(os.path.join(os.path.dirname(__file__), 'scripts'))
import build_data  # 导入刚才拆分出去的模块

SERVER_PORT = 8080
VERSION = "4.2 (Split Architecture)"

def is_port_in_use(port):
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        return s.connect_ex(('localhost', port)) == 0

def serve_forever_in_new_window():
    print(f"[Server] Launching dedicated server on port {SERVER_PORT}...")
    cmd = [sys.executable, "-m", "http.server", str(SERVER_PORT)]
    CREATE_NEW_CONSOLE = 0x00000010
    try:
        subprocess.Popen(cmd, creationflags=CREATE_NEW_CONSOLE, cwd=os.getcwd())
        print(f"[Done] Server window opened.")
    except Exception as e:
        print(f"[Error] Failed to launch server window: {e}")

def main():
    # 确保在根目录运行
    os.chdir(os.path.dirname(os.path.abspath(__file__)))
    print(f"=== Galactic Blog System {VERSION} ===")
    
    # 1. 调用公共的构建逻辑
    print(">>> Running Build Script...")
    build_data.build_data()
    print(">>> Build Finished.")
    
    # 2. 启动服务器逻辑
    url = f"http://localhost:{SERVER_PORT}"
    
    if is_port_in_use(SERVER_PORT):
        print(f"[Check] Server is already running.")
        print(f"[Refresh] Data updated. Please refresh your browser: {url}")
        webbrowser.open(url)
    else:
        serve_forever_in_new_window()
        time.sleep(1)
        webbrowser.open(url)
        
    print("=== Execution Finished ===")

if __name__ == '__main__':
    main()