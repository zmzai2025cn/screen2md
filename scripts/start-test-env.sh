#!/bin/bash
# start-test-env.sh - Wine测试环境启动脚本
# Usage: start-test-env.sh [options] [command]

set -e

# 默认配置
XVFB_RESOLUTION="1920x1080x24"
XVFB_DISPLAY=:99
TEST_MODE=${TEST_MODE:-"0"}
RESULTS_DIR="/results"

# 解析参数
COMMAND=""
while [[ $# -gt 0 ]]; do
    case $1 in
        --resolution)
            XVFB_RESOLUTION="$2"
            shift 2
            ;;
        --display)
            XVFB_DISPLAY="$2"
            shift 2
            ;;
        --test-mode)
            TEST_MODE="1"
            shift
            ;;
        --help)
            echo "Screen2MD Wine Test Environment"
            echo ""
            echo "Usage: $0 [options] [command]"
            echo ""
            echo "Options:"
            echo "  --resolution WxHxD    Set Xvfb resolution (default: 1920x1080x24)"
            echo "  --display :N          Set X11 display number (default: :99)"
            echo "  --test-mode           Enable test mode"
            echo "  --help                Show this help"
            echo ""
            echo "Commands:"
            echo "  shell                 Start interactive shell"
            echo "  test                  Run tests"
            echo "  benchmark             Run benchmarks"
            exit 0
            ;;
        *)
            COMMAND="$1"
            shift
            ;;
    esac
done

export DISPLAY="$XVFB_DISPLAY"

echo "=== Screen2MD Wine Test Environment ==="
echo "Display: $DISPLAY"
echo "Resolution: $XVFB_RESOLUTION"
echo "Test Mode: $TEST_MODE"
echo ""

# 1. 启动虚拟显示
echo "[1/4] Starting virtual display..."
Xvfb $DISPLAY -screen 0 $XVFB_RESOLUTION -ac +extension GLX +render -noreset &
XVFB_PID=$!
sleep 2

if ! kill -0 $XVFB_PID 2>/dev/null; then
    echo "ERROR: Failed to start Xvfb"
    exit 1
fi
echo "      Xvfb started (PID: $XVFB_PID)"

# 2. 启动窗口管理器
echo "[2/4] Starting window manager..."
fluxbox &
FLUXBOX_PID=$!
sleep 1
echo "      Fluxbox started (PID: $FLUXBOX_PID)"

# 3. 配置 Wine 环境
echo "[3/4] Configuring Wine..."
wine reg add "HKCU\\Software\\Wine\\DllOverrides" /v mscoree /d "" /f 2>/dev/null || true
wine reg add "HKCU\\Software\\Wine\\DllOverrides" /v mscorsvw.exe /d "" /f 2>/dev/null || true
echo "      Wine configured"

# 4. 启动测试代理
echo "[4/4] Starting test agent..."
if [ -f "/opt/test-agent/agent.py" ]; then
    python3 /opt/test-agent/agent.py &
    AGENT_PID=$!
    echo "      Test agent started (PID: $AGENT_PID)"
fi

echo ""
echo "=== Environment Ready ==="
echo ""

# 执行命令
case "$COMMAND" in
    shell)
        echo "Starting interactive shell..."
        /bin/bash
        ;;
    test)
        echo "Running tests..."
        if [ -f "/opt/test-agent/run_tests.py" ]; then
            python3 /opt/test-agent/run_tests.py
        else
            echo "No test runner found"
            exit 1
        fi
        ;;
    benchmark)
        echo "Running benchmarks..."
        if [ -f "/opt/test-agent/run_benchmarks.py" ]; then
            python3 /opt/test-agent/run_benchmarks.py
        else
            echo "No benchmark runner found"
            exit 1
        fi
        ;;
    "")
        echo "No command specified, keeping container alive..."
        echo "Use 'docker exec' to interact with this container"
        tail -f /dev/null
        ;;
    *)
        echo "Executing: $COMMAND"
        eval "$COMMAND"
        ;;
esac
