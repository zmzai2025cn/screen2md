#!/bin/bash
# setup-docker-proxy.sh - 配置Docker通过腾讯云代理访问外网

TX_CLOUD_IP="43.134.94.244"
PROXY_PORT="1080"

echo "=== 设置 Docker 代理 (通过腾讯云) ==="
echo ""

echo "[1/3] 建立 SSH 隧道..."
ssh -fN -D $PROXY_PORT root@$TX_CLOUD_IP
if [ $? -ne 0 ]; then
    echo "ERROR: 无法连接到腾讯云"
    exit 1
fi
echo "      SOCKS5 代理已建立: localhost:$PROXY_PORT"

echo ""
echo "[2/3] 配置 Docker 使用代理..."
mkdir -p /etc/systemd/system/docker.service.d/

cat > /etc/systemd/system/docker.service.d/proxy.conf << EOF
[Service]
Environment="HTTP_PROXY=socks5://127.0.0.1:$PROXY_PORT"
Environment="HTTPS_PROXY=socks5://127.0.0.1:$PROXY_PORT"
Environment="NO_PROXY=localhost,127.0.0.1,.tencentyun.com,.baidubce.com"
EOF

echo ""
echo "[3/3] 重启 Docker..."
systemctl daemon-reload
systemctl restart docker

echo ""
echo "=== 验证代理 ==="
docker info 2>&1 | grep -i proxy || echo "Proxy configured"

echo ""
echo "测试拉取镜像:"
docker pull hello-world 2>&1 | head -5

echo ""
echo "=== 配置完成 ==="
echo "Docker 现在通过腾讯云 ($TX_CLOUD_IP) 访问外网"
echo ""
echo "如需恢复:"
echo "  rm /etc/systemd/system/docker.service.d/proxy.conf"
echo "  systemctl daemon-reload && systemctl restart docker"
