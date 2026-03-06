# Screen2MD Enterprise - 部署运维手册

## 系统要求

### 最低配置

| 组件 | 要求 |
|------|------|
| CPU | 2核 |
| 内存 | 4GB RAM |
| 磁盘 | 10GB 可用空间 |
| 网络 | 可访问外网（用于OCR下载） |
| OS | Windows 10/11, Ubuntu 20.04+, macOS 12+ |

### 推荐配置

| 组件 | 要求 |
|------|------|
| CPU | 4核+ |
| 内存 | 8GB RAM |
| 磁盘 | 50GB SSD |
| GPU | 支持CUDA（可选，加速OCR） |

## 安装部署

### 方式1：二进制部署

#### Linux (systemd)

```bash
# 1. 创建用户
sudo useradd -r -s /bin/false screen2md

# 2. 安装二进制
sudo mkdir -p /opt/screen2md
sudo tar -xzf screen2md-linux-x64.tar.gz -C /opt/screen2md
sudo chown -R screen2md:screen2md /opt/screen2md

# 3. 创建数据目录
sudo mkdir -p /var/lib/screen2md/{captures,index,config}
sudo chown -R screen2md:screen2md /var/lib/screen2md

# 4. 创建systemd服务
sudo tee /etc/systemd/system/screen2md.service > /dev/null <<EOF
[Unit]
Description=Screen2MD Enterprise
After=network.target

[Service]
Type=simple
User=screen2md
Group=screen2md
WorkingDirectory=/opt/screen2md
ExecStart=/opt/screen2md/Screen2MD --daemon
Restart=always
RestartSec=10
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
EOF

# 5. 启动服务
sudo systemctl daemon-reload
sudo systemctl enable screen2md
sudo systemctl start screen2md

# 6. 验证状态
sudo systemctl status screen2md
sudo journalctl -u screen2md -f
```

#### Windows (Service)

```powershell
# 1. 解压到Program Files
Expand-Archive screen2md-win-x64.zip "C:\Program Files\Screen2MD"

# 2. 创建服务
New-LocalUser -Name "screen2md" -Password (ConvertTo-SecureString -String "YourPassword" -AsPlainText -Force) -PasswordNeverExpires

$serviceParams = @{
    Name = "Screen2MD"
    BinaryPathName = "C:\Program Files\Screen2MD\Screen2MD.exe --daemon"
    DisplayName = "Screen2MD Enterprise"
    StartupType = "Automatic"
    Credential = Get-Credential -UserName "screen2md" -Message "Enter password"
}
New-Service @serviceParams

# 3. 启动服务
Start-Service Screen2MD
Get-Service Screen2MD
```

### 方式2：Docker部署

```yaml
# docker-compose.production.yml
version: '3.8'

services:
  screen2md:
    image: screen2md/enterprise:latest
    container_name: screen2md-prod
    restart: unless-stopped
    user: "1000:1000"
    volumes:
      - /data/screen2md/captures:/data/captures
      - /data/screen2md/index:/data/index
      - /data/screen2md/config:/data/config
      - /data/screen2md/logs:/app/logs
    environment:
      - Screen2MD__Capture__IntervalSeconds=30
      - Screen2MD__Storage__MaxStorageGB=50
      - DOTNET_ENVIRONMENT=Production
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '1'
          memory: 2G
    healthcheck:
      test: ["CMD", "./Screen2MD", "--health-check"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

部署：

```bash
docker-compose -f docker-compose.production.yml up -d
```

### 方式3：Kubernetes部署

```yaml
# k8s-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: screen2md
  namespace: productivity
spec:
  replicas: 1
  selector:
    matchLabels:
      app: screen2md
  template:
    metadata:
      labels:
        app: screen2md
    spec:
      securityContext:
        runAsUser: 1000
        runAsGroup: 1000
        fsGroup: 1000
      containers:
      - name: screen2md
        image: screen2md/enterprise:latest
        resources:
          requests:
            memory: "2Gi"
            cpu: "1000m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
        volumeMounts:
        - name: data
          mountPath: /data
        - name: config
          mountPath: /data/config
        livenessProbe:
          exec:
            command:
            - ./Screen2MD
            - --health-check
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          exec:
            command:
            - ./Screen2MD
            - --ready-check
          initialDelaySeconds: 10
          periodSeconds: 10
      volumes:
      - name: data
        persistentVolumeClaim:
          claimName: screen2md-data
      - name: config
        configMap:
          name: screen2md-config
```

## 配置管理

### 环境变量配置

| 变量 | 说明 | 示例 |
|------|------|------|
| `DOTNET_ENVIRONMENT` | 运行环境 | Production |
| `Screen2MD__Capture__IntervalSeconds` | 截图间隔 | 30 |
| `Screen2MD__Capture__OutputDirectory` | 输出目录 | /data/captures |
| `Screen2MD__Storage__MaxStorageGB` | 最大存储 | 50 |
| `Screen2MD__Log__Level` | 日志级别 | Information |

### 配置文件路径

| 平台 | 路径 |
|------|------|
| Linux | `/var/lib/screen2md/config/config.json` |
| Windows | `%PROGRAMDATA%\Screen2MD\config.json` |
| Docker | `/data/config/config.json` |

### 配置热加载

```bash
# 修改配置后发送信号重载
# Linux
sudo systemctl kill -s HUP screen2md

# Docker
docker kill -s HUP screen2md-prod
```

## 监控与告警

### 日志收集

```bash
# 查看实时日志
# Linux (journald)
journalctl -u screen2md -f

# Docker
docker logs -f screen2md-prod

# 日志轮转配置
# /etc/logrotate.d/screen2md
/var/lib/screen2md/logs/*.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    notifempty
    create 0640 screen2md screen2md
    sharedscripts
    postrotate
        systemctl reload screen2md
    endscript
}
```

### Prometheus监控

```yaml
# prometheus.yml scrape config
scrape_configs:
  - job_name: 'screen2md'
    static_configs:
      - targets: ['localhost:8080']
    metrics_path: /metrics
```

### 关键指标告警

| 指标 | 阈值 | 级别 |
|------|------|------|
| screen2md_captures_total | < 1/min | Warning |
| screen2md_ocr_errors_total | > 10/hour | Critical |
| screen2md_storage_used_bytes | > 90% | Warning |
| process_memory_working_set_bytes | > 2GB | Critical |

## 备份与恢复

### 备份策略

```bash
#!/bin/bash
# backup.sh

BACKUP_DIR="/backup/screen2md/$(date +%Y%m%d)"
mkdir -p "$BACKUP_DIR"

# 停止服务（可选，保证一致性）
sudo systemctl stop screen2md

# 备份数据
tar -czf "$BACKUP_DIR/captures.tar.gz" /var/lib/screen2md/captures
tar -czf "$BACKUP_DIR/index.tar.gz" /var/lib/screen2md/index
cp /var/lib/screen2md/config/config.json "$BACKUP_DIR/"

# 启动服务
sudo systemctl start screen2md

# 清理旧备份（保留30天）
find /backup/screen2md -type d -mtime +30 -exec rm -rf {} \;
```

### 恢复流程

```bash
# 1. 停止服务
sudo systemctl stop screen2md

# 2. 恢复数据
sudo tar -xzf /backup/screen2md/20240307/captures.tar.gz -C /
sudo tar -xzf /backup/screen2md/20240307/index.tar.gz -C /
sudo cp /backup/screen2md/20240307/config.json /var/lib/screen2md/config/

# 3. 修复权限
sudo chown -R screen2md:screen2md /var/lib/screen2md

# 4. 重建索引（如有必要）
screen2md index rebuild

# 5. 启动服务
sudo systemctl start screen2md
```

## 故障排查

### 服务无法启动

```bash
# 检查日志
journalctl -u screen2md --since "1 hour ago"

# 检查配置有效性
screen2md --validate-config

# 检查权限
ls -la /var/lib/screen2md/

# 检查端口占用
netstat -tlnp | grep 8080
```

### 性能问题

```bash
# 查看资源使用
top -p $(pgrep Screen2MD)

# 生成性能分析
dotnet-trace collect -p $(pgrep Screen2MD)

# 内存分析
dotnet-dump collect -p $(pgrep Screen2MD)
dotnet-dump analyze /tmp/dump.dmp
```

### 常见问题

| 问题 | 原因 | 解决 |
|------|------|------|
| 截图失败 | 权限不足 | 检查目录权限 |
| OCR识别慢 | 语言包缺失 | 安装 tesseract-lang-chi-sim |
| 磁盘满 | 清理未运行 | 检查 cleanup 配置 |
| 内存泄漏 | 版本Bug | 升级到最新版本 |

## 升级流程

### 升级前检查

```bash
# 1. 检查当前版本
screen2md --version

# 2. 备份数据
./backup.sh

# 3. 查看升级说明
cat CHANGELOG.md
```

### 升级步骤

```bash
# 1. 下载新版本
wget https://github.com/screen2md/releases/latest/download/screen2md-linux-x64.tar.gz

# 2. 停止服务
sudo systemctl stop screen2md

# 3. 替换二进制
sudo tar -xzf screen2md-linux-x64.tar.gz -C /opt/screen2md

# 4. 运行迁移（如果需要）
screen2md migrate

# 5. 启动服务
sudo systemctl start screen2md

# 6. 验证
screen2md --version
screen2md health-check
```

## 安全加固

### 文件权限

```bash
# 数据目录权限
sudo chmod 750 /var/lib/screen2md
sudo chmod 640 /var/lib/screen2md/config/config.json

# 日志权限
sudo chmod 640 /var/lib/screen2md/logs/*.log
```

### 网络安全

```bash
# 防火墙配置（仅允许本地访问Web界面）
sudo ufw allow from 127.0.0.1 to any port 8080
sudo ufw deny 8080

# 或使用iptables
sudo iptables -A INPUT -p tcp --dport 8080 -s 127.0.0.1 -j ACCEPT
sudo iptables -A INPUT -p tcp --dport 8080 -j DROP
```

## 性能调优

### 系统参数

```bash
# /etc/sysctl.conf
# 增加文件句柄限制
fs.file-max = 65535

# 优化磁盘I/O
vm.swappiness = 10
vm.dirty_ratio = 40

# 应用
sudo sysctl -p
```

### 应用配置

```json
{
  "capture": {
    "intervalSeconds": 30,
    "batchSize": 5,
    "parallelism": 2
  },
  "ocr": {
    "timeoutSeconds": 30,
    "parallelism": 2,
    "enableGpu": true
  }
}
```

## 联系支持

- 文档：https://docs.screen2md.ai
- 问题反馈：https://github.com/screen2md/issues
- 企业支持：support@screen2md.ai
