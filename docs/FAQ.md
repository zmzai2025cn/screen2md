# Screen2MD Enterprise - 故障排查 FAQ

## 常见问题速查

### Q: 服务无法启动

**症状**: 运行 `screen2md start` 后没有反应或报错

**排查步骤**:

1. **检查端口占用**
```bash
# Linux/macOS
netstat -tlnp | grep 8080
lsof -i :8080

# Windows
netstat -ano | findstr :8080
```

2. **检查日志**
```bash
# Linux
journalctl -u screen2md --since "1 hour ago"

# Windows
Get-EventLog -LogName Application -Source Screen2MD

# 通用
cat ~/.local/share/Screen2MD/logs/app.log
```

3. **验证权限**
```bash
# 检查目录权限
ls -la /var/lib/screen2md/

# 修复权限（Linux）
sudo chown -R screen2md:screen2md /var/lib/screen2md
```

**解决方案**:
- 端口被占用：修改配置文件中的端口号
- 权限不足：使用 `sudo` 或以管理员运行
- 配置文件损坏：删除后重新初始化

---

### Q: 截图功能不工作

**症状**: 执行截图命令后没有文件生成

**排查步骤**:

1. **检查显示器连接**
```bash
screen2md diagnostics displays
```

2. **验证输出目录**
```bash
# 检查目录是否存在且可写
test -w /path/to/output && echo "Writable" || echo "Not writable"

# 检查磁盘空间
df -h /path/to/output
```

3. **查看截图日志**
```bash
grep -i "capture" ~/.local/share/Screen2MD/logs/app.log
```

**常见原因**:
- 显示器被锁定或处于睡眠状态
- 输出目录不存在或无写入权限
- 磁盘空间不足
- UAC阻止截图（Windows）

**解决方案**:
```bash
# 创建输出目录
mkdir -p ~/Screen2MD/Captures

# 检查权限
chmod 755 ~/Screen2MD/Captures

# Windows: 以管理员运行
# 右键 → 以管理员身份运行
```

---

### Q: OCR识别失败或识别率低

**症状**: OCR返回空文本或错误文本

**排查步骤**:

1. **检查Tesseract安装**
```bash
tesseract --version

# 验证语言包
tesseract --list-langs | grep chi_sim
```

2. **测试OCR**
```bash
# 手动测试
tesseract test.png stdout -l chi_sim+eng
```

3. **检查图像质量**
```bash
# 查看截图文件
file capture.png
identify capture.png  # ImageMagick
```

**常见原因**:
- Tesseract未安装或版本过低
- 语言包缺失
- 图像分辨率太低
- 图像模糊或对比度低

**解决方案**:
```bash
# Ubuntu/Debian
sudo apt-get install tesseract-ocr tesseract-ocr-chi-sim

# 更新索引
screen2md ocr --update-langs

# 调整截图质量
screen2md config set capture.quality 95
```

---

### Q: 搜索功能找不到结果

**症状**: 搜索关键词返回空结果

**排查步骤**:

1. **检查索引状态**
```bash
screen2md index stats
```

2. **验证文档已索引**
```bash
# 查看索引目录
ls -la ~/.local/share/Screen2MD/index/

# 检查Lucene索引
screen2md index verify
```

3. **重建索引**
```bash
screen2md index rebuild
```

**常见原因**:
- 索引文件损坏
- 权限问题导致无法写入索引
- 文档未被正确索引

---

### Q: 存储空间不足

**症状**: 收到磁盘空间警告或截图失败

**排查步骤**:

1. **检查存储使用**
```bash
screen2md stats

# 详细分析
du -sh ~/.local/share/Screen2MD/captures/*
```

2. **检查清理配置**
```bash
screen2md config get storage.autoCleanupDays
screen2md config get storage.maxStorageGB
```

**解决方案**:
```bash
# 手动清理旧文件
screen2md cleanup --days 7

# 调整存储限制
screen2md config set storage.maxStorageGB 50

# 立即执行清理
screen2md cleanup --force
```

---

### Q: 服务占用内存过高

**症状**: 内存使用持续增长，超过预期

**排查步骤**:

1. **查看内存使用**
```bash
# Linux
ps aux | grep screen2md

dotnet-counters monitor -p $(pgrep Screen2MD)
```

2. **生成内存转储**
```bash
dotnet-dump collect -p $(pgrep Screen2MD)
dotnet-dump analyze /tmp/dump.dmp
```

3. **检查泄漏模式**
```bash
# 查看GC统计
dotnet-counters monitor --counters System.Runtime
```

**常见原因**:
- 事件未取消订阅
- 大对象未释放
- 缓存无限增长

**解决方案**:
- 重启服务：`screen2md restart`
- 调整GC模式：`DOTNET_GCConserveMemory=1`
- 限制缓存大小：修改配置

---

### Q: 跨平台兼容性问题

**症状**: 在特定操作系统上功能异常

#### Linux

**问题**: 截图黑屏或权限错误

**解决方案**:
```bash
# 检查显示环境
echo $DISPLAY

# X11权限
xhost +local:

# Wayland兼容性
export SCREEN2MD_FORCE_X11=1
```

#### Windows

**问题**: UAC弹窗或权限不足

**解决方案**:
```powershell
# 以管理员运行
# 右键 → 以管理员身份运行

# 或修改UAC设置
# 控制面板 → 用户账户 → 更改UAC设置
```

#### macOS

**问题**: 屏幕录制权限被拒绝

**解决方案**:
```bash
# 系统偏好设置 → 安全性与隐私 → 屏幕录制
# 添加 Screen2MD

# 重置权限
tccutil reset ScreenCapture
```

---

### Q: 配置文件损坏

**症状**: 启动时报配置错误，或行为异常

**排查步骤**:

1. **验证配置格式**
```bash
screen2md config validate

# 或使用Python
python3 -m json.tool ~/.config/Screen2MD/config.json
```

2. **备份并重置**
```bash
# 备份
cp ~/.config/Screen2MD/config.json ~/.config/Screen2MD/config.json.backup

# 重置为默认
screen2md config reset

# 手动编辑后验证
screen2md config validate
```

---

### Q: 性能下降

**症状**: 截图或搜索变慢

**排查步骤**:

1. **性能诊断**
```bash
screen2md diagnostics performance

# 查看基准测试
dotnet test --filter "Category=Benchmark"
```

2. **检查资源竞争**
```bash
# 查看线程数
ps -eLf | grep screen2md | wc -l

# 检查磁盘IO
iostat -x 1
```

**优化建议**:
- 减少并发OCR线程数
- 优化索引（`screen2md index optimize`）
- 增加系统内存
- 使用SSD存储

---

### Q: 升级后数据丢失

**症状**: 升级后看不到之前的截图

**排查步骤**:

1. **检查数据目录**
```bash
# 查看旧数据位置
find ~ -name "Screen2MD" -type d 2>/dev/null

# 检查数据完整性
ls -la ~/.local/share/Screen2MD/captures/
```

2. **数据迁移**
```bash
# 如果有备份
screen2md restore backup-2024-03-01.zip

# 手动迁移
cp -r ~/old/Screen2MD/captures/* ~/.local/share/Screen2MD/captures/
screen2md index rebuild
```

---

### Q: 如何启用调试模式

**开启详细日志**:
```bash
# 临时调试
screen2md --verbose capture

# 持续调试模式
screen2md config set log.level Debug
screen2md restart

# 查看调试日志
tail -f ~/.local/share/Screen2MD/logs/debug.log
```

---

### Q: 如何报告Bug

**收集信息**:
```bash
# 生成诊断报告
screen2md diagnostics report > bug-report.txt

# 收集日志
tar -czf logs.tar.gz ~/.local/share/Screen2MD/logs/

# 收集配置（脱敏后）
screen2md config list > config.txt
```

**提交渠道**:
- GitHub Issues: https://github.com/screen2md/issues
- 邮件: support@screen2md.ai
- 社区: https://discord.gg/screen2md

---

## 快速诊断命令

```bash
# 完整系统检查
screen2md diagnostics full

# 检查所有组件状态
screen2md health-check

# 生成支持包
screen2md support-bundle --output support.zip
```

---

## 联系支持

如果以上方案无法解决问题：

1. 查阅完整文档: https://docs.screen2md.ai
2. 搜索已知问题: https://github.com/screen2md/issues
3. 邮件支持: support@screen2md.ai
4. 社区讨论: https://discord.gg/screen2md

**提供信息时请包含**:
- 操作系统和版本
- Screen2MD版本 (`screen2md --version`)
- 错误日志
- 复现步骤
