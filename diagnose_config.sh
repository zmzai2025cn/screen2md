#!/bin/bash
# ConfigurationService 崩溃诊断脚本

echo "=== ConfigurationService 崩溃诊断 ==="
echo ""

# 测试1: 损坏的 JSON 文件
echo "Test 1: 损坏的 JSON 文件"
echo "not valid json {[" > /tmp/corrupted_test.json
cat /tmp/corrupted_test.json
echo ""

cd /root/.openclaw/workspace/screen2md-enterprise

# 运行单个测试并捕获输出
dotnet test tests/Screen2MD.Core.Tests/ \
    --filter "FullyQualifiedName~Constructor_WithCorruptedFile" \
    -v d \
    2>&1 | tail -50

echo ""
echo "=== 清理 ==="
rm -f /tmp/corrupted_test.json
