# Screen2MD Enterprise - 全自动测试架构

**零Bug、零人工干预的测试体系**

---

## 1. 测试哲学

### 1.1 核心原则

```
"一旦测试完成，绝不出现任何Bug、报错、崩溃或逻辑问题"
```

### 1.2 测试金字塔 (自动化率100%)

```
                    /\
                   /  \     E2E 测试 (10%)
                  /----\    - 完整工作流
                 /      \   - 7×24小时运行
                /--------\  
               /   集成   \  集成测试 (30%)
              /   测试    \  - 模块交互
             /------------\ - API契约
            /    单元测试   \ 单元测试 (60%)
           /                 \ - 函数/类级别
          /-------------------\ - 边界条件
```

---

## 2. 测试环境架构

### 2.1 跨平台测试方案

由于 OpenClaw 运行在 Linux，而被测软件在 Windows：

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Linux 测试机 (OpenClaw)                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    测试协调中心 (TestOrchestrator)                │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐             │   │
│  │  │ 测试调度器   │  │ 结果分析器   │  │ 报告生成器   │             │   │
│  │  │ Scheduler   │  │ Analyzer    │  │ Reporter    │             │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘             │   │
│  │                                                                  │   │
│  │  - 测试用例管理 (TestRail API)                                   │   │
│  │  - 测试执行控制                                                   │   │
│  │  - 结果收集与验证                                                 │   │
│  │  - 报告自动生成                                                   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                              │ SSH/WinRM                                │
│                              ▼                                          │
├─────────────────────────────────────────────────────────────────────────┤
│                         Windows 被测机                                   │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    测试代理 (TestAgent.exe)                       │   │
│  │  - 接收并执行测试命令                                             │   │
│  │  - 采集系统状态 (CPU/内存/日志)                                    │   │
│  │  - 返回执行结果                                                   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                              │                                          │
│              ┌───────────────┼───────────────┐                        │
│              ▼               ▼               ▼                        │
│  ┌────────────────┐ ┌──────────────┐ ┌──────────────┐               │
│  │ Screen2MD      │ │ FlaUI        │ │ 屏幕模拟器    │               │
│  │ (被测软件)      │ │ (自动化控制)  │ │ (虚拟显示器)  │               │
│  └────────────────┘ └──────────────┘ └──────────────┘               │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 通信协议

```csharp
// 测试命令协议 (JSON over SSH)
public class TestCommand
{
    public string CommandId { get; set; }        // 命令唯一ID
    public CommandType Type { get; set; }        // 命令类型
    public Dictionary<string, object> Params { get; set; }  // 参数
    public TimeSpan Timeout { get; set; }        // 超时时间
}

public enum CommandType
{
    StartService,      // 启动被测服务
    StopService,       // 停止服务
    CaptureScreen,     // 执行截屏
    SimulateUI,        // 模拟UI操作
    GetLogs,           // 获取日志
    GetMetrics,        // 获取性能指标
    ExecuteTest        // 执行测试用例
}

// 测试结果协议
public class TestResult
{
    public string CommandId { get; set; }
    public ResultStatus Status { get; set; }     // Success/Failure/Timeout
    public object Data { get; set; }             // 结果数据
    public List<LogEntry> Logs { get; set; }     // 相关日志
    public MetricsSnapshot Metrics { get; set; } // 性能快照
}
```

---

## 3. 零Bug测试框架

### 3.1 零崩溃保障

```csharp
public static class ZeroCrashAssertions
{
    /// <summary>
    /// 验证进程在测试期间未崩溃
    /// </summary>
    public static void ProcessDidNotCrash(Process process, TimeSpan duration)
    {
        var startTime = DateTime.Now;
        while (DateTime.Now - startTime < duration)
        {
            if (process.HasExited)
            {
                var exitCode = process.ExitCode;
                throw new ZeroBugViolationException(
                    $"Process crashed with exit code {exitCode} after " +
                    $"{(DateTime.Now - startTime).TotalSeconds}s");
            }
            Thread.Sleep(100);
        }
    }
    
    /// <summary>
    /// 验证无未处理异常
    /// </summary>
    public static void NoUnhandledExceptions(ILogReader logReader)
    {
        var exceptions = logReader.GetEntries()
            .Where(e => e.Level == LogLevel.Fatal || 
                       e.Message.Contains("unhandled exception"))
            .ToList();
        
        if (exceptions.Any())
        {
            throw new ZeroBugViolationException(
                $"Found {exceptions.Count} unhandled exceptions:\n" +
                string.Join("\n", exceptions.Select(e => $"  - {e.Timestamp}: {e.Message}")));
        }
    }
    
    /// <summary>
    /// 7×24小时连续运行测试
    /// </summary>
    [Fact]
    [Trait("Category", "ZeroBug")]
    [Trait("Duration", "LongRunning")]
    public async Task SevenByTwentyFour_Hour_Stability_Test()
    {
        // 安排测试
        var testId = await _orchestrator.ScheduleTest(new TestSchedule
        {
            Name = "7x24_Stability",
            Duration = TimeSpan.FromDays(7),
            Interval = TimeSpan.FromSeconds(5),
            WindowsVersion = "Win11_23H2"
        });
        
        // 等待完成
        var result = await _orchestrator.WaitForCompletion(testId, 
            timeout: TimeSpan.FromDays(8));
        
        // 验证结果
        Assert.Equal(TestStatus.Passed, result.Status);
        Assert.Equal(0, result.CrashCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.True(result.UptimePercentage > 99.99);
    }
}
```

### 3.2 零报错保障

```csharp
public static class ZeroErrorAssertions
{
    /// <summary>
    /// 验证无 Error 级别日志
    /// </summary>
    public static void NoErrorLogs(ILogReader logReader, TimeSpan period)
    {
        var errors = logReader.GetEntries(DateTime.Now - period, DateTime.Now)
            .Where(e => e.Level >= LogLevel.Error)
            .ToList();
        
        if (errors.Any())
        {
            throw new ZeroBugViolationException(
                $"Found {errors.Count} error logs in the last {period}:\n" +
                string.Join("\n", errors.Take(10).Select(e => 
                    $"  [{e.Timestamp:HH:mm:ss}] {e.Message}")));
        }
    }
    
    /// <summary>
    /// 验证特定操作不产生错误
    /// </summary>
    public static async Task OperationProducesNoErrors(
        Func<Task> operation, 
        ILogReader logReader)
    {
        var beforeCount = logReader.GetErrorCount();
        
        await operation();
        
        var afterCount = logReader.GetErrorCount();
        
        if (afterCount > beforeCount)
        {
            var newErrors = logReader.GetErrors().Skip(beforeCount).ToList();
            throw new ZeroBugViolationException(
                $"Operation produced {newErrors.Count} new errors:\n" +
                string.Join("\n", newErrors.Select(e => $"  - {e.Message}")));
        }
    }
}
```

### 3.3 零逻辑错误保障

```csharp
public static class ZeroLogicErrorAssertions
{
    /// <summary>
    /// 验证所有捕获都有有效元数据
    /// </summary>
    public static void AllCapturesHaveValidMetadata(List<CaptureRecord> captures)
    {
        foreach (var capture in captures)
        {
            // 必须有时间戳
            Assert.NotEqual(default(DateTime), capture.Timestamp);
            
            // 必须有软件类型
            Assert.NotEqual(SoftwareType.Unknown, capture.SoftwareType);
            
            // 文件必须存在
            Assert.True(File.Exists(capture.ScreenshotPath), 
                $"Screenshot file missing: {capture.ScreenshotPath}");
            
            // 文件大小必须合理
            var fileInfo = new FileInfo(capture.ScreenshotPath);
            Assert.True(fileInfo.Length > 0, "Screenshot file is empty");
            Assert.True(fileInfo.Length < 50 * 1024 * 1024, 
                "Screenshot file too large (>50MB)");
            
            // 必须有文本内容或标记为无文本
            if (!capture.HasText)
            {
                Assert.True(capture.TextContent == null || capture.TextContent == "",
                    "Marked as no text but has text content");
            }
        }
    }
    
    /// <summary>
    /// 验证隐私过滤器正确工作
    /// </summary>
    public static void PrivacyFilterWorksCorrectly(
        List<CaptureRecord> captures,
        List<string> sensitiveKeywords)
    {
        var violations = captures
            .Where(c => !c.PrivacyBlocked)
            .Where(c => sensitiveKeywords.Any(kw => 
                c.TextContent?.Contains(kw) == true))
            .ToList();
        
        if (violations.Any())
        {
            throw new ZeroBugViolationException(
                $"Privacy filter failed for {violations.Count} captures. " +
                $"Sensitive content was not blocked:\n" +
                string.Join("\n", violations.Take(5).Select(v => 
                    $"  - {v.Timestamp}: {v.WindowTitle}")));
        }
    }
    
    /// <summary>
    /// 验证时序正确性
    /// </summary>
    public static void TimestampsAreSequential(List<CaptureRecord> captures)
    {
        for (int i = 1; i < captures.Count; i++)
        {
            var prev = captures[i - 1].Timestamp;
            var curr = captures[i].Timestamp;
            
            Assert.True(curr >= prev,
                $"Timestamp out of order at index {i}: {prev} -> {curr}");
            
            // 验证时间间隔合理（不超过1分钟，除非是暂停状态）
            var interval = curr - prev;
            if (interval > TimeSpan.FromMinutes(1))
            {
                Assert.True(captures[i].IsAfterResume || captures[i-1].IsBeforePause,
                    $"Unexpected large time gap: {interval.TotalSeconds}s");
            }
        }
    }
}
```

---

## 4. 测试用例设计

### 4.1 测试矩阵

| 测试类别 | 用例数 | 执行时间 | 目标 |
|----------|--------|----------|------|
| **单元测试** | 500+ | <5分钟 | 代码覆盖率>95% |
| **集成测试** | 100+ | <30分钟 | 模块间契约正确 |
| **稳定性测试** | 20 | 7×24小时 | 零崩溃 |
| **压力测试** | 10 | 2小时 | 极限负载稳定 |
| **兼容性测试** | 50 | 4小时 | Win10/Win11 |
| **场景测试** | 30 | 6小时 | 真实使用场景 |

### 4.2 核心测试场景

```csharp
public class CoreTestScenarios
{
    [Theory]
    [InlineData(SamplingMode.FixedInterval, 5)]
    [InlineData(SamplingMode.Adaptive, 0)]  // 自适应，无固定间隔
    [InlineData(SamplingMode.OnChangeOnly, 0)]
    [Trait("Category", "Core")]
    public async Task Capture_WithDifferentSamplingModes(
        SamplingMode mode, int intervalSeconds)
    {
        // 配置服务
        await _agent.Configure(new CaptureConfig
        {
            SamplingMode = mode,
            IntervalSeconds = intervalSeconds
        });
        
        // 启动服务
        await _agent.StartService();
        
        // 运行一段时间
        await Task.Delay(TimeSpan.FromMinutes(1));
        
        // 获取捕获记录
        var captures = await _agent.GetCaptures();
        
        // 验证
        ZeroCrashAssertions.ProcessDidNotCrash(_agent.Process, 
            TimeSpan.FromMinutes(1));
        ZeroErrorAssertions.NoErrorLogs(_agent.LogReader, 
            TimeSpan.FromMinutes(1));
        ZeroLogicErrorAssertions.AllCapturesHaveValidMetadata(captures);
    }
    
    [Fact]
    [Trait("Category", "Privacy")]
    [Trait("Category", "ZeroBug")]
    public async Task PrivacyFilter_BlocksAllSensitiveContent()
    {
        // 准备测试数据（包含敏感信息的界面）
        var testWindows = new[]
        {
            ("Login Form", "Username: admin\nPassword: ********"),
            ("Banking App", "Card: 6222 **** **** 1234"),
            ("ID Card", "身份证号: 110101********0001")
        };
        
        foreach (var (title, content) in testWindows)
        {
            // 创建测试窗口
            await _agent.CreateTestWindow(title, content);
            
            // 触发捕获
            await _agent.TriggerCapture();
            
            // 验证被拦截
            var lastCapture = await _agent.GetLastCapture();
            Assert.True(lastCapture.PrivacyBlocked,
                $"Sensitive content in '{title}' was not blocked");
        }
    }
    
    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Category", "ZeroBug")]
    public async Task ResourceUsage_StaysWithinLimits()
    {
        var metrics = new List<MetricsSnapshot>();
        
        // 监控资源使用
        using (var monitor = _agent.StartMonitoring())
        {
            await _agent.StartService();
            
            // 运行10分钟
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                metrics.Add(monitor.GetSnapshot());
            }
        }
        
        // 分析资源使用
        var avgCpu = metrics.Average(m => m.CpuPercent);
        var maxMemory = metrics.Max(m => m.MemoryMB);
        var avgMemory = metrics.Average(m => m.MemoryMB);
        
        // 验证
        Assert.True(avgCpu < 1.0, $"Average CPU usage {avgCpu:F2}% exceeds 1%");
        Assert.True(maxMemory < 100, $"Max memory {maxMemory:F0}MB exceeds 100MB");
        Assert.True(avgMemory < 50, $"Average memory {avgMemory:F0}MB exceeds 50MB");
    }
    
    [Fact]
    [Trait("Category", "LongRunning")]
    [Trait("Duration", "7Days")]
    public async Task SevenDay_Stability_Test()
    {
        var testStart = DateTime.Now;
        
        await _agent.StartService();
        
        // 持续监控7天
        while (DateTime.Now - testStart < TimeSpan.FromDays(7))
        {
            // 每5秒检查一次
            await Task.Delay(TimeSpan.FromSeconds(5));
            
            // 验证进程存活
            Assert.False(_agent.Process.HasExited, 
                "Process crashed during long-running test");
            
            // 收集指标
            var metrics = await _agent.GetMetrics();
            _orchestrator.ReportMetrics(metrics);
        }
        
        // 最终验证
        var finalMetrics = await _agent.GetTestSummary();
        Assert.Equal(0, finalMetrics.CrashCount);
        Assert.Equal(0, finalMetrics.ErrorCount);
        Assert.True(finalMetrics.UptimePercentage > 99.99);
    }
}
```

---

## 5. 测试执行流程

### 5.1 CI/CD 集成

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
      - main
      - develop

stages:
- stage: UnitTests
  displayName: '单元测试'
  jobs:
  - job: RunUnitTests
    pool:
      vmImage: 'windows-latest'
    steps:
    - task: DotNetCoreCLI@2
      inputs:
        command: 'test'
        projects: '**/*Tests.csproj'
        arguments: '--configuration Release --collect:"XPlat Code Coverage"'
    - task: PublishCodeCoverageResults@1
      inputs:
        codeCoverageTool: 'Cobertura'
        summaryFileLocation: '$(Agent.TempDirectory)/**/coverage.cobertura.xml'
        failIfCoverageEmpty: true
        threshold: 95

- stage: IntegrationTests
  displayName: '集成测试'
  dependsOn: UnitTests
  jobs:
  - job: RunIntegrationTests
    pool:
      name: 'TestPool'  # 专用测试机池
    steps:
    - script: |
        dotnet test tests/Screen2MD.IntegrationTests \
          --filter "Category=Integration" \
          --logger trx
      displayName: 'Run Integration Tests'

- stage: ZeroBugTests
  displayName: '零Bug测试'
  dependsOn: IntegrationTests
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
  jobs:
  - job: StabilityTest
    timeoutInMinutes: 10080  # 7天
    pool:
      name: 'LongRunningTestPool'
    steps:
    - script: |
        dotnet test tests/Screen2MD.ZeroBugTests \
          --filter "Category=ZeroBug" \
          --logger trx
      displayName: '7x24 Stability Test'
```

### 5.2 测试报告

```csharp
public class TestReportGenerator
{
    public void GenerateReport(TestRunResult result)
    {
        var report = new TestReport
        {
            Summary = new ReportSummary
            {
                TotalTests = result.Total,
                Passed = result.Passed,
                Failed = result.Failed,
                Skipped = result.Skipped,
                Duration = result.Duration,
                ZeroBugViolations = result.ZeroBugViolations
            },
            
            Coverage = new CoverageReport
            {
                LineCoverage = result.Coverage.LinePercent,
                BranchCoverage = result.Coverage.BranchPercent,
                MethodCoverage = result.Coverage.MethodPercent
            },
            
            Performance = new PerformanceReport
            {
                AvgCpuUsage = result.Metrics.AvgCpu,
                MaxMemoryUsage = result.Metrics.MaxMemory,
                AvgCaptureLatency = result.Metrics.AvgLatency
            },
            
            Failures = result.Failures.Select(f => new FailureDetail
            {
                TestName = f.TestName,
                ErrorMessage = f.ErrorMessage,
                StackTrace = f.StackTrace,
                Screenshot = f.ScreenshotPath
            }).ToList()
        };
        
        // 生成HTML报告
        var html = GenerateHtml(report);
        File.WriteAllText("test-report.html", html);
        
        // 上传到测试服务器
        UploadToTestServer(html);
    }
}
```

---

## 6. 测试数据管理

### 6.1 测试数据集

```
tests/
├── data/
│   ├── screenshots/           # 测试截图
│   │   ├── standard/          # 标准控件窗口
│   │   ├── custom/            # 自绘控件窗口
│   │   ├── games/             # 游戏界面
│   │   └── edge_cases/        # 极限情况
│   ├── expected_outputs/      # 预期输出
│   └── golden_files/          # 基准文件
└── fixtures/                  # 测试夹具
```

### 6.2 测试窗口生成器

```csharp
public class TestWindowGenerator
{
    /// <summary>
    /// 生成包含特定内容的测试窗口
    /// </summary>
    public WindowHandle CreateTestWindow(string title, string content)
    {
        var window = new TestWindow
        {
            Title = title,
            Content = content,
            Size = new Size(800, 600)
        };
        
        // 如果是敏感内容，添加标记
        if (IsSensitiveContent(content))
        {
            window.Tags.Add("sensitive");
        }
        
        return window.Show();
    }
    
    /// <summary>
    /// 模拟各种软件界面
    /// </summary>
    public WindowHandle SimulateSoftware(SoftwareType type)
    {
        return type switch
        {
            SoftwareType.CommandLine => SimulateCommandLine(),
            SoftwareType.EmailClient => SimulateEmailClient(),
            SoftwareType.InstantMessage => SimulateIMWindow(),
            SoftwareType.VideoConference => SimulateVideoConference(),
            SoftwareType.WebBrowser => SimulateBrowser(),
            _ => throw new NotSupportedException($"Software type {type} not supported")
        };
    }
}
```

---

## 7. 性能基准

### 7.1 基准测试套件

```csharp
[MemoryDiagnoser]
[RankColumn]
public class CaptureBenchmarks
{
    private CaptureEngine _engine;
    private Bitmap _testScreen;
    
    [GlobalSetup]
    public void Setup()
    {
        _engine = new CaptureEngine();
        _testScreen = LoadTestScreen("complex_ui.png");
    }
    
    [Benchmark(Baseline = true)]
    public CaptureResult Baseline_Capture()
    {
        return _engine.Capture(_testScreen);
    }
    
    [Benchmark]
    public async Task<CaptureResult> Capture_WithOCR()
    {
        return await _engine.CaptureWithOCRAsync(_testScreen);
    }
    
    [Benchmark]
    public async Task<CaptureResult> Capture_FullPipeline()
    {
        return await _engine.CaptureFullAsync(_testScreen);
    }
}
```

### 7.2 性能回归检测

```csharp
public class PerformanceRegressionDetector
{
    public void CompareWithBaseline(BenchmarkResult current, BenchmarkResult baseline)
    {
        foreach (var method in current.Methods)
        {
            var baselineMethod = baseline.GetMethod(method.Name);
            if (baselineMethod == null) continue;
            
            var regression = (method.Mean - baselineMethod.Mean) / baselineMethod.Mean;
            
            if (regression > 0.20)  // 20%退化
            {
                throw new PerformanceRegressionException(
                    $"Method {method.Name} regressed by {regression:P2}. " +
                    $"Baseline: {baselineMethod.Mean:F2}ms, " +
                    $"Current: {method.Mean:F2}ms");
            }
        }
    }
}
```

---

**文档版本**: v1.0.0  
**测试框架**: Screen2MD.TestFramework  
**维护团队**: QA Team  
**日期**: 2026-03-05
