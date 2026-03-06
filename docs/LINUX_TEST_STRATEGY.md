# Screen2MD Enterprise - 纯Linux测试方案

**无Windows物理机的全自动化测试架构**

---

## 1. 测试策略总览

由于无法提供Windows物理机，采用三层Linux-native测试方案：

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     Linux 测试方案选型矩阵                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────┐  │
│  │   方案A         │  │   方案B         │  │   方案C               │  │
│  │   Wine+容器     │  │   KVM虚拟机     │  │   Windows容器         │  │
│  │   (推荐)        │  │   (备选)        │  │   (实验性)            │  │
│  ├─────────────────┤  ├─────────────────┤  ├─────────────────────────┤  │
│  │ • 轻量级        │  │ • 完整Windows   │  │ • 云原生              │  │
│  │ • 快速启动      │  │ • 100%兼容      │  │ • 可扩展              │  │
│  │ • 资源占用低    │  │ • 资源占用高    │  │ • 需要Windows镜像     │  │
│  │ • 兼容性~95%    │  │ • 兼容性100%    │  │ • 兼容性~90%          │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────────────┘  │
│                                                                          │
│  推荐组合: 方案A(主) + 方案B(回归) + 方案C(压力测试)                      │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. 方案A: Wine + Docker 容器化 (主力方案)

### 2.1 架构设计

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Linux 宿主机                                     │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    Docker 容器 (screen2md-test)                    │  │
│  │  ┌─────────────────────────────────────────────────────────────┐  │  │
│  │  │                      Wine 环境                               │  │  │
│  │  │  ┌─────────────────┐  ┌──────────────────────────────────┐  │  │  │
│  │  │  │  Windows应用     │  │  Screen2MD Enterprise (被测)     │  │  │  │
│  │  │  │  - Notepad       │  │  - 屏幕捕获引擎                  │  │  │  │
│  │  │  │  - Calc          │  │  - 变化检测                      │  │  │  │
│  │  │  │  - WordPad       │  │  - 隐私过滤                      │  │  │  │
│  │  │  └─────────────────┘  └──────────────────────────────────┘  │  │  │
│  │  │                                                              │  │  │
│  │  │  ┌──────────────────────────────────────────────────────────┐ │  │  │
│  │  │  │              Xvfb (虚拟显示)                              │ │  │  │
│  │  │  │  - 无头模式运行                                          │ │  │  │
│  │  │  │  - 支持多种分辨率                                        │ │  │  │
│  │  │  │  - 屏幕录制能力                                          │ │  │  │
│  │  │  └──────────────────────────────────────────────────────────┘ │  │  │
│  │  └─────────────────────────────────────────────────────────────┘  │  │
│  │                                                                    │  │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────────┐  │  │
│  │  │ 测试协调器       │  │ 日志收集器       │  │ 性能监控器       │  │  │
│  │  │ (Test Agent)    │  │ (Log Collector) │  │ (Perf Monitor)   │  │  │
│  │  └─────────────────┘  └─────────────────┘  └──────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    测试协调中心 (OpenClaw)                         │  │
│  │  - 测试用例管理                                                    │  │
│  │  - 结果收集与分析                                                  │  │
│  │  - 报告生成                                                        │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Docker 镜像构建

```dockerfile
# Dockerfile.test-wine
FROM ubuntu:22.04

# 安装基础依赖
RUN dpkg --add-architecture i386 \
    && apt-get update \
    && apt-get install -y \
        wine64 \
        wine32 \
        winetricks \
        xvfb \
        x11-apps \
        wget \
        curl \
        python3 \
        python3-pip \
        dotnet-runtime-8.0 \
    && rm -rf /var/lib/apt/lists/*

# 配置 Wine
ENV WINEARCH=win64
ENV WINEPREFIX=/root/.wine
ENV DISPLAY=:99

# 初始化 Wine
RUN wine --version
RUN winetricks -q corefonts

# 安装测试用的 Windows 应用
RUN wget -O /tmp/notepad.exe "https://.../notepad.exe" \
    && wine /tmp/notepad.exe /S

# 创建虚拟显示
RUN mkdir -p /tmp/.X11-unix

# 复制被测软件
COPY artifacts/Screen2MD-Setup.exe /tmp/
RUN wine /tmp/Screen2MD-Setup.exe /SILENT

# 复制测试代理
COPY test-agent/ /opt/test-agent/
RUN pip3 install -r /opt/test-agent/requirements.txt

# 启动脚本
COPY scripts/start-test-env.sh /opt/
RUN chmod +x /opt/start-test-env.sh

ENTRYPOINT ["/opt/start-test-env.sh"]
```

### 2.3 测试执行流程

```bash
#!/bin/bash
# start-test-env.sh

# 1. 启动虚拟显示
Xvfb :99 -screen 0 1920x1080x24 &
export DISPLAY=:99

# 2. 启动窗口管理器 (可选，用于更真实的测试)
fluxbox &

# 3. 启动测试应用
wine /opt/screen2md/Screen2MD.Daemon.exe &
DAEMON_PID=$!

# 4. 启动测试代理
python3 /opt/test-agent/agent.py &
AGENT_PID=$!

# 5. 等待终止信号
wait $AGENT_PID
```

### 2.4 测试用例示例 (Linux-native)

```python
# test_linux_wine.py
import pytest
import docker
import time
import requests
from datetime import datetime, timedelta

class TestScreen2MDWine:
    """Wine容器化测试 - 零Bug保障"""
    
    @pytest.fixture(scope="class")
    def test_container(self):
        """启动测试容器"""
        client = docker.from_env()
        
        container = client.containers.run(
            "screen2md-test:wine",
            detach=True,
            environment={
                "DISPLAY": ":99",
                "TEST_MODE": "1"
            },
            volumes={
                "test-results": {"bind": "/results", "mode": "rw"}
            },
            mem_limit="2g",
            cpu_period=100000,
            cpu_quota=100000  # 1 CPU core
        )
        
        # 等待服务就绪
        time.sleep(10)
        
        yield container
        
        # 清理
        container.stop()
        container.remove()
    
    def test_zero_crash_24h(self, test_container):
        """24小时零崩溃测试"""
        start_time = datetime.now()
        duration = timedelta(hours=24)
        
        while datetime.now() - start_time < duration:
            # 检查进程状态
            result = test_container.exec_run(
                "pgrep -f Screen2MD.Daemon.exe"
            )
            
            # 零Bug断言: 进程必须存活
            assert result.exit_code == 0, \
                f"Process crashed at {datetime.now()}"
            
            # 检查错误日志
            logs = test_container.exec_run(
                "cat /opt/screen2md/logs/error.log"
            )
            
            # 零Bug断言: 无Error级别日志
            assert logs.output.decode().count("[ERROR]") == 0, \
                f"Error logs found: {logs.output.decode()}"
            
            time.sleep(5)
    
    def test_resource_usage_limits(self, test_container):
        """资源占用测试"""
        # 运行5分钟，收集指标
        metrics = []
        for _ in range(30):  # 30次采样
            stats = test_container.stats(stream=False)
            
            cpu_usage = stats['cpu_stats']['cpu_usage']['total_usage']
            memory_usage = stats['memory_stats']['usage'] / (1024 * 1024)  # MB
            
            metrics.append({
                'cpu': cpu_usage,
                'memory': memory_usage
            })
            
            time.sleep(10)
        
        avg_memory = sum(m['memory'] for m in metrics) / len(metrics)
        max_memory = max(m['memory'] for m in metrics)
        
        # 零Bug断言: 内存占用 < 50MB
        assert max_memory < 50, \
            f"Memory usage exceeded 50MB: max={max_memory:.2f}MB"
        
        assert avg_memory < 30, \
            f"Average memory usage too high: avg={avg_memory:.2f}MB"
    
    def test_privacy_filter(self, test_container):
        """隐私过滤器测试"""
        # 创建包含敏感内容的测试窗口
        test_container.exec_run(
            """
            wine notepad.exe &
            echo "Password: secret123" | wine notepad.exe
            """,
            detach=True
        )
        
        time.sleep(2)
        
        # 触发捕获
        result = test_container.exec_run(
            "wine /opt/screen2md/Screen2MD.CLI.exe --capture-now"
        )
        
        # 检查捕获结果
        captures = test_container.exec_run(
            "ls /opt/screen2md/data/captures/"
        )
        
        # 读取最新捕获
        latest = test_container.exec_run(
            "cat /opt/screen2md/data/captures/latest.json"
        )
        
        capture_data = json.loads(latest.output)
        
        # 零Bug断言: 敏感内容必须被拦截
        assert capture_data['privacy_blocked'] == True, \
            "Sensitive content was not blocked by privacy filter"
        
        assert 'secret123' not in capture_data.get('text_content', ''), \
            "Password found in captured text"
```

---

## 3. 方案B: KVM虚拟机 (回归测试)

### 3.1 架构设计

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Linux 宿主机 (KVM)                               │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    KVM 虚拟机 (Windows 10)                         │  │
│  │  ┌─────────────────────────────────────────────────────────────┐  │  │
│  │  │                  Windows 10 完整系统                         │  │  │
│  │  │                                                              │  │  │
│  │  │  ┌──────────────────────────────────────────────────────┐   │  │  │
│  │  │  │              真实 Windows 环境                        │   │  │  │
│  │  │  │  - 完整 Win32 API                                    │   │  │  │
│  │  │  │  - 真实 GUI 渲染                                     │   │  │  │
│  │  │  │  - 所有软件兼容                                      │   │  │  │
│  │  │  │                                                      │   │  │  │
│  │  │  │  Screen2MD Enterprise (被测)                         │   │  │  │
│  │  │  │  Test Agent (Windows服务)                           │   │  │  │
│  │  │  └──────────────────────────────────────────────────────┘   │  │  │
│  │  └─────────────────────────────────────────────────────────────┘  │  │
│  │                                                                    │  │
│  │  连接: SPICE/VNC + SSH                                             │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  适用场景:                                                               │
│  - 最终回归测试 (每周一次)                                              │
│  - 兼容性验证                                                           │
│  - 发布前验证                                                           │
└─────────────────────────────────────────────────────────────────────────┘
```

### 3.2 虚拟机配置

```yaml
# vm-config.yaml
vm:
  name: "screen2md-test-win10"
  memory: 4096  # 4GB
  vcpus: 2
  disk: 40G
  os: "windows10"
  
network:
  type: "nat"
  ssh_port: 2222
  spice_port: 5900

display:
  type: "spice"
  resolution: "1920x1080"

snapshot:
  base: "clean-install"  # 基础快照
  test: "test-state"     # 测试快照
```

### 3.3 自动化脚本

```bash
#!/bin/bash
# kvm-test-runner.sh

VM_NAME="screen2md-test-win10"
SNAPSHOT_BASE="clean-install"

# 1. 恢复快照 (确保干净环境)
virsh snapshot-revert $VM_NAME $SNAPSHOT_BASE

# 2. 启动虚拟机
virsh start $VM_NAME

# 3. 等待Windows启动
sleep 60

# 4. 安装最新版本
scp -P 2222 artifacts/Screen2MD-Setup.exe admin@localhost:/tmp/
ssh -p 2222 admin@localhost "C:/tmp/Screen2MD-Setup.exe /SILENT"

# 5. 执行测试
python3 test_runner.py --host localhost --port 2222 --duration 24h

# 6. 收集结果
scp -P 2222 admin@localhost:/results/* ./results/

# 7. 关闭虚拟机
virsh shutdown $VM_NAME
```

---

## 4. 方案C: Windows容器 (实验性)

```dockerfile
# 需要 Windows Server Core 镜像 (实验性)
FROM mcr.microsoft.com/windows/servercore:ltsc2022

# 安装 .NET 8
ADD https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.ps1 /tmp/
RUN powershell -File /tmp/dotnet-install.ps1 -Channel 8.0

# 复制被测软件
COPY artifacts/ /app/

# 启动
ENTRYPOINT ["C:/app/Screen2MD.Daemon.exe"]
```

**限制**: 
- 需要 Windows 容器运行时 (不能在纯Linux运行)
- 仅适用于云环境 (Azure Container Instances)

---

## 5. 测试矩阵

| 测试类型 | 方案A (Wine) | 方案B (KVM) | 频率 |
|----------|--------------|-------------|------|
| 单元测试 | ✅ | ❌ | 每次提交 |
| 集成测试 | ✅ | ❌ | 每次提交 |
| 稳定性测试 | ✅ | ✅ | 每日 |
| 回归测试 | ⚠️ | ✅ | 每周 |
| 压力测试 | ✅ | ❌ | 每周 |
| 发布验证 | ⚠️ | ✅ | 每次发布 |

---

## 6. 零Bug保障 (Linux环境)

### 6.1 断言框架 (Python版)

```python
# zero_bug_assertions.py
class ZeroBugViolation(Exception):
    """零Bug违规异常"""
    pass

class ZeroBugAssertions:
    """零Bug断言 - Linux原生实现"""
    
    @staticmethod
    def no_process_crashes(container, duration_seconds):
        """验证容器内进程未崩溃"""
        start = time.time()
        while time.time() - start < duration_seconds:
            result = container.exec_run("pgrep -f Screen2MD")
            if result.exit_code != 0:
                raise ZeroBugViolation(
                    f"Process crashed after {time.time() - start:.1f}s"
                )
            time.sleep(1)
    
    @staticmethod
    def no_error_logs(container, log_path, since_seconds):
        """验证无错误日志"""
        result = container.exec_run(
            f"grep -c '\\[ERROR\\]' {log_path} || echo 0"
        )
        error_count = int(result.output.decode().strip())
        
        if error_count > 0:
            # 获取错误详情
            errors = container.exec_run(
                f"grep '\\[ERROR\\]' {log_path} | tail -20"
            )
            raise ZeroBugViolation(
                f"Found {error_count} error logs:\n{errors.output.decode()}"
            )
    
    @staticmethod
    def memory_within_limit(container, max_mb):
        """验证内存占用在限制内"""
        stats = container.stats(stream=False)
        memory_mb = stats['memory_stats']['usage'] / (1024 * 1024)
        
        if memory_mb > max_mb:
            raise ZeroBugViolation(
                f"Memory usage {memory_mb:.1f}MB exceeds limit {max_mb}MB"
            )
    
    @staticmethod
    def cpu_within_limit(container, max_percent):
        """验证CPU占用在限制内"""
        # 需要计算CPU使用率
        stats1 = container.stats(stream=False)
        time.sleep(1)
        stats2 = container.stats(stream=False)
        
        cpu_delta = stats2['cpu_stats']['cpu_usage']['total_usage'] - \
                    stats1['cpu_stats']['cpu_usage']['total_usage']
        system_delta = stats2['cpu_stats']['system_cpu_usage'] - \
                       stats1['cpu_stats']['system_cpu_usage']
        
        cpu_percent = (cpu_delta / system_delta) * 100
        
        if cpu_percent > max_percent:
            raise ZeroBugViolation(
                f"CPU usage {cpu_percent:.1f}% exceeds limit {max_percent}%"
            )
```

---

## 7. CI/CD 集成

```yaml
# .github/workflows/linux-test.yml
name: Linux Zero-Bug Tests

on: [push, pull_request]

jobs:
  wine-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Docker
        uses: docker/setup-buildx-action@v3
      
      - name: Build Test Image
        run: docker build -t screen2md-test:wine -f Dockerfile.test-wine .
      
      - name: Run Unit Tests
        run: |
          docker run --rm \
            -v $(pwd)/results:/results \
            screen2md-test:wine \
            pytest tests/unit -v --tb=short
      
      - name: Run Integration Tests
        run: |
          docker run --rm \
            -v $(pwd)/results:/results \
            screen2md-test:wine \
            pytest tests/integration -v
      
      - name: Run 1h Stability Test
        timeout-minutes: 70
        run: |
          docker run --rm \
            --name s2md-stability \
            -v $(pwd)/results:/results \
            screen2md-test:wine \
            pytest tests/stability/test_1h_zero_crash.py -v
      
      - name: Check Results
        run: |
          if [ -f results/zero-bug-violations.txt ]; then
            echo "ZERO BUG VIOLATIONS FOUND:"
            cat results/zero-bug-violations.txt
            exit 1
          fi
      
      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: results/

  kvm-regression:
    runs-on: self-hosted  # 需要KVM支持的runner
    if: github.ref == 'refs/heads/main'
    needs: wine-tests
    steps:
      - uses: actions/checkout@v4
      
      - name: Run KVM Regression Tests
        run: ./scripts/kvm-test-runner.sh
        timeout-minutes: 2880  # 48 hours
```

---

## 8. 测试报告

测试完成后自动生成报告：

```
================================================================================
                    Screen2MD Enterprise - 测试报告
================================================================================

测试时间: 2026-03-XX XX:XX:XX
测试环境: Linux + Wine 8.0
测试时长: 24小时

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                              零Bug验证结果
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[✅] 零崩溃验证: PASSED
     - 进程存活时间: 24:00:00
     - 崩溃次数: 0

[✅] 零报错验证: PASSED  
     - Error日志数: 0
     - Warning日志数: 3 (在允许范围内)

[✅] 资源占用验证: PASSED
     - 平均CPU: 0.3%
     - 峰值CPU: 1.2%
     - 平均内存: 28.5MB
     - 峰值内存: 42.1MB

[✅] 功能验证: PASSED
     - 捕获次数: 17,280
     - 识别准确率: 96.5%
     - 隐私拦截: 100% (23次敏感内容)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                              结论
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

状态: ✅ ALL TESTS PASSED - ZERO BUG ACHIEVED

该版本符合发布标准，可进入生产环境。

================================================================================
```

---

**文档版本**: v1.0.0  
**测试框架**: Screen2MD.LinuxTest  
**维护团队**: QA Team  
**日期**: 2026-03-05
