# Screen2MD Enterprise - 网站与发布架构

**项目门户网站技术方案**

---

## 1. 网站架构

### 1.1 部署拓扑

```
                              用户
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                     百度云服务器 (106.13.179.14)                  │
│                          Port: 9999                              │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │                     Nginx (反向代理)                       │   │
│  │  - 静态文件服务                                            │   │
│  │  - 负载均衡                                                │   │
│  │  - SSL 终端 (可选)                                         │   │
│  └───────────────────────┬───────────────────────────────────┘   │
│                          │                                       │
│          ┌───────────────┼───────────────┐                       │
│          ▼               ▼               ▼                       │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐             │
│  │ 静态文档网站  │ │ 下载服务      │ │ API 服务      │             │
│  │ (MkDocs)     │ │ (Kestrel)    │ │ (Kestrel)    │             │
│  │ Port: 9990   │ │ Port: 9991   │ │ Port: 9992   │             │
│  └──────────────┘ └──────────────┘ └──────────────┘             │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │                   文件存储                                  │   │
│  │  /opt/screen2md-web/                                       │   │
│  │  ├── docs/          # 文档网站文件                          │   │
│  │  ├── downloads/     # 可执行文件                            │   │
│  │  │   ├── latest/    # 最新版本                              │   │
│  │  │   ├── v2.0.0/    # 历史版本                              │   │
│  │  │   └── beta/      # 测试版本                              │   │
│  │  └── api/           # API 相关                              │   │
│  └───────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 服务组件

| 服务 | 端口 | 技术 | 用途 |
|------|------|------|------|
| Nginx | 9999 | Nginx | 统一入口、静态文件、路由 |
| 文档站 | 9990 | MkDocs | 项目文档 |
| 下载站 | 9991 | Kestrel | 文件下载服务 |
| API服务 | 9992 | Kestrel | 版本检查、统计API |

---

## 2. 文档网站

### 2.1 技术选型

- **生成器**: MkDocs
- **主题**: Material for MkDocs
- **插件**:
  - `mkdocs-material` - 主题
  - `mkdocs-search-plugin` - 搜索
  - `mkdocs-minify-plugin` - 压缩
  - `mkdocs-redirects` - 重定向

### 2.2 目录结构

```
docs-site/
├── mkdocs.yml              # 站点配置
├── docs/
│   ├── index.md            # 首页
│   ├── getting-started.md  # 快速开始
│   ├── architecture/
│   │   ├── overview.md     # 架构概览
│   │   ├── kernel.md       # 内核设计
│   │   ├── engines.md      # 识别引擎
│   │   └── storage.md      # 存储设计
│   ├── deployment/
│   │   ├── installation.md # 安装指南
│   │   ├── configuration.md# 配置说明
│   │   └── upgrade.md      # 升级指南
│   ├── development/
│   │   ├── setup.md        # 开发环境
│   │   ├── contributing.md # 贡献指南
│   │   └── api.md          # API文档
│   └── changelog.md        # 更新日志
└── overrides/              # 主题覆盖
    └── partials/
        └── footer.html
```

### 2.3 MkDocs 配置

```yaml
# mkdocs.yml
site_name: Screen2MD Enterprise
site_description: 企业级屏幕内容智能采集系统
site_author: OpenClaw Team
site_url: http://106.13.179.14:9999

theme:
  name: material
  language: zh
  palette:
    - scheme: default
      primary: indigo
      accent: indigo
      toggle:
        icon: material/brightness-7
        name: 切换到深色模式
    - scheme: slate
      primary: indigo
      accent: indigo
      toggle:
        icon: material/brightness-4
        name: 切换到浅色模式
  features:
    - navigation.tabs
    - navigation.sections
    - navigation.expand
    - search.suggest
    - search.highlight
    - content.code.copy

plugins:
  - search:
      lang: zh
  - minify:
      minify_html: true
      minify_js: true
      minify_css: true

extra:
  version:
    provider: mike
  social:
    - icon: fontawesome/brands/github
      link: https://github.com/openclaw/screen2md-enterprise

nav:
  - 首页: index.md
  - 快速开始: getting-started.md
  - 架构设计:
    - 概览: architecture/overview.md
    - 内核: architecture/kernel.md
    - 引擎: architecture/engines.md
    - 存储: architecture/storage.md
  - 部署指南:
    - 安装: deployment/installation.md
    - 配置: deployment/configuration.md
    - 升级: deployment/upgrade.md
  - 开发文档:
    - 环境搭建: development/setup.md
    - 贡献指南: development/contributing.md
    - API参考: development/api.md
  - 更新日志: changelog.md
```

---

## 3. 下载服务

### 3.1 版本管理

```
/opt/screen2md-web/downloads/
├── latest/
│   ├── Screen2MD-Setup.exe          # 最新安装包
│   ├── Screen2MD-Portable.zip       # 便携版
│   └── version.json                 # 版本信息
│
├── v2.0.0/
│   ├── Screen2MD-2.0.0-Setup.exe
│   ├── Screen2MD-2.0.0-Portable.zip
│   └── checksums.sha256
│
├── v1.5.0/
│   ├── Screen2MD-1.5.0-Setup.exe
│   └── ...
│
├── beta/
│   └── Screen2MD-2.1.0-beta.exe
│
└── versions.json                    # 所有版本索引
```

### 3.2 版本信息格式

```json
// latest/version.json
{
  "version": "2.0.0",
  "releaseDate": "2026-04-23",
  "downloadUrl": "/downloads/latest/Screen2MD-Setup.exe",
  "checksum": {
    "sha256": "a1b2c3d4e5f6..."
  },
  "releaseNotes": "/changelog/#v200",
  "minSystemRequirements": {
    "os": "Windows 10 1809+",
    "ram": "2GB",
    "disk": "100MB"
  }
}

// versions.json
{
  "versions": [
    {
      "version": "2.0.0",
      "date": "2026-04-23",
      "stable": true,
      "url": "/downloads/v2.0.0/"
    },
    {
      "version": "1.5.0",
      "date": "2026-04-09",
      "stable": true,
      "url": "/downloads/v1.5.0/"
    }
  ],
  "latest": {
    "stable": "2.0.0",
    "beta": "2.1.0-beta"
  }
}
```

### 3.3 下载服务代码

```csharp
// DownloadService.cs
public class DownloadService : BackgroundService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DownloadService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        
        builder.WebHost.UseUrls("http://0.0.0.0:9991");
        
        var app = builder.Build();
        
        // 启用静态文件
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(_env.ContentRootPath, "downloads")),
            RequestPath = "/downloads",
            ServeUnknownFileTypes = false,
            OnPrepareResponse = ctx =>
            {
                // 添加下载头
                ctx.Context.Response.Headers.Add(
                    "Content-Disposition", 
                    $"attachment; filename=\"{Path.GetFileName(ctx.File.Name)}\"");
                
                // 启用断点续传
                ctx.Context.Response.Headers.Add("Accept-Ranges", "bytes");
            }
        });
        
        // API: 获取最新版本
        app.MapGet("/api/version/latest", () =>
        {
            var versionFile = Path.Combine(_env.ContentRootPath, 
                "downloads", "latest", "version.json");
            return Results.File(versionFile, "application/json");
        });
        
        // API: 获取所有版本
        app.MapGet("/api/versions", () =>
        {
            var versionsFile = Path.Combine(_env.ContentRootPath, 
                "downloads", "versions.json");
            return Results.File(versionsFile, "application/json");
        });
        
        // API: 检查更新
        app.MapGet("/api/update/check", (string currentVersion) =>
        {
            var latest = GetLatestVersion();
            var current = Version.Parse(currentVersion);
            
            if (latest > current)
            {
                return Results.Ok(new
                {
                    HasUpdate = true,
                    LatestVersion = latest.ToString(),
                    DownloadUrl = $"/downloads/latest/Screen2MD-Setup.exe",
                    ReleaseNotes = "/changelog"
                });
            }
            
            return Results.Ok(new { HasUpdate = false });
        });
        
        await app.RunAsync(stoppingToken);
    }
}
```

---

## 4. Nginx 配置

```nginx
# /etc/nginx/sites-available/screen2md

server {
    listen 9999;
    server_name 106.13.179.14;
    
    # 日志
    access_log /var/log/nginx/screen2md-access.log;
    error_log /var/log/nginx/screen2md-error.log;
    
    # Gzip压缩
    gzip on;
    gzip_types text/plain text/css application/json 
               application/javascript text/xml application/xml;
    
    # 文档网站 (MkDocs)
    location / {
        proxy_pass http://localhost:9990;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        
        # 缓存静态资源
        location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
    }
    
    # 下载服务
    location /downloads {
        proxy_pass http://localhost:9991;
        proxy_set_header Host $host;
        
        # 大文件传输优化
        proxy_buffering off;
        proxy_request_buffering off;
        
        # 限速 (可选)
        # limit_rate 1m;
    }
    
    # API服务
    location /api {
        proxy_pass http://localhost:9992;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        
        # API缓存
        location ~* /api/version/ {
            expires 1h;
            add_header Cache-Control "public";
        }
    }
    
    # 错误页面
    error_page 404 /404.html;
    error_page 500 502 503 504 /50x.html;
}
```

---

## 5. 自动化部署

### 5.1 文档部署脚本

```bash
#!/bin/bash
# deploy-docs.sh

set -e

WEB_ROOT="/opt/screen2md-web"
DOCS_BUILD="docs-site/site"

echo "Building documentation..."
cd docs-site
mkdocs build --strict

echo "Deploying to server..."
rsync -avz --delete $DOCS_BUILD/ root@106.13.179.14:$WEB_ROOT/docs/

echo "Documentation deployed successfully!"
echo "URL: http://106.13.179.14:9999"
```

### 5.2 版本发布脚本

```bash
#!/bin/bash
# release-version.sh

VERSION=$1
if [ -z "$VERSION" ]; then
    echo "Usage: $0 <version>"
    exit 1
fi

WEB_ROOT="/opt/screen2md-web/downloads"
SERVER="root@106.13.179.14"

echo "Releasing version $VERSION..."

# 创建版本目录
ssh $SERVER "mkdir -p $WEB_ROOT/v$VERSION"

# 上传文件
echo "Uploading installer..."
scp "artifacts/Screen2MD-$VERSION-Setup.exe" $SERVER:$WEB_ROOT/v$VERSION/

echo "Uploading portable version..."
scp "artifacts/Screen2MD-$VERSION-Portable.zip" $SERVER:$WEB_ROOT/v$VERSION/

# 生成校验和
echo "Generating checksums..."
ssh $SERVER "cd $WEB_ROOT/v$VERSION && sha256sum * > checksums.sha256"

# 更新 latest 链接
echo "Updating latest version..."
ssh $SERVER "
    rm -rf $WEB_ROOT/latest/*
    cp $WEB_ROOT/v$VERSION/* $WEB_ROOT/latest/
    echo '{\"version\": \"'$VERSION'\", \"date\": \"'$(date -I)'\"}' > $WEB_ROOT/latest/version.json
"

# 更新版本索引
echo "Updating version index..."
scp versions.json $SERVER:$WEB_ROOT/

echo "Version $VERSION released successfully!"
echo "Download: http://106.13.179.14:9999/downloads/latest/"
```

### 5.3 GitHub Actions 工作流

```yaml
# .github/workflows/release.yml
name: Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'
      
      - name: Build
        run: dotnet build -c Release
      
      - name: Test
        run: dotnet test --filter "Category=Release"
      
      - name: Publish
        run: |
          dotnet publish src/Screen2MD.Daemon -c Release -o publish
          dotnet publish src/Screen2MD.UI -c Release -o publish
      
      - name: Create Installer
        run: |
          iscc installer.iss
      
      - name: Upload to Server
        run: |
          ./scripts/release-version.sh ${{ github.ref_name }}
        env:
          SSH_KEY: ${{ secrets.SSH_KEY }}
      
      - name: Create GitHub Release
        uses: softprops/action-gh-release@v1
        with:
          files: |
            artifacts/Screen2MD-*-Setup.exe
            artifacts/Screen2MD-*-Portable.zip
```

---

## 6. 监控与统计

### 6.1 下载统计

```csharp
// 简单的下载统计中间件
public class DownloadTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IStatisticsStore _store;
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/downloads"))
        {
            var fileName = Path.GetFileName(context.Request.Path);
            var userAgent = context.Request.Headers.UserAgent.ToString();
            var ip = context.Connection.RemoteIpAddress?.ToString();
            
            await _store.RecordDownload(new DownloadRecord
            {
                Timestamp = DateTime.UtcNow,
                FileName = fileName,
                UserAgent = userAgent,
                IpAddress = ip,
                Referer = context.Request.Headers.Referer
            });
        }
        
        await _next(context);
    }
}
```

### 6.2 统计面板

```
http://106.13.179.14:9999/stats

下载统计
├── 今日下载: 156
├── 本周下载: 1,234
├── 本月下载: 5,678
└── 总下载: 45,678

版本分布
├── v2.0.0: 45%
├── v1.5.0: 35%
└── 其他: 20%

地理分布
├── 中国: 80%
├── 美国: 10%
└── 其他: 10%
```

---

## 7. 部署清单

### 7.1 服务器配置

```bash
# 1. 安装依赖
apt update
apt install -y nginx python3-pip
pip3 install mkdocs mkdocs-material

# 2. 创建目录
mkdir -p /opt/screen2md-web/{docs,downloads}
mkdir -p /var/log/nginx

# 3. 配置防火墙
ufw allow 9999/tcp

# 4. 启动服务
systemctl enable nginx
systemctl start nginx
```

### 7.2 服务启动

```bash
# 启动文档服务 (MkDocs)
cd /opt/screen2md-web/docs
mkdocs serve --dev-addr=0.0.0.0:9990 &

# 启动下载服务
cd /opt/screen2md-web
dotnet run --project DownloadService.csproj --urls=http://0.0.0.0:9991 &

# 重载 Nginx
nginx -s reload
```

---

**文档版本**: v1.0.0  
**网站地址**: http://106.13.179.14:9999  
**维护团队**: DevOps Team  
**日期**: 2026-03-05
