using Markdig;
using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Web.Services;

/// <summary>
/// 文档服务 - Markdown 文档渲染
/// </summary>
public class DocumentationService : IKernelComponent
{
    private readonly MarkdownPipeline _markdownPipeline;
    private readonly Dictionary<string, string> _documents;

    public string Name => nameof(DocumentationService);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public DocumentationService()
    {
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        // 内置文档
        _documents = new Dictionary<string, string>
        {
            ["index"] = LoadIndexDocument(),
            ["quickstart"] = LoadQuickStartDocument(),
            ["architecture"] = LoadArchitectureDocument(),
            ["api"] = LoadApiDocument(),
            ["faq"] = LoadFaqDocument()
        };
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Healthy;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // 无需要释放的资源
    }

    /// <summary>
    /// 获取文档列表
    /// </summary>
    public List<DocItem> GetDocumentList()
    {
        return new List<DocItem>
        {
            new() { Id = "index", Title = "简介", Icon = "📖" },
            new() { Id = "quickstart", Title = "快速开始", Icon = "🚀" },
            new() { Id = "architecture", Title = "架构设计", Icon = "🏗️" },
            new() { Id = "api", Title = "API 文档", Icon = "📚" },
            new() { Id = "faq", Title = "常见问题", Icon = "❓" }
        };
    }

    /// <summary>
    /// 渲染 Markdown 为 HTML
    /// </summary>
    public string RenderMarkdown(string markdown)
    {
        return Markdown.ToHtml(markdown, _markdownPipeline);
    }

    /// <summary>
    /// 获取文档内容
    /// </summary>
    public string? GetDocument(string docId)
    {
        return _documents.TryGetValue(docId, out var content) ? content : null;
    }

    private string LoadIndexDocument()
    {
        return @"# Screen2MD Enterprise

## 企业级屏幕内容采集系统

Screen2MD Enterprise 是一个智能化的屏幕内容采集与分析系统，专为知识工作者设计。

### 核心特性

- 🚀 **零崩溃设计** - 7×24小时稳定运行
- ⚡ **极低资源占用** - CPU <1%, 内存 <50MB
- 🔒 **隐私保护** - 100%敏感信息拦截
- 🎯 **智能识别** - 代码/文档/会议自动分类
- 📊 **数据分析** - 完整的使用统计与报告

### 技术架构

```
┌─────────────┐
│    UI 层    │  WPF + Blazor Web
├─────────────┤
│  服务层     │  存储/调度/隐私
├─────────────┤
│  引擎层     │  OCR/OpenCV/UIA
├─────────────┤
│  内核层     │  进程/配置/日志
└─────────────┘
```

### 开始使用

1. 下载安装包
2. 运行安装程序
3. 配置采集规则
4. 开始智能采集
";
    }

    private string LoadQuickStartDocument()
    {
        return @"# 快速开始

## 安装

1. 从 [下载页面](/download) 获取最新版本
2. 运行安装程序 `Screen2MD-v2.0.0.exe`
3. 按照向导完成安装

## 配置

### 首次启动

1. 系统托盘会出现 Screen2MD 图标
2. 双击图标打开管理控制台
3. 在设置中配置：
   - 采集频率
   - 隐私规则
   - 上传设置

### 隐私规则

默认会拦截以下敏感内容：

- 密码输入框
- 银行网站
- 支付页面
- 私人聊天窗口

## 使用

### 暂停/恢复

- 右键点击托盘图标
- 选择「暂停采集」或「恢复采集」

### 查看记录

1. 打开 Web 控制台 (http://localhost:9999)
2. 浏览捕获的历史记录
3. 按软件类型筛选

### 数据导出

支持导出为：
- Markdown 文档
- JSON 数据
- 图片归档
";
    }

    private string LoadArchitectureDocument()
    {
        return @"# 架构设计

## 系统架构

### 内核层 (Kernel)

- **进程管理器** - 服务化运行
- **配置管理器** - JSON/YAML 配置，热重载
- **日志系统** - 结构化日志，自动轮转
- **异常处理** - 全局捕获，零崩溃
- **资源监控** - CPU/内存监控，<1%
- **事件总线** - 异步消息传递
- **插件系统** - DLL 热插拔

### 引擎层 (Engines)

- **变化检测引擎** - 像素/哈希/特征匹配
- **屏幕捕获引擎** - WinAPI 封装
- **UIA 引擎** - Windows UI 自动化
- **OpenCV 引擎** - 计算机视觉
- **OCR 引擎** - PaddleOCR 文字识别

### 服务层 (Services)

- **存储服务** - SQLite，1000 TPS
- **调度服务** - 自适应采样
- **隐私服务** - 敏感信息过滤

### 界面层 (UI)

- **系统托盘** - 图标/菜单
- **WPF 管理台** - 本地管理
- **Blazor Web** - 网页控制台
";
    }

    private string LoadApiDocument()
    {
        return @"# API 文档

## 存储服务 API

### SaveCaptureAsync

保存采集记录

```csharp
Task<string> SaveCaptureAsync(CaptureRecord record);
```

### QueryAsync

查询采集记录

```csharp
Task<QueryResult<CaptureRecord>> QueryAsync(CaptureQuery query);
```

## 调度器 API

### StartAsync

启动调度器

```csharp
Task StartAsync(CancellationToken ct = default);
```

### PauseAsync / ResumeAsync

暂停/恢复采集

```csharp
Task PauseAsync(CancellationToken ct = default);
Task ResumeAsync(CancellationToken ct = default);
```

## 引擎 API

### 变化检测

```csharp
Task<ChangeDetectionResult> DetectAsync(byte[] screen);
```

### OCR 识别

```csharp
Task<OCRResult> RecognizeAsync(byte[] image);
```
";
    }

    private string LoadFaqDocument()
    {
        return @"# 常见问题

## Q: 采集会占用多少资源？

**A:** 设计目标是 CPU <1%, 内存 <50MB。实际测试表明在空闲时 CPU 占用接近 0%。

## Q: 如何确保隐私安全？

**A:** 系统内置隐私过滤器，会自动识别并跳过：
- 密码输入框
- 银行/支付网站
- 私人聊天窗口

## Q: 支持哪些软件识别？

**A:** 支持识别 100+ 种软件：
- IDE: VS Code, Visual Studio, IntelliJ IDEA
- 浏览器: Chrome, Edge, Firefox
- IM: 微信, QQ, Slack, Teams
- 办公: Office, WPS

## Q: 数据存储在哪里？

**A:** 本地 SQLite 数据库存储，默认路径：
```
%LOCALAPPDATA%\Screen2MD\data.db
```

## Q: 如何升级到最新版本？

**A:** 
1. 从 Web 控制台下载新版本
2. 运行安装程序
3. 自动迁移旧数据
";
    }
}

/// <summary>
/// 文档项
/// </summary>
public record DocItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Icon { get; init; } = "📄";
}
