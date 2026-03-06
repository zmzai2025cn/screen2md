#!/bin/bash
# build-on-txcloud.sh - 在腾讯云机器上构建并导出镜像

set -e

echo "=== 在腾讯云构建 Screen2MD 测试镜像 ==="
echo "目标机器: 43.134.94.244 (TXclaw)"
echo ""

# 确保代码已同步到腾讯云
echo "[1/4] 同步代码到腾讯云..."
rsync -avz --exclude='bin' --exclude='obj' \
    /root/.openclaw/workspace/screen2md-enterprise/ \
    root@43.134.94.244:/opt/screen2md-enterprise/

echo ""
echo "[2/4] 在腾讯云构建镜像..."
ssh root@43.134.94.244 << 'EOF'
    cd /opt/screen2md-enterprise
    docker build -t screen2md-test:wine -f Dockerfile.test-wine .
EOF

echo ""
echo "[3/4] 导出镜像..."
ssh root@43.134.94.244 "docker save screen2md-test:wine | gzip" > /tmp/screen2md-test-wine.tar.gz

echo ""
echo "[4/4] 导入到本地Docker..."
docker load < /tmp/screen2md-test-wine.tar.gz

echo ""
echo "=== 构建完成 ==="
docker images | grep screen2md-test
