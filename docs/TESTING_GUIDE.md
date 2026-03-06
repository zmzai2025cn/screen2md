# Screen2MD Enterprise - 测试指南

## 快速开始

### 运行所有测试

```bash
dotnet test Screen2MD-v3.sln
```

### 运行特定类别的测试

```bash
# 仅单元测试
dotnet test --filter "Category=Unit"

# 仅集成测试
dotnet test --filter "Category=Integration"

# 性能测试
dotnet test --filter "Category=Benchmark"

# 压力测试
dotnet test --filter "Category=Stress"
```

---

## 测试分类

### 1. 单元测试

**目标**: 验证单个组件的正确性

**位置**: `tests/*Tests/`

**运行**:
```bash
dotnet test --filter "Category=Unit" --verbosity normal
```

**示例**:
```csharp
[Fact]
[Trait("Category", "Unit")]
public async Task CaptureService_WithValidOptions_ShouldSucceed()
{
    // Arrange
    var service = CreateCaptureService();
    
    // Act
    var result = await service.CaptureAsync();
    
    // Assert
    Assert.True(result.Success);
}
```

### 2. 集成测试

**目标**: 验证组件间的协作

**运行**:
```bash
dotnet test --filter "Category=Integration"
```

### 3. 性能基准测试

**目标**: 建立性能基线，确保SLA

**SLA标准**:
- 截图P95延迟: < 100ms
- OCR 1080p: < 5s
- 搜索10万文档: < 100ms

**运行**:
```bash
dotnet test --filter "Category=Benchmark" -c Release
```

### 4. 压力测试

**目标**: 发现并发问题和资源泄漏

**包含**:
- 100线程并发截图
- 内存泄漏检测
- 句柄泄漏检测

**运行**:
```bash
# 注意：耗时较长
dotnet test --filter "Category=Stress" --blame
```

### 5. 混沌测试

**目标**: 验证容错性和恢复能力

**场景**:
- 随机文件损坏
- 磁盘满
- 网络延迟
- 索引损坏

**运行**:
```bash
dotnet test --filter "Category=Chaos"
```

### 6. 安全测试

**目标**: 发现安全漏洞

**包含**:
- 路径遍历攻击
- SQL注入
- 敏感信息泄露

**运行**:
```bash
dotnet test --filter "Category=Security"
```

---

## 编写测试

### 测试命名规范

```csharp
// 格式: MethodName_StateUnderTest_ExpectedBehavior
[Fact]
public void CaptureAsync_WithInvalidPath_ShouldReturnFailure()

[Fact]
public void SearchAsync_WithKeywords_ShouldReturnMatchingResults()
```

### 测试结构 (AAA)

```csharp
[Fact]
public async Task TestName()
{
    // Arrange - 准备
    var service = CreateService();
    var input = "test";
    
    // Act - 执行
    var result = await service.DoSomethingAsync(input);
    
    // Assert - 验证
    Assert.Equal(expected, result);
}
```

### 使用 Trait 标记

```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("Component", "Capture")]
[Trait("Priority", "P0")]
public void TestName() { }
```

### 参数化测试

```csharp
[Theory]
[InlineData(1920, 1080)]
[InlineData(2560, 1440)]
[InlineData(3840, 2160)]
public async Task Ocr_DifferentResolutions_ShouldWork(int width, int height)
{
    var image = new MockImage(width, height);
    var result = await _ocr.RecognizeAsync(image);
    Assert.True(result.Success);
}
```

---

## 代码覆盖率

### 生成覆盖率报告

```bash
# 安装工具
dotnet tool install --global dotnet-coverage

# 收集覆盖率
dotnet-coverage collect "dotnet test" -f xml -o coverage.xml

# 生成HTML报告
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage.xml -targetdir:coveragereport -reporttypes:Html

# 打开报告
open coveragereport/index.html
```

### 覆盖率目标

| 模块 | 目标覆盖率 | 当前 |
|------|-----------|------|
| Core | 90% | 90%+ |
| Services | 85% | 85%+ |
| Engines | 70% | 70%+ |

---

## 调试测试

### 在 IDE 中调试

**VS Code**:
```json
{
    "name": "Debug Tests",
    "type": "coreclr",
    "request": "launch",
    "program": "dotnet",
    "args": ["test", "--filter", "FullyQualifiedName~TestName"],
    "cwd": "${workspaceFolder}"
}
```

### 命令行调试

```bash
# 运行特定测试并输出详细信息
dotnet test --filter "FullyQualifiedName~TestName" -v d

# 运行并保留输出
dotnet test --filter "TestName" --logger "console;verbosity=detailed"
```

---

## 性能测试最佳实践

### 1. Warmup

```csharp
// 预热，排除JIT编译影响
for (int i = 0; i < 10; i++)
    await service.CaptureAsync();
```

### 2. 多次测量

```csharp
var measurements = new List<double>();
for (int i = 0; i < 100; i++)
{
    var sw = Stopwatch.StartNew();
    await operation();
    measurements.Add(sw.Elapsed.TotalMilliseconds);
}

var p95 = measurements.OrderBy(x => x).Skip(95).First();
```

### 3. 独立环境

```csharp
public class PerformanceTests : IDisposable
{
    private readonly string _testDir;
    
    public PerformanceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }
    
    public void Dispose()
    {
        Directory.Delete(_testDir, recursive: true);
    }
}
```

---

## Mock 使用指南

### 创建 Mock 服务

```csharp
// 使用内置 Mock
var engine = new MockCaptureEngine();
var service = new CaptureService(engine, processor);

// 配置 Mock 行为
engine.Setup(e => e.CaptureAsync(It.IsAny<int>()))
    .ReturnsAsync(new MockCapturedImage());
```

### Mock 最佳实践

- 一个测试只验证一个行为
- 使用有意义的测试数据
- 清理资源（使用 IDisposable）

---

## CI/CD 集成

### GitHub Actions

```yaml
- name: Run Tests
  run: dotnet test --verbosity normal

- name: Run Tests with Coverage
  run: dotnet-coverage collect "dotnet test" -f xml -o coverage.xml

- name: Upload Coverage
  uses: codecov/codecov-action@v3
  with:
    files: ./coverage.xml
```

---

## 故障排除

### 测试失败但信息不足

```bash
# 增加详细输出
dotnet test -v d

# 查看完整异常
dotnet test --blame

# 生成 TRX 报告
dotnet test --logger trx --results-directory ./TestResults
```

### 测试在 CI 中失败但本地通过

**可能原因**:
1. 环境差异（检查 .NET 版本）
2. 文件路径问题（使用 Path.Combine）
3. 时区差异（使用 DateTimeOffset）
4. 并发问题（使用锁或隔离）

### 内存不足

```bash
# 限制并行度
dotnet test --maxcpucount 1

# 分批次运行
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

---

## 测试数据管理

### 测试数据目录

```
tests/
├── TestData/
│   ├── Images/
│   │   ├── test-1080p.png
│   │   └── test-4k.png
│   ├── Documents/
│   └── Configs/
```

### 使用测试数据

```csharp
[Fact]
public void TestWithData()
{
    var imagePath = Path.Combine("TestData", "Images", "test.png");
    var image = File.ReadAllBytes(imagePath);
    
    // 测试...
}
```

---

## 参考

- [xUnit 文档](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [.NET 测试最佳实践](https://docs.microsoft.com/en-us/dotnet/core/testing/)
