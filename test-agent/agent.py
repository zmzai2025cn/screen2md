#!/usr/bin/env python3
"""
Screen2MD Wine Test Agent
测试代理 - 在Wine容器中协调测试执行
"""

import os
import sys
import json
import time
import signal
import logging
import subprocess
from datetime import datetime
from pathlib import Path

# 配置日志
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(),
        logging.FileHandler('/results/test-agent.log')
    ]
)
logger = logging.getLogger('TestAgent')

class TestAgent:
    """Wine测试容器中的测试代理"""
    
    def __init__(self):
        self.results_dir = Path('/results')
        self.results_dir.mkdir(exist_ok=True)
        self.running = False
        
    def start(self):
        """启动代理"""
        logger.info("Test Agent starting...")
        self.running = True
        
        # 设置信号处理
        signal.signal(signal.SIGTERM, self._handle_shutdown)
        signal.signal(signal.SIGINT, self._handle_shutdown)
        
        # 写入状态文件
        self._write_status('ready')
        
        logger.info("Test Agent ready")
        
        # 保持运行
        while self.running:
            time.sleep(1)
            
    def _handle_shutdown(self, signum, frame):
        """处理关闭信号"""
        logger.info(f"Received signal {signum}, shutting down...")
        self.running = False
        self._write_status('stopped')
        
    def _write_status(self, status):
        """写入状态文件"""
        status_file = self.results_dir / 'agent-status.json'
        data = {
            'status': status,
            'timestamp': datetime.now().isoformat(),
            'pid': os.getpid()
        }
        with open(status_file, 'w') as f:
            json.dump(data, f)
            
    def run_command(self, command, timeout=60):
        """执行命令并返回结果"""
        logger.info(f"Executing: {command}")
        
        try:
            result = subprocess.run(
                command,
                shell=True,
                capture_output=True,
                text=True,
                timeout=timeout
            )
            
            return {
                'exit_code': result.returncode,
                'stdout': result.stdout,
                'stderr': result.stderr,
                'success': result.returncode == 0
            }
        except subprocess.TimeoutExpired:
            return {
                'exit_code': -1,
                'stdout': '',
                'stderr': 'Command timed out',
                'success': False
            }
        except Exception as e:
            return {
                'exit_code': -1,
                'stdout': '',
                'stderr': str(e),
                'success': False
            }

def main():
    agent = TestAgent()
    agent.start()

if __name__ == '__main__':
    main()
