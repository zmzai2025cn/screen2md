# 从 v2.x 迁移到 v3.0 指南

## 迁移概述

Screen2MD Enterprise v3.0 是一次重大版本升级，包含架构重构、性能优化和新功能。

**迁移前请务必备份数据！**

## 破坏性变更

### 1. 配置格式变更

**v2.x (INI格式)**:
```ini
[Capture]
Interval=30
OutputDir=C:\Captures

[OCR]
Enabled=true
Languages=chi_sim,eng
```

**v3.0 (JSON格式)**:
```json
{
  "capture": {
    "intervalSeconds": 30,
    "outputDirectory": "C:\\Captures",
    "enableOcr": true
  },
  "ocr": {
    "languages": ["chi_sim", "eng"]
  }
}
```

### 2. 索引格式变更

v3.0 使用 Lucene.NET 替代 SQLite FTS5，**需要重建索引**。

### 3. API 变更

- CLI 命令重新设计
- 配置项名称变更
- 日志格式变更

## 自动迁移工具

### 使用迁移命令

```bash
# 自动检测并迁移
screen2md migrate --from 2.x --to 3.0 --dry-run

# 执行迁移
screen2md migrate --from 2.x --to 3.0

# 验证迁移结果
screen2md migrate --verify
```

### 迁移步骤详解

#### 步骤1: 备份数据

```bash
# 创建完整备份
screen2md backup --output v2-backup-$(date +%Y%m%d).zip

# 或手动备份
cp -r ~/.config/Screen2MD ~/Screen2MD-v2-backup
cp -r ~/.local/share/Screen2MD ~/Screen2MD-data-backup
```

#### 步骤2: 安装 v3.0

```bash
# Linux
wget https://github.com/screen2md/releases/v3.0.0/screen2md-linux-x64.tar.gz
tar -xzf screen2md-linux-x64.tar.gz
sudo mv screen2md-linux-x64 /opt/screen2md-v3

# Windows
# 下载并运行安装程序
```

#### 步骤3: 配置迁移

```bash
# 自动转换配置
/opt/screen2md-v3/Screen2MD migrate config \
  --source ~/.config/Screen2MD-v2/config.ini \
  --target ~/.config/Screen2MD/config.json

# 验证新配置
/opt/screen2md-v3/Screen2MD config validate
```

#### 步骤4: 数据迁移

```bash
# 迁移截图文件
# 文件位置不变，v3.0 自动识别

# 重建搜索索引（必须）
/opt/screen2md-v3/Screen2MD index rebuild

# 这可能需要一些时间，取决于数据量
# 进度会显示在控制台
```

#### 步骤5: 验证

```bash
# 基本功能测试
/opt/screen2md-v3/Screen2MD --version
/opt/screen2md-v3/Screen2MD capture --test
/opt/screen2md-v3/Screen2MD search "test"

# 性能测试
/opt/screen2md-v3/Screen2MD diagnostics performance
```

## 手动迁移（高级用户）

### 配置文件转换

```python
# convert-config.py
import configparser
import json

# 读取旧配置
config = configparser.ConfigParser()
config.read('config.ini')

# 转换为新格式
new_config = {
    "capture": {
        "intervalSeconds": int(config.get('Capture', 'Interval', fallback=30)),
        "outputDirectory": config.get('Capture', 'OutputDir', fallback='~/Captures'),
        "enableOcr": config.getboolean('OCR', 'Enabled', fallback=True)
    },
    "ocr": {
        "languages": config.get('OCR', 'Languages', fallback='chi_sim,eng').split(',')
    },
    "storage": {
        "maxStorageGB": int(config.get('Storage', 'MaxSize', fallback=10))
    }
}

# 写入新配置
with open('config.json', 'w') as f:
    json.dump(new_config, f, indent=2)
```

### 数据库迁移

```bash
# 导出v2数据
sqlite3 ~/.local/share/Screen2MD-v2/captures.db ".dump" > v2-export.sql

# v3.0使用Lucene索引，无需导入SQLite
# 但需要重新索引所有截图
```

## 配置项映射表

| v2.x 配置项 | v3.0 配置项 | 变更说明 |
|-------------|-------------|----------|
| `[Capture] Interval` | `capture.intervalSeconds` | 名称变更 |
| `[Capture] OutputDir` | `capture.outputDirectory` | 名称变更 |
| `[OCR] Enabled` | `capture.enableOcr` | 位置变更 |
| `[OCR] Languages` | `ocr.languages` | 格式变为数组 |
| `[Storage] MaxSize` | `storage.maxStorageGB` | 单位明确 |
| `[Storage] CleanupDays` | `storage.autoCleanupDays` | 名称变更 |
| `[Display] PrimaryOnly` | `display.primaryDisplayOnly` | 新增 |
| `[Advanced] ThreadCount` | *(已移除)* | 自动管理 |

## 常见问题

### Q: 迁移后索引为空

**原因**: v3.0 使用 Lucene 替代 SQLite FTS

**解决**:
```bash
# 重建索引
screen2md index rebuild

# 对于大量数据，可以后台执行
screen2md index rebuild --background
```

### Q: 配置迁移失败

**原因**: 旧配置包含无效值

**解决**:
```bash
# 重置为默认，再手动配置
screen2md config reset
screen2md config set capture.intervalSeconds 30
# ... 逐个设置
```

### Q: 启动后找不到旧截图

**原因**: 数据目录路径变更

**解决**:
```bash
# 检查数据目录
ls -la ~/.local/share/Screen2MD/captures/

# 如果数据在旧位置，迁移过来
cp -r ~/Screen2MD-v2-backup/captures/* ~/.local/share/Screen2MD/captures/

# 更新索引
screen2md index rebuild
```

### Q: 插件/脚本不兼容

**原因**: CLI 命令和输出格式变更

**解决**:
```bash
# 查看新命令格式
screen2md --help

# 更新脚本示例
# v2.x: screen2md capture --output /path
# v3.0: screen2md capture -o /path
```

## 回滚方案

如果迁移后遇到问题：

```bash
# 1. 停止v3.0
screen2md stop

# 2. 恢复v2.x配置
cp ~/Screen2MD-v2-backup/config.ini ~/.config/Screen2MD/

# 3. 启动v2.x（如果保留）
/opt/screen2md-v2/Screen2MD start

# 4. 报告问题
screen2md support-bundle --output rollback-issue.zip
```

## 新功能快速上手

### 新增功能一览

1. **Web UI**: 访问 http://localhost:8080
2. **全文搜索**: `screen2md search "关键词"`
3. **自动归档**: 配置 `storage.autoCleanupDays`
4. **性能监控**: `screen2md diagnostics performance`
5. **健康检查**: `screen2md health-check`

### 推荐新配置

```json
{
  "capture": {
    "intervalSeconds": 30,
    "similarityThreshold": 0.95,
    "detectChanges": true
  },
  "ocr": {
    "languages": ["chi_sim", "eng"],
    "timeoutSeconds": 30
  },
  "storage": {
    "maxStorageGB": 10,
    "cleanupStrategy": "Days",
    "autoCleanupDays": 30
  },
  "privacy": {
    "enablePrivacyFilter": true,
    "blurPasswordFields": true
  },
  "log": {
    "level": "Information",
    "outputToConsole": false
  }
}
```

## 支持

迁移遇到问题？

- 文档: https://docs.screen2md.ai/migration
- 社区: https://discord.gg/screen2md
- 邮件: migration@screen2md.ai

---

**迁移愉快！v3.0 带来更强大的功能和更好的性能！**
