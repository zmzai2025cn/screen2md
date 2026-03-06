# Screen2MD Enterprise - 系统架构文档

## 架构概览

Screen2MD Enterprise 采用**分层架构**设计，遵循**SOLID原则**和**依赖倒置原则**。

```
┌─────────────────────────────────────────────────────────────┐
│                      Presentation Layer                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                  │
│  │   CLI    │  │  System  │  │   Web    │                  │
│  │  Tool    │  │   Tray   │  │   UI     │                  │
│  └──────────┘  └──────────┘  └──────────┘                  │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                      Application Layer                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │    Core      │  │   Services   │  │   Engines    │      │
│  │  Services    │  │   (Business) │  │  (Platform)  │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                      Domain Layer                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Abstractions │  │    Kernel    │  │   Contracts  │      │
│  │  (Interfaces)│  │  (Framework) │  │   (Models)   │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│                     Infrastructure Layer                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Windows    │  │   Common     │  │    Mock      │      │
│  │  Platform    │  │   (Skia)     │  │   (Tests)    │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

## 模块详解

### 1. Domain Layer (领域层)

#### Abstractions (抽象接口)
定义核心业务接口，无外部依赖。

```csharp
// 核心接口
public interface ICaptureService { }
public interface IOcrEngine { }
public interface ISearchService { }
public interface IStorageService { }
```

#### Kernel (内核框架)
提供基础框架能力：
- **生命周期管理**: `IKernelComponent`
- **配置管理**: `IConfigurationManager`
- **事件总线**: `IEventBus`
- **日志抽象**: `IKernelLogger`

#### Contracts (契约模型)
DTO和数据契约：
```csharp
public record CaptureResult { }
public record SearchQuery { }
public record StorageInfo { }
```

### 2. Application Layer (应用层)

#### Core Services
核心业务逻辑实现：

```
CaptureService
├── 截图调度
├── 文件命名
├── 变化检测
└── 结果聚合

OcrService
├── 引擎管理
├── 语言配置
├── 超时控制
└── 结果缓存

StorageService
├── 文件操作
├── 统计分析
├── 自动清理
└── 空间管理

ConfigurationService
├── 配置读写
├── 持久化
├── 变更通知
└── 默认值管理
```

#### Services (业务服务)
高级业务功能：

```
AutoCleanupService
├── 定时任务
├── 策略执行
└── 空间回收

CaptureScheduler
├── 自适应采样
├── 资源监控
└── 动态调节

FullTextSearchService (Obsolete)
└── 已被 LuceneSearchService 替代

LuceneSearchService
├── 索引管理
├── 查询解析
├── 结果排序
└── 增量更新

PrivacyFilterService
├── 敏感模式
├── 内容过滤
└── 脱敏处理
```

#### Engines (平台引擎)
平台特定实现：

```
ScreenCaptureEngine (Windows)
├── EnumDisplayMonitors
├── GetMonitorInfo
├── BitBlt/PrintWindow
└── DPI处理

OcrEngine (Tesseract)
├── 图像预处理
├── 多语言支持
├── 并行识别
└── 结果解析

ImageProcessor (Skia)
├── 格式转换
├── 压缩优化
├── 相似度计算
└── 内存管理
```

### 3. Infrastructure Layer (基础设施层)

#### Platform.Common
跨平台通用实现（SkiaSharp）：
- `SkiaImageProcessor`
- 图像格式转换
- 基础图像操作

#### Platform.Windows
Windows特定实现：
- `WindowsCaptureEngine`
- `WindowsWindowManager`
- Win32 API调用

#### Platform.Mock
测试模拟实现：
- `MockCaptureEngine`
- `MockOcrEngine`
- 用于单元测试

## 数据流

### 截图流程

```
1. Trigger (Timer/Manual)
   ↓
2. CaptureScheduler
   ↓
3. CaptureService
   ├── Check resources
   ├── Enumerate displays
   └── Parallel capture
   ↓
4. ScreenCaptureEngine (Platform)
   └── Win32 API calls
   ↓
5. ImageProcessor (Skia)
   ├── Format conversion
   └── Quality optimization
   ↓
6. OcrService (if enabled)
   ├── Tesseract OCR
   └── Text extraction
   ↓
7. StorageService
   ├── File save
   └── Directory organization
   ↓
8. LuceneSearchService
   └── Index document
   ↓
9. EventBus
   └── Notifications
```

### 搜索流程

```
1. User Query
   ↓
2. SearchQuery Parser
   ├── Keyword extraction
   ├── Filter parsing
   └── Time range
   ↓
3. LuceneSearchService
   ├── Query building
   ├── Index search
   └── Result ranking
   ↓
4. Result Aggregation
   ├── Pagination
   └── Metadata enrichment
   ↓
5. Response
```

## 设计模式

### 1. 依赖注入 (DI)

```csharp
// ServiceCollectionExtensions.cs
services.AddSingleton<ICaptureService, CaptureService>();
services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
services.AddSingleton<IStorageService, StorageService>();
```

### 2. 策略模式

```csharp
// 清理策略
public interface ICleanupStrategy
{
    Task<CleanupResult> ExecuteAsync();
}

public class CleanupByDaysStrategy : ICleanupStrategy { }
public class CleanupByCountStrategy : ICleanupStrategy { }
```

### 3. 观察者模式

```csharp
// EventBus实现
public interface IEventBus
{
    void Subscribe<TEvent>(Action<TEvent> handler);
    void Publish<TEvent>(TEvent event);
}

// 使用
_eventBus.Subscribe<CaptureCompletedEvent>(e => 
{
    _logger.LogInfo($"Captured: {e.FilePath}");
});
```

### 4. 工厂模式

```csharp
public interface IPlatformServiceFactory
{
    IScreenCaptureEngine CreateCaptureEngine();
    IWindowManager CreateWindowManager();
}

public class WindowsPlatformFactory : IPlatformServiceFactory { }
public class LinuxPlatformFactory : IPlatformServiceFactory { }
```

## 并发设计

### 线程模型

```
Main Thread
├── UI Updates
├── Configuration Changes
└── Service Lifecycle

Worker Threads (ThreadPool)
├── Capture Operations
├── OCR Processing
├── File I/O
└── Index Updates

Background Thread
├── Scheduled Tasks
├── Cleanup Jobs
└── Health Checks
```

### 同步机制

```csharp
// ReaderWriterLockSlim for config
private readonly ReaderWriterLockSlim _configLock = new();

// SemaphoreSlim for resource limiting
private readonly SemaphoreSlim _ocrSemaphore = new(4);

// ConcurrentDictionary for thread-safe cache
private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

// Lock for index writer (Lucene)
private readonly object _indexWriterLock = new();
```

## 配置系统

### 配置层级

```
1. 默认值 (Code)
   ↓
2. 配置文件 (config.json)
   ↓
3. 环境变量 (Environment)
   ↓
4. 运行时修改 (Runtime)
```

### 配置热加载

```csharp
// 文件系统监控
var watcher = new FileSystemWatcher(configDir);
watcher.Changed += (s, e) =>
{
    if (e.Name == "config.json")
    {
        ReloadConfiguration();
    }
};
```

## 错误处理策略

### 异常层级

```
1. 可恢复异常
   └── 重试3次后降级

2. 部分失败
   └── 记录失败项，继续处理其他

3. 致命错误
   └── 记录状态，优雅退出
```

### 熔断机制

```csharp
public class CircuitBreaker
{
    private int _failureCount;
    private DateTime _lastFailureTime;
    
    public bool IsOpen => 
        _failureCount >= 5 && 
        DateTime.Now - _lastFailureTime < TimeSpan.FromMinutes(1);
}
```

## 扩展点

### 1. 自定义OCR引擎

```csharp
public class CustomOcrEngine : IOcrEngine
{
    public Task<OcrResult> RecognizeAsync(ICapturedImage image)
    {
        // 自定义实现
    }
}

// 注册
services.AddSingleton<IOcrEngine, CustomOcrEngine>();
```

### 2. 自定义存储

```csharp
public class CloudStorageService : IStorageService
{
    // 实现云存储
}
```

### 3. 自定义处理器

```csharp
public class SlackNotificationHandler : ICaptureHandler
{
    public Task HandleAsync(CaptureResult result)
    {
        // 发送Slack通知
    }
}
```

## 技术栈

| 组件 | 技术 |
|------|------|
| 运行时 | .NET 8.0 |
| UI | WPF / Avalonia (跨平台) |
| 图像处理 | SkiaSharp |
| OCR | Tesseract |
| 搜索 | Lucene.NET |
| 配置 | JSON + 环境变量 |
| 日志 | Microsoft.Extensions.Logging |
| 测试 | xUnit + FluentAssertions |
| CI/CD | GitHub Actions |

## 部署架构

### 单机部署

```
┌─────────────────────────────────┐
│          User Machine           │
│  ┌─────────────────────────┐   │
│  │   Screen2MD Service     │   │
│  │   ├─ Capture Engine     │   │
│  │   ├─ OCR Engine         │   │
│  │   ├─ Search Index       │   │
│  │   └─ Web UI             │   │
│  └─────────────────────────┘   │
│           SQLite               │
│      (Local Storage)           │
└─────────────────────────────────┘
```

### 企业部署

```
┌─────────────────────────────────┐
│         Load Balancer           │
└─────────────────────────────────┘
              │
    ┌─────────┴─────────┐
    │                   │
┌───┴───┐           ┌───┴───┐
│ Node1 │           │ Node2 │
│ Screen│           │ Screen│
│ 2MD   │           │ 2MD   │
└───┬───┘           └───┬───┘
    │                   │
    └─────────┬─────────┘
              │
    ┌─────────┴─────────┐
    │  Shared Storage   │
    │  (NAS/S3)         │
    └───────────────────┘
```

## 未来演进

### v3.1 计划
- 插件系统
- Webhook支持
- 云端备份

### v4.0 愿景
- 微服务架构
- 云原生部署
- AI智能分类

---

## 参考文档

- [ADR-001: Lucene.NET替换SQLite FTS5](adr/001-lucene-replacement.md)
- [ADR-002: 跨平台架构设计](adr/002-cross-platform.md)
- [性能基准报告](PERFORMANCE_BASELINE_REPORT.md)
