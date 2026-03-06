# 配置参考手册

## 配置文件位置

| 平台 | 路径 |
|------|------|
| Linux | `~/.config/Screen2MD/config.json` |
| Windows | `%APPDATA%\Screen2MD\config.json` |
| macOS | `~/Library/Application Support/Screen2MD/config.json` |
| Docker | `/data/config/config.json` |

## 完整配置示例

```json
{
  "capture": {
    "intervalSeconds": 30,
    "outputDirectory": "~/Screen2MD/Captures",
    "formats": ["png"],
    "enableOcr": true,
    "similarityThreshold": 0.95,
    "manualMode": false,
    "captureAllDisplays": true,
    "primaryDisplayOnly": false
  },
  "ocr": {
    "languages": ["chi_sim", "eng"],
    "timeoutSeconds": 30,
    "dpi": 300,
    "psm": 3,
    "parallelism": 2
  },
  "storage": {
    "maxStorageGB": 10,
    "autoCleanupDays": 30,
    "cleanupEnabled": true,
    "cleanupStrategy": "Days",
    "keepCount": 1000
  },
  "search": {
    "indexPath": "~/Screen2MD/Index",
    "enableFullText": true,
    "cacheSize": 100
  },
  "privacy": {
    "enablePrivacyFilter": false,
    "blurPasswordFields": false,
    "sensitivePatterns": ["credit_card", "id_number", "email"]
  },
  "display": {
    "captureAllDisplays": true,
    "primaryDisplayOnly": false,
    "excludeDisplays": []
  },
  "scheduler": {
    "minIntervalMs": 1000,
    "maxIntervalMs": 3600000,
    "enableAdaptiveSampling": true,
    "cpuThreshold": 80,
    "memoryThresholdMB": 1024
  },
  "log": {
    "level": "Information",
    "outputToConsole": true,
    "outputToFile": true,
    "logDirectory": "~/Screen2MD/Logs",
    "maxFileSizeMB": 100,
    "maxFiles": 10
  },
  "web": {
    "enabled": true,
    "port": 8080,
    "bindAddress": "127.0.0.1",
    "enableCors": false,
    "allowedOrigins": []
  },
  "security": {
    "enableEncryption": true,
    "keyDerivationIterations": 100000,
    "autoUpdate": true
  }
}
```

## 配置项详解

### Capture (截图配置)

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| intervalSeconds | int | 30 | 自动截图间隔（秒） |
| outputDirectory | string | ~/Captures | 截图保存目录 |
| formats | array | ["png"] | 保存格式（png, jpg, bmp） |
| enableOcr | bool | true | 启用OCR识别 |
| similarityThreshold | float | 0.95 | 变化检测阈值（0-1） |
| manualMode | bool | false | 仅手动模式 |
| captureAllDisplays | bool | true | 捕获所有显示器 |
| primaryDisplayOnly | bool | false | 仅主显示器 |

### OCR (OCR配置)

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| languages | array | ["chi_sim", "eng"] | 识别语言 |
| timeoutSeconds | int | 30 | OCR超时时间 |
| dpi | int | 300 | 图像DPI |
| psm | int | 3 | 页面分割模式 |
| parallelism | int | 2 | 并行OCR线程数 |

**语言代码**:
- `eng` - 英文
- `chi_sim` - 简体中文
- `chi_tra` - 繁体中文
- `jpn` - 日文
- `kor` - 韩文
- `fra` - 法文
- `deu` - 德文

### Storage (存储配置)

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| maxStorageGB | int | 10 | 最大存储空间(GB) |
| autoCleanupDays | int | 30 | 自动清理天数 |
| cleanupEnabled | bool | true | 启用自动清理 |
| cleanupStrategy | string | "Days" | 清理策略（Days/Count） |
| keepCount | int | 1000 | Count策略保留数量 |

### Log (日志配置)

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| level | string | "Information" | 日志级别 |
| outputToConsole | bool | true | 输出到控制台 |
| outputToFile | bool | true | 输出到文件 |
| logDirectory | string | ~/Logs | 日志目录 |
| maxFileSizeMB | int | 100 | 单个日志文件大小 |
| maxFiles | int | 10 | 保留日志文件数 |

**日志级别**: `Debug` < `Information` < `Warning` < `Error` < `Critical`

### Web (Web界面配置)

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| enabled | bool | true | 启用Web界面 |
| port | int | 8080 | 监听端口 |
| bindAddress | string | "127.0.0.1" | 绑定地址 |
| enableCors | bool | false | 启用CORS |
| allowedOrigins | array | [] | 允许的跨域来源 |

## 环境变量配置

所有配置项都可通过环境变量设置，优先级高于配置文件。

**命名规则**: `Screen2MD__Section__Key`

```bash
# Linux/macOS
export Screen2MD__Capture__IntervalSeconds=60
export Screen2MD__Storage__MaxStorageGB=20
export Screen2MD__Log__Level=Debug

# Windows
set Screen2MD__Capture__IntervalSeconds=60
set Screen2MD__Storage__MaxStorageGB=20
```

## 配置热加载

修改配置后，发送信号重新加载：

```bash
# Linux
sudo systemctl kill -s HUP screen2md

# 或使用命令
screen2md config reload
```

## 配置验证

```bash
# 验证配置文件语法
screen2md config validate

# 测试配置效果
screen2md config test
```

## 配置模板

### 高性能配置

```json
{
  "capture": {
    "intervalSeconds": 10,
    "similarityThreshold": 0.90
  },
  "ocr": {
    "parallelism": 4,
    "timeoutSeconds": 10
  },
  "scheduler": {
    "enableAdaptiveSampling": false
  }
}
```

### 省电配置

```json
{
  "capture": {
    "intervalSeconds": 300,
    "enableOcr": false
  },
  "scheduler": {
    "enableAdaptiveSampling": true,
    "cpuThreshold": 50
  }
}
```

### 隐私增强配置

```json
{
  "privacy": {
    "enablePrivacyFilter": true,
    "blurPasswordFields": true,
    "sensitivePatterns": ["credit_card", "id_number", "email", "phone"]
  },
  "security": {
    "enableEncryption": true
  }
}
```
