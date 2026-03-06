# Screen2MD Enterprise v3.0

## 产品概述

Screen2MD Enterprise 是一款企业级智能截图管理解决方案，提供自动化屏幕捕获、OCR文字识别、全文检索和智能归档功能。

## 核心特性

- **智能截图**: 自动/手动截图，支持多显示器，智能变化检测
- **OCR识别**: 集成Tesseract引擎，支持中英文及多语言
- **全文搜索**: 基于Lucene.NET的高性能全文检索
- **自动归档**: 智能清理旧文件，节省存储空间
- **隐私保护**: 敏感信息自动过滤
- **跨平台**: 支持Windows、Linux、macOS

## 快速开始

### 安装

#### Windows
```powershell
# 下载最新版本
Invoke-WebRequest -Uri "https://github.com/screen2md/releases/latest/download/screen2md-win-x64.zip" -OutFile "screen2md.zip"
Expand-Archive -Path "screen2md.zip" -DestinationPath "C:\Program Files\Screen2MD"

# 添加到环境变量（可选）
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";C:\Program Files\Screen2MD", "Machine")
```

#### Linux
```bash
# 下载并解压
wget https://github.com/screen2md/releases/latest/download/screen2md-linux-x64.tar.gz
tar -xzf screen2md-linux-x64.tar.gz
sudo mv screen2md-linux-x64 /opt/screen2md
sudo ln -s /opt/screen2md/Screen2MD /usr/local/bin/screen2md
```

#### macOS
```bash
brew tap screen2md/tap
brew install screen2md
```

### 首次运行

```bash
# 初始化配置
screen2md --init

# 启动服务
screen2md --start

# 查看状态
screen2md --status
```

### 基础配置

编辑配置文件 `~/.config/Screen2MD/config.json`:

```json
{
  "capture": {
    "intervalSeconds": 30,
    "outputDirectory": "~/Screen2MD/Captures",
    "formats": ["png"],
    "enableOcr": true
  },
  "ocr": {
    "languages": ["chi_sim", "eng"],
    "timeoutSeconds": 30
  },
  "storage": {
    "maxStorageGB": 10,
    "autoCleanupDays": 30
  }
}
```

## 使用指南

### 命令行工具

```bash
# 截图（立即执行一次）
screen2md capture

# 启动后台服务
screen2md start --daemon

# 停止服务
screen2md stop

# 搜索历史截图
screen2md search "关键词"

# 查看统计
screen2md stats

# 导出数据
screen2md export --start 2024-01-01 --end 2024-03-01 --output backup.zip
```

### 系统托盘（Windows）

运行后会在系统托盘显示图标：
- 右键点击打开菜单
- 左键双击查看最近截图
- 支持快捷键 `Ctrl+Shift+S` 快速截图

### Web界面

访问 `http://localhost:8080` 打开Web管理界面：
- 浏览历史截图
- 全文搜索
- 配置管理
- 统计报表

## 配置详解

### 截图配置 (capture)

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| intervalSeconds | int | 30 | 自动截图间隔（秒） |
| outputDirectory | string | ~/Screen2MD/Captures | 截图保存目录 |
| formats | array | ["png"] | 保存格式：png, jpg, bmp |
| enableOcr | bool | true | 是否启用OCR |
| similarityThreshold | float | 0.95 | 变化检测阈值（0-1） |
| manualMode | bool | false | 仅手动模式 |

### OCR配置 (ocr)

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| languages | array | ["chi_sim", "eng"] | 识别语言 |
| timeoutSeconds | int | 30 | OCR超时时间 |
| dpi | int | 300 | 图像DPI |
| psm | int | 3 | 页面分割模式 |

### 存储配置 (storage)

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| maxStorageGB | int | 10 | 最大存储空间(GB) |
| autoCleanupDays | int | 30 | 自动清理天数 |
| cleanupEnabled | bool | true | 启用自动清理 |
| cleanupStrategy | string | "Days" | 策略：Days/Count |
| keepCount | int | 1000 | 按数量清理时保留数量 |

## 故障排查

### 常见问题

#### Q: 无法启动服务
```bash
# 检查端口占用
netstat -tlnp | grep 8080

# 检查权限
screen2md --check-permissions

# 查看日志
tail -f ~/.local/share/Screen2MD/logs/app.log
```

#### Q: OCR识别失败
- 确认Tesseract已安装并添加到PATH
- 检查语言包是否下载完整
- 查看 `ocr.log` 获取详细错误

#### Q: 存储空间不足
```bash
# 手动清理旧文件
screen2md cleanup --days 7

# 调整存储限制
screen2md config set storage.maxStorageGB 20
```

### 日志位置

| 平台 | 路径 |
|------|------|
| Windows | `%APPDATA%\Screen2MD\logs\` |
| Linux | `~/.local/share/Screen2MD/logs/` |
| macOS | `~/Library/Application Support/Screen2MD/logs/` |

## 隐私与安全

### 敏感信息过滤

系统会自动识别并过滤以下信息：
- 银行卡号（16位数字）
- 身份证号（18位）
- 密码字段（Password/PWD等关键词后内容）
- 邮箱地址（可选）

### 数据加密

- 配置文件使用AES-256加密存储敏感字段
- 本地索引文件支持加密（需手动开启）
- 网络传输使用TLS 1.3

## 支持与反馈

- 文档: https://docs.screen2md.ai
- 问题反馈: https://github.com/screen2md/issues
- 邮件支持: support@screen2md.ai

## 许可证

MIT License - 详见 LICENSE 文件
