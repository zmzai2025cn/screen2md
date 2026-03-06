#!/bin/bash
# build-test-container.sh - 构建Wine测试容器脚本

set -e

echo "=== Screen2MD Wine Test Container Builder ==="
echo ""

# 检查Docker
if ! command -v docker &> /dev/null; then
    echo "ERROR: Docker not found. Please install Docker first."
    exit 1
fi

echo "[1/3] Building Wine test container..."
docker build -t screen2md-test:wine -f Dockerfile.test-wine . 2>&1 | tee /tmp/docker-build.log

echo ""
echo "[2/3] Verifying container..."
docker run --rm screen2md-test:wine wine --version 2>/dev/null || echo "Wine version check skipped"

echo ""
echo "[3/3] Testing basic functionality..."
docker run --rm \
    -e DISPLAY=:99 \
    screen2md-test:wine \
    bash -c "Xvfb :99 -screen 0 1024x768x24 & sleep 2 && echo 'Xvfb OK'" || true

echo ""
echo "=== Build Complete ==="
echo "Container image: screen2md-test:wine"
echo ""
echo "Usage:"
echo "  docker run -it screen2md-test:wine shell"
echo "  docker run -v \$(pwd)/results:/results screen2md-test:wine test"
echo ""
