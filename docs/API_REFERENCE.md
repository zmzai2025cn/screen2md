# Screen2MD Enterprise - API 参考文档

## 概述

Screen2MD Enterprise 提供多种API接口供开发者集成：
- **命令行接口 (CLI)**: 适合脚本和自动化
- **C# SDK**: 适合.NET应用集成
- **HTTP REST API**: 适合跨语言集成（Web界面使用）

---

## 命令行接口 (CLI)

### 全局选项

```bash
screen2md [global-options] <command> [command-options]

Global Options:
  -v, --verbose          详细输出
  -q, --quiet            静默模式
  --config <path>        指定配置文件路径
  --version              显示版本
  --help                 显示帮助
```

### 核心命令

#### capture - 立即截图

```bash
screen2md capture [options]

Options:
  -o, --output <path>    输出目录
  -f, --format <format>  格式: png, jpg, bmp (默认: png)
  -q, --quality <0-100>  质量 (仅jpg)
  -d, --display <index>  指定显示器 (默认: 主显示器)
  --region <x,y,w,h>     区域截图
  --no-ocr               禁用OCR
```

**示例**:
```bash
# 立即截图并OCR
screen2md capture

# 保存为JPG，质量90
screen2md capture -f jpg -q 90

# 截图指定区域
screen2md capture --region 100,100,800,600
```

#### start/stop - 服务控制

```bash
# 启动后台服务
screen2md start [--daemon]

# 停止服务
screen2md stop

# 重启服务
screen2md restart

# 查看状态
screen2md status
```

#### search - 搜索截图

```bash
screen2md search <query> [options]

Options:
  --start <date>         开始日期 (YYYY-MM-DD)
  --end <date>           结束日期
  --process <name>       过滤进程名
  --format <format>      输出: json, table (默认: table)
  -l, --limit <n>        最大结果数
```

**示例**:
```bash
# 搜索包含"meeting"的截图
screen2md search "meeting"

# 搜索特定日期范围
screen2md search "project" --start 2024-01-01 --end 2024-03-01

# JSON格式输出
screen2md search "error" --format json
```

#### config - 配置管理

```bash
screen2md config [command]

Commands:
  get <key>              获取配置值
  set <key> <value>      设置配置值
  list                   列出所有配置
  reset                  重置为默认配置
  validate               验证配置有效性
```

**示例**:
```bash
# 查看当前配置
screen2md config list

# 修改截图间隔
screen2md config set capture.intervalSeconds 60

# 验证配置
screen2md config validate
```

#### export/import - 数据迁移

```bash
# 导出数据
screen2md export --start <date> --end <date> --output <file.zip>

# 导入数据
screen2md import <file.zip>

# 备份
screen2md backup --output <backup.zip>

# 恢复
screen2md restore <backup.zip>
```

#### index - 索引管理

```bash
screen2md index [command]

Commands:
  rebuild                重建搜索索引
  optimize               优化索引性能
  stats                  显示索引统计
  cleanup                清理无效索引
```

---

## C# SDK

### 安装

```bash
dotnet add package Screen2MD.SDK
```

### 快速开始

```csharp
using Screen2MD.SDK;

// 创建客户端
var client = new Screen2MDClient(new Screen2MDOptions
{
    ConfigPath = "/path/to/config.json"
});

// 初始化
await client.InitializeAsync();

// 立即截图
var result = await client.CaptureAsync(new CaptureOptions
{
    OutputDirectory = "/path/to/output",
    EnableOcr = true,
    Format = ImageFormat.Png
});

Console.WriteLine($"Captured: {result.FilePath}");
Console.WriteLine($"OCR Text: {result.OcrResult.Text}");
```

### 核心类

#### Screen2MDClient

主客户端类，提供所有功能的访问。

```csharp
public class Screen2MDClient : IDisposable
{
    // 构造函数
    public Screen2MDClient(Screen2MDOptions options);
    
    // 初始化
    public Task InitializeAsync(CancellationToken ct = default);
    
    // 截图
    public Task<CaptureResult> CaptureAsync(CaptureOptions options);
    
    // 搜索
    public Task<SearchResult> SearchAsync(SearchQuery query);
    
    // 服务控制
    public Task StartAsync();
    public Task StopAsync();
    
    // 事件
    public event EventHandler<CaptureEventArgs> OnCapture;
    public event EventHandler<ErrorEventArgs> OnError;
}
```

#### CaptureOptions

```csharp
public class CaptureOptions
{
    public string OutputDirectory { get; set; }
    public ImageFormat Format { get; set; } = ImageFormat.Png;
    public int Quality { get; set; } = 95;
    public int DisplayIndex { get; set; } = 0;
    public Rectangle? Region { get; set; }
    public bool EnableOcr { get; set; } = true;
    public bool DetectChanges { get; set; } = true;
    public double SimilarityThreshold { get; set; } = 0.95;
}
```

#### SearchQuery

```csharp
public class SearchQuery
{
    public string Keywords { get; set; }
    public string ProcessName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool ExactMatch { get; set; }
    public int PageSize { get; set; } = 20;
    public int PageNumber { get; set; } = 1;
}
```

### 事件订阅

```csharp
var client = new Screen2MDClient(options);

// 订阅截图事件
client.OnCapture += (sender, e) =>
{
    Console.WriteLine($"Screenshot saved: {e.FilePath}");
    Console.WriteLine($"Text found: {e.OcrResult?.Text}");
};

// 订阅错误事件
client.OnError += (sender, e) =>
{
    Console.WriteLine($"Error: {e.Exception.Message}");
};

await client.StartAsync();
```

### 高级用法

#### 批量处理

```csharp
// 批量截图
var results = await client.CaptureBatchAsync(
    Enumerable.Range(0, 10).Select(i => new CaptureOptions
    {
        DisplayIndex = i % 2  // 交替捕获两个显示器
    }));

// 批量OCR
foreach (var result in results)
{
    if (result.OcrResult?.Confidence > 0.9)
    {
        Console.WriteLine($"High confidence: {result.OcrResult.Text}");
    }
}
```

#### 自定义处理器

```csharp
public class CustomCaptureHandler : ICaptureHandler
{
    public async Task HandleAsync(CaptureResult result)
    {
        // 上传到云存储
        await UploadToCloudAsync(result.FilePath);
        
        // 发送通知
        if (result.OcrResult?.Text.Contains("ERROR") == true)
        {
            await SendAlertAsync("Error detected in screenshot");
        }
    }
}

client.AddHandler(new CustomCaptureHandler());
```

---

## HTTP REST API

### 基础信息

- **Base URL**: `http://localhost:8080/api/v1`
- **Content-Type**: `application/json`
- **Authentication**: Bearer Token (可选)

### 端点列表

#### GET /health
健康检查

**Response**:
```json
{
  "status": "Healthy",
  "timestamp": "2024-03-07T10:00:00Z",
  "checks": [...]
}
```

#### POST /capture
立即截图

**Request**:
```json
{
  "format": "png",
  "quality": 95,
  "enableOcr": true,
  "region": {"x": 0, "y": 0, "width": 1920, "height": 1080}
}
```

**Response**:
```json
{
  "success": true,
  "filePath": "/data/captures/2024-03-07/100000.png",
  "timestamp": "2024-03-07T10:00:00Z",
  "ocrResult": {
    "text": "extracted text...",
    "confidence": 0.95
  }
}
```

#### GET /search
搜索截图

**Query Parameters**:
- `q`: 搜索关键词
- `start`: 开始日期 (ISO 8601)
- `end`: 结束日期
- `process`: 进程名过滤
- `page`: 页码
- `size`: 每页大小

**Response**:
```json
{
  "items": [
    {
      "id": "doc-123",
      "filePath": "/data/captures/...",
      "timestamp": "2024-03-07T10:00:00Z",
      "title": "Document",
      "processName": "chrome.exe",
      "matchScore": 0.95
    }
  ],
  "totalCount": 100,
  "pageNumber": 1,
  "pageSize": 20
}
```

#### GET /stats
统计信息

**Response**:
```json
{
  "totalCaptures": 1523,
  "capturesToday": 45,
  "storageUsedGB": 2.5,
  "indexedDocuments": 1523,
  "lastCaptureAt": "2024-03-07T09:55:00Z"
}
```

#### GET /config
获取配置

**Response**:
```json
{
  "capture": {
    "intervalSeconds": 30,
    "outputDirectory": "/data/captures",
    "enableOcr": true
  },
  "ocr": {
    "languages": ["chi_sim", "eng"]
  }
}
```

#### POST /config
更新配置

**Request**:
```json
{
  "capture.intervalSeconds": 60
}
```

---

## 错误处理

### 错误格式

```json
{
  "error": {
    "code": "CAPTURE_FAILED",
    "message": "Failed to capture screen",
    "details": "Display 0 not found",
    "timestamp": "2024-03-07T10:00:00Z"
  }
}
```

### 错误码

| 错误码 | HTTP状态 | 说明 |
|--------|---------|------|
| INVALID_REQUEST | 400 | 请求参数错误 |
| UNAUTHORIZED | 401 | 未授权 |
| NOT_FOUND | 404 | 资源不存在 |
| CAPTURE_FAILED | 500 | 截图失败 |
| OCR_FAILED | 500 | OCR识别失败 |
| STORAGE_FULL | 507 | 存储空间不足 |

---

## 示例代码

### Python

```python
import requests

# 截图
response = requests.post('http://localhost:8080/api/v1/capture', json={
    'format': 'png',
    'enableOcr': True
})
result = response.json()
print(f"Saved to: {result['filePath']}")

# 搜索
response = requests.get('http://localhost:8080/api/v1/search', params={
    'q': 'meeting',
    'page': 1,
    'size': 10
})
results = response.json()
for item in results['items']:
    print(f"{item['timestamp']}: {item['title']}")
```

### JavaScript/Node.js

```javascript
const axios = require('axios');

// 截图
const { data } = await axios.post('http://localhost:8080/api/v1/capture', {
  format: 'png',
  enableOcr: true
});
console.log('Captured:', data.filePath);

// 搜索
const { data: searchResult } = await axios.get(
  'http://localhost:8080/api/v1/search',
  { params: { q: 'error', page: 1 } }
);
console.log(`Found ${searchResult.totalCount} results`);
```

### PowerShell

```powershell
# 截图
$result = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/capture" -Method POST -ContentType "application/json" -Body '{"format":"png"}'
Write-Host "Captured: $($result.filePath)"

# 搜索
$results = Invoke-RestMethod -Uri "http://localhost:8080/api/v1/search?q=meeting"
$results.items | ForEach-Object { Write-Host "$($_.timestamp): $($_.title)" }
```

---

## 版本历史

| API版本 | 产品版本 | 说明 |
|---------|---------|------|
| v1 | 3.0.0 | 初始API版本 |

---

## 支持

- API文档: https://docs.screen2md.ai/api
- 问题反馈: https://github.com/screen2md/issues
- 邮件: api@screen2md.ai
