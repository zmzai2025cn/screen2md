# Screen2MD v3.0 完整测试计划

## 目标：生产级质量

- 代码覆盖率: ≥ 80%
- 测试通过率: 100%
- 边界条件覆盖: 100%
- 异常处理覆盖: 100%

---

## 第一阶段：单元测试补全（2 天）

### 1. CaptureService 测试

```csharp
// 正常流程测试
[Fact] CaptureAsync_WithValidOptions_ShouldReturnSuccess()
[Fact] CaptureAsync_WithDetectChanges_ShouldSkipSimilarImages()
[Fact] CaptureAsync_AllDisplays_ShouldCaptureEachDisplay()
[Fact] CaptureAsync_SingleDisplay_ShouldCapturePrimaryOnly()

// 边界条件测试
[Fact] CaptureAsync_WithEmptyOptions_ShouldUseDefaults()
[Fact] CaptureAsync_WithCancellation_ShouldStopImmediately()
[Theory] CaptureAsync_WithInvalidDisplayIndex_ShouldThrow(int index)

// 异常处理测试
[Fact] CaptureAsync_WhenEngineThrows_ShouldReturnFailure()
[Fact] CaptureAsync_WhenDiskFull_ShouldHandleGracefully()
[Fact] CaptureAsync_WhenNoDisplays_ShouldReturnEmptyResult()

// 并发测试
[Fact] CaptureAsync_ConcurrentCalls_ShouldBeThreadSafe()

// 性能测试
[Fact] CaptureAsync_1000Calls_ShouldCompleteInUnder10Seconds()
```

### 2. OcrService 测试

```csharp
// 正常流程
[Fact] RecognizeAsync_WithValidImage_ShouldReturnText()
[Fact] RecognizeAsync_WithChinese_ShouldReturnChineseText()
[Fact] RecognizeAsync_WithCode_ShouldPreserveIndentation()

// 边界条件
[Fact] RecognizeAsync_WithEmptyImage_ShouldReturnEmptyResult()
[Fact] RecognizeAsync_WithHugeImage_ShouldNotCrash()
[Fact] RecognizeAsync_WithTimeout_ShouldCancelGracefully()

// 异常处理
[Fact] RecognizeAsync_WhenEngineUnavailable_ShouldReturnError()
[Fact] RecognizeAsync_WhenEngineThrows_ShouldHandleException()

// 性能
[Fact] RecognizeAsync_100Images_ShouldAverageUnder500ms()
```

### 3. StorageService 测试

```csharp
// 正常流程
[Fact] GetStats_WithExistingFiles_ShouldReturnCorrectStats()
[Fact] CleanupAsync_WithOldFiles_ShouldDeleteThem()
[Fact] CleanupAsync_WithRecentFiles_ShouldPreserveThem()

// 边界条件
[Fact] GetStats_WithEmptyDirectory_ShouldReturnZeroStats()
[Fact] GetStats_WithNestedDirectories_ShouldCountAllFiles()
[Fact] CleanupAsync_WithZeroDays_ShouldDeleteAllFiles()
[Fact] CleanupAsync_WithReadOnlyFiles_ShouldHandleGracefully()

// 异常处理
[Fact] CleanupAsync_WhenDirectoryLocked_ShouldNotCrash()
[Fact] GetStats_WhenPathTooLong_ShouldHandleGracefully()
```

### 4. ConfigurationService 测试

```csharp
// 正常流程
[Fact] Get_WithExistingKey_ShouldReturnValue()
[Fact] Get_WithMissingKey_ShouldReturnDefault()
[Fact] Set_WithNewKey_ShouldPersistValue()
[Fact] Set_WithExistingKey_ShouldUpdateValue()

// 边界条件
[Fact] Get_WithNullKey_ShouldThrowArgumentNullException()
[Fact] Set_WithNullValue_ShouldStoreNull()
[Fact] Get_WithComplexType_ShouldDeserializeCorrectly()
[Fact] ConcurrentReadsAndWrites_ShouldBeThreadSafe()

// 异常处理
[Fact] Load_WhenFileCorrupted_ShouldReturnEmptyConfig()
[Fact] Save_WhenDiskFull_ShouldThrowIOException()
```

---

## 第二阶段：集成测试（2 天）

### 1. 端到端工作流测试

```csharp
// 完整截图流程
[Fact, PlatformSpecific(OSPlatform.Windows)]
FullWorkflow_CaptureAndOcr_ShouldExtractText()

// 多显示器流程
[Fact, PlatformSpecific(OSPlatform.Windows)]
FullWorkflow_MultiDisplayCapture_ShouldCaptureAllScreens()

// 存储流程
[Fact]
FullWorkflow_CaptureAndSave_ShouldCreateFile()

// 清理流程
[Fact]
FullWorkflow_OldFiles_ShouldBeCleanedUp()
```

### 2. 平台集成测试

```csharp
// Windows 平台
[Fact, PlatformSpecific(OSPlatform.Windows)]
Windows_CaptureEngine_ShouldReturnRealScreens()
[Fact, PlatformSpecific(OSPlatform.Windows)]
Windows_OcrEngine_ShouldCallTesseract()

// Mock 平台（Linux/CI）
[Fact]
Mock_CaptureEngine_ShouldReturnTestImages()
[Fact]
Mock_OcrEngine_ShouldReturnMockText()
```

---

## 第三阶段：边界和压力测试（1 天）

### 1. 边界条件测试

```csharp
// 图像边界
[Theory]
[InlineData(1, 1)]       // 最小图像
[InlineData(1, 1080)]    // 高但窄
[InlineData(1920, 1)]    // 宽但矮
[InlineData(7680, 4320)] // 8K
[InlineData(15360, 8640)] // 16K
CaptureRegion_ExtremeSizes_ShouldHandle(int width, int height)

// 文件系统边界
[Fact]
Save_ToPathWith255Chars_ShouldWork()
[Fact]
Save_ToPathWith256Chars_ShouldFail()
[Fact]
Save_WhenFileExists_ShouldOverwrite()

// 内存边界
[Fact]
Process_10000Images_ShouldNotLeakMemory()
[Fact]
Process_1GBImage_ShouldNotCrash()
```

### 2. 压力测试

```csharp
// 并发压力
[Fact]
Concurrent_100ThreadsCapturing_ShouldNotCrash()
[Fact]
Concurrent_1000OcrRequests_ShouldQueueProperly()

// 长时间运行
[Fact]
Continuous_24HourRun_ShouldNotLeakMemory()
[Fact]
Continuous_100000Captures_ShouldRemainStable()

// 资源限制
[Fact]
WithLimitedMemory_ShouldHandleGracefully()
[Fact]
WithLimitedDiskSpace_ShouldCleanupAutomatically()
```

---

## 第四阶段：安全测试（1 天）

```csharp
// 路径遍历防护
[Fact]
Save_WithPathTraversal_ShouldBeBlocked()
[Fact]
Save_WithNullBytesInPath_ShouldBeBlocked()

// 输入验证
[Fact]
Ocr_WithMalformedImage_ShouldNotCrash()
[Fact]
Config_WithHugeJson_ShouldNotCrash()

// 权限测试
[Fact]
Read_WithoutReadPermission_ShouldThrowUnauthorizedAccessException()
[Fact]
Write_WithoutWritePermission_ShouldThrowUnauthorizedAccessException()
```

---

## 第五阶段：静态分析和代码审查（1 天）

### 1. 静态分析工具

```bash
# .NET 分析器
dotnet analyze

# SonarQube（如果有）
sonar-scanner

# CodeClimate
codeclimate analyze
```

### 2. 代码审查清单

- [ ] 所有 public API 都有 XML 文档
- [ ] 所有 async 方法都有 CancellationToken 参数
- [ ] 所有 IDisposable 对象都正确释放
- [ ] 所有异常都被正确处理
- [ ] 没有空引用风险
- [ ] 没有资源泄漏

---

## 第六阶段：文档和发布（1 天）

### 1. 测试文档

- [ ] 测试覆盖率报告
- [ ] 性能基准报告
- [ ] 已知限制文档

### 2. 发布检查清单

- [ ] 所有测试通过
- [ ] 覆盖率 ≥ 80%
- [ ] 无编译警告
- [ ] 无代码分析警告
- [ ] 性能基准通过
- [ ] 文档完整

---

## 预计时间

| 阶段 | 时间 | 产出 |
|------|------|------|
| 单元测试补全 | 2 天 | 100+ 单元测试 |
| 集成测试 | 2 天 | 20+ 集成测试 |
| 边界和压力测试 | 1 天 | 30+ 压力测试 |
| 安全测试 | 1 天 | 10+ 安全测试 |
| 静态分析 | 1 天 | 0 警告 |
| 文档和发布 | 1 天 | 发布包 |
| **总计** | **8 天** | **生产级质量** |

---

## 当前状态 vs 目标

| 指标 | 当前 | 目标 | 差距 |
|------|------|------|------|
| 测试数量 | 10 | 160+ | 150+ |
| 代码覆盖率 | 5% | 80% | 75% |
| 边界条件 | 0 | 100% | 100% |
| 异常处理 | 0 | 100% | 100% |
| 性能基准 | 0 | 有 | 缺失 |

**结论：当前测试严重不足，需要 8 天补全才能达到生产级质量。**