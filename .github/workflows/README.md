# Screen2MD Enterprise - CI/CD 流程文档

## 概述

本CI/CD流程基于 GitHub Actions，采用**多阶段流水线**设计，确保代码质量、测试覆盖和自动化发布。

---

## 流水线架构

```
┌─────────────────────────────────────────────────────────────┐
│  Stage 1: Code Quality                                      │
│  - 代码格式化检查                                            │
│  - 编译（警告作为错误）                                       │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  Stage 2: Unit Tests (矩阵: Ubuntu/Windows/MacOS)          │
│  - 快速反馈（~2分钟）                                        │
│  - 跨平台兼容性验证                                          │
└─────────────────────────────────────────────────────────────┘
                              ↓
        ┌─────────────────────┼─────────────────────┐
        ↓                     ↓                     ↓
┌───────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ Stage 3:      │  │ Stage 4:         │  │ Stage 5:         │
│ Integration   │  │ Stress Tests     │  │ Security Scan    │
│ Tests         │  │ (并发/内存/混沌)  │  │ (CodeQL/注入)    │
└───────────────┘  └──────────────────┘  └──────────────────┘
        │                     │                     │
        └─────────────────────┼─────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  Stage 6: Build Release                                     │
│  - Linux x64 可执行文件                                      │
│  - Windows x64 可执行文件                                    │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  Stage 7: Create Release (Tags Only)                        │
│  - GitHub Release 自动创建                                   │
│  - 制品上传到 CDN                                           │
└─────────────────────────────────────────────────────────────┘
```

---

## 触发条件

| 事件 | 触发的工作流 |
|------|-------------|
| `push` 到 `main`/`develop` | 完整CI流程 |
| `pull_request` | 代码质量 + 单元测试 |
| `tag` (v*) | 完整CI + 发布 |

---

## 各阶段详解

### Stage 1: Code Quality

**目的**: 确保代码风格一致，无编译警告

**检查项**:
- `dotnet format --verify-no-changes` - 代码格式化
- `dotnet build -p:TreatWarningsAsErrors=true` - 零警告编译

**失败条件**: 任何格式化错误或编译警告

### Stage 2: Unit Tests

**目的**: 快速验证核心功能，跨平台兼容性

**测试矩阵**:
| OS | 运行时 | 目的 |
|----|--------|------|
| Ubuntu Latest | Linux x64 | 主目标平台 |
| Windows Latest | Windows x64 | Windows兼容性 |
| macOS Latest | macOS x64 | macOS兼容性 |

**测试范围**: `Category=Unit` (~250个测试)

**时间**: ~2分钟

### Stage 3: Integration Tests

**目的**: 验证组件集成

**测试范围**: `Category=Integration` (~30个测试)

**平台**: Ubuntu + Windows

### Stage 4: Stress Tests

**目的**: 发现并发问题、内存泄漏、性能退化

**测试类型**:
- **并发压力**: 100线程并发截图
- **内存泄漏**: 1000次操作内存监控
- **混沌工程**: 故障注入和恢复

**超时**: 30分钟

**仅在Linux运行**（资源限制）

### Stage 5: Security Scan

**目的**: 发现安全漏洞

**扫描项**:
1. **安全单元测试**: 路径遍历、SQL注入、敏感信息过滤
2. **CodeQL**: GitHub官方静态分析
3. **依赖漏洞**: `dotnet list package --vulnerable`

### Stage 6: Build Release

**目的**: 构建可执行文件

**构建配置**:
```bash
dotnet publish \
  --configuration Release \
  --runtime [linux-x64|win-x64] \
  --self-contained true \
  --single-file true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

**输出**:
- `screen2md-linux-x64.tar.gz`
- `screen2md-win-x64.zip`

### Stage 7: Create Release

**触发条件**: Git tag 匹配 `v*`

**自动执行**:
1. 下载所有构建产物
2. 创建 GitHub Release
3. 自动生成 Release Notes
4. 标记为 Pre-release（如果 tag 包含 `-rc` 或 `-beta`）
5. 上传到生产 CDN（仅正式版本）

---

## 本地开发命令

### 快速验证
```bash
# 构建
make build

# 运行单元测试
make test-unit

# 运行压力测试
make test-stress

# 代码检查
make lint
```

### Docker 开发
```bash
# 构建镜像
make docker-build

# 运行容器
make docker-run

# 启动完整环境（含监控）
docker-compose --profile monitoring up -d
```

### 发布构建
```bash
# Linux 发布包
make publish-linux

# Windows 发布包  
make publish-windows
```

---

## 测试分类说明

### 测试属性标签

| 标签 | 说明 | CI阶段 |
|------|------|--------|
| `Category=Unit` | 单元测试 | Stage 2 |
| `Category=Integration` | 集成测试 | Stage 3 |
| `Category=Stress` | 压力测试 | Stage 4 |
| `Category=Memory` | 内存测试 | Stage 4 |
| `Category=Security` | 安全测试 | Stage 5 |
| `Category=Performance` | 性能测试 | Stage 4 |
| `Category=Chaos` | 混沌工程 | Stage 4 |
| `Duration=Long` | 长时间测试 | Stage 4 |

### 运行特定测试

```bash
# 仅单元测试
dotnet test --filter "Category=Unit"

# 排除压力测试
dotnet test --filter "Category!=Stress"

# 特定平台和类别
dotnet test --filter "Category=Unit|Category=Integration"
```

---

## 质量门禁

### 通过条件

| 阶段 | 通过标准 |
|------|---------|
| Code Quality | 零格式化错误，零编译警告 |
| Unit Tests | 100%通过，跨平台一致 |
| Integration Tests | 100%通过 |
| Stress Tests | 成功率>95%，无内存泄漏 |
| Security Scan | 无高危漏洞 |
| Build | 成功构建两个平台 |

### 失败处理

1. **Code Quality 失败**: 修复格式化问题后重新推送
2. **测试失败**: 查看Artifacts中的测试报告，修复后重新推送
3. **安全扫描失败**: 修复漏洞，更新依赖版本
4. **构建失败**: 检查平台特定代码

---

## 监控和告警

### 测试报告

- **GitHub Actions**: 自动显示在PR中
- **Artifacts**: 下载详细测试结果（.trx文件）
- **Code Coverage**: Codecov集成，覆盖率报告

### 通知

可在 `.github/workflows/ci.yml` 中添加:

```yaml
- name: Notify Slack
  if: failure()
  uses: 8398a7/action-slack@v3
  with:
    status: ${{ job.status }}
    fields: repo,message,commit,author
  env:
    SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK }}
```

---

## 版本发布流程

### 1. 准备发布

```bash
# 更新版本号（在 .csproj 中）
# 更新 CHANGELOG.md

# 提交更改
git add .
git commit -m "Release v1.2.0"
git push origin main
```

### 2. 创建 Tag

```bash
# 正式版本
git tag -a v1.2.0 -m "Release version 1.2.0"
git push origin v1.2.0

# 或 RC版本
git tag -a v1.2.0-rc1 -m "Release candidate 1"
git push origin v1.2.0-rc1
```

### 3. 自动触发

创建 tag 后，CI 自动:
1. 运行完整测试
2. 构建发布包
3. 创建 GitHub Release
4. 上传制品

### 4. 手动验证

下载构建产物进行最终验证:
```bash
# Linux
./screen2md --version
./screen2md --self-test

# Windows
screen2md.exe --version
screen2md.exe --self-test
```

---

## 故障排除

### 常见问题

**Q: 测试在本地通过，CI失败？**
A: 检查平台差异，使用 `dotnet test --runtime` 指定运行时测试

**Q: Docker构建失败？**
A: 检查Dockerfile中的系统依赖，尝试 `docker build --no-cache`

**Q: 发布包无法运行？**
A: 确认 `--self-contained` 和 `--single-file` 参数正确

---

## 扩展建议

### 可添加的功能

1. **多架构支持**: ARM64 (Apple Silicon, Raspberry Pi)
2. **签名验证**: Windows代码签名，macOS公证
3. **自动更新**: 内置更新检查机制
4. **A/B测试**: 金丝雀发布
5. **性能基准对比**: 与上一版本性能对比

---

## 文档索引

- 本文档: `.github/workflows/README.md`
- CI配置: `.github/workflows/ci.yml`
- Docker: `Dockerfile`, `docker-compose.yml`
- Makefile: `Makefile`
- 测试报告: `docs/HIGH_INTENSITY_TESTS_REPORT.md`
