#!/bin/bash
# run-tests.sh - 内核测试运行脚本

set -e

echo "=========================================="
echo "Screen2MD Kernel Test Suite"
echo "=========================================="

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 项目路径
PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
TEST_PROJECT="$PROJECT_ROOT/src/Screen2MD.Kernel.Tests"
COVERAGE_DIR="$PROJECT_ROOT/test-results"

# 创建输出目录
mkdir -p "$COVERAGE_DIR"

echo ""
echo "Building test project..."
dotnet build "$TEST_PROJECT" --configuration Release

echo ""
echo "=========================================="
echo "Running Unit Tests"
echo "=========================================="
dotnet test "$TEST_PROJECT" \
    --filter "Category=Unit" \
    --no-build \
    --logger "console;verbosity=detailed" \
    --logger "trx;LogFileName=$COVERAGE_DIR/unit-tests.trx" \
    --collect:"XPlat Code Coverage" \
    --results-directory "$COVERAGE_DIR"

UNIT_EXIT=$?

echo ""
echo "=========================================="
echo "Running ZeroBug Tests"
echo "=========================================="
dotnet test "$TEST_PROJECT" \
    --filter "Category=ZeroBug" \
    --no-build \
    --logger "console;verbosity=detailed" \
    --logger "trx;LogFileName=$COVERAGE_DIR/zerobug-tests.trx"

ZEROBUG_EXIT=$?

echo ""
echo "=========================================="
echo "Running Integration Tests"
echo "=========================================="
dotnet test "$TEST_PROJECT" \
    --filter "Category=Integration" \
    --no-build \
    --logger "console;verbosity=detailed" \
    --logger "trx;LogFileName=$COVERAGE_DIR/integration-tests.trx"

INTEGRATION_EXIT=$?

echo ""
echo "=========================================="
echo "Running Performance Tests"
echo "=========================================="
dotnet test "$TEST_PROJECT" \
    --filter "Category=Performance" \
    --no-build \
    --logger "console;verbosity=detailed" \
    --logger "trx;LogFileName=$COVERAGE_DIR/performance-tests.trx"

PERFORMANCE_EXIT=$?

echo ""
echo "=========================================="
echo "Test Summary"
echo "=========================================="

if [ $UNIT_EXIT -eq 0 ]; then
    echo -e "${GREEN}✓ Unit Tests: PASSED${NC}"
else
    echo -e "${RED}✗ Unit Tests: FAILED${NC}"
fi

if [ $ZEROBUG_EXIT -eq 0 ]; then
    echo -e "${GREEN}✓ ZeroBug Tests: PASSED${NC}"
else
    echo -e "${RED}✗ ZeroBug Tests: FAILED${NC}"
fi

if [ $INTEGRATION_EXIT -eq 0 ]; then
    echo -e "${GREEN}✓ Integration Tests: PASSED${NC}"
else
    echo -e "${RED}✗ Integration Tests: FAILED${NC}"
fi

if [ $PERFORMANCE_EXIT -eq 0 ]; then
    echo -e "${GREEN}✓ Performance Tests: PASSED${NC}"
else
    echo -e "${RED}✗ Performance Tests: FAILED${NC}"
fi

# 生成覆盖率报告
if command -v reportgenerator &> /dev/null; then
    echo ""
    echo "Generating coverage report..."
    reportgenerator \
        -reports:"$COVERAGE_DIR/**/coverage.cobertura.xml" \
        -targetdir:"$COVERAGE_DIR/coverage-report" \
        -reporttypes:"Html;TextSummary"
    
    echo ""
    echo "Coverage report generated: $COVERAGE_DIR/coverage-report/index.html"
fi

# 计算总退出码
TOTAL_EXIT=$((UNIT_EXIT + ZEROBUG_EXIT + INTEGRATION_EXIT + PERFORMANCE_EXIT))

echo ""
echo "=========================================="
if [ $TOTAL_EXIT -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
    echo "=========================================="
    exit 0
else
    echo -e "${RED}Some tests failed.${NC}"
    echo "=========================================="
    exit 1
fi