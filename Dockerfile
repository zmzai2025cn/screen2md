# Screen2MD Enterprise - Dockerfile
# 多阶段构建，最小化镜像体积

# 阶段1: 构建
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制项目文件
COPY . .

# 还原依赖
RUN dotnet restore Screen2MD-v3.sln

# 构建发布版本
RUN dotnet publish src/Screen2MD/Screen2MD.csproj \
    -c Release \
    -o /app/publish \
    --self-contained true \
    --runtime linux-x64 \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true

# 阶段2: 运行时
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS runtime
WORKDIR /app

# 安装必要的系统依赖
RUN apt-get update && apt-get install -y \
    libfontconfig1 \
    libfreetype6 \
    libpng16-16 \
    libjpeg62-turbo \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

# 创建非root用户（安全最佳实践）
RUN groupadd -r screen2md && useradd -r -g screen2md screen2md

# 复制构建产物
COPY --from=build /app/publish/Screen2MD .

# 创建数据目录
RUN mkdir -p /data/captures /data/index /data/config && \
    chown -R screen2md:screen2md /data

# 切换到非root用户
USER screen2md

# 数据卷
VOLUME ["/data/captures", "/data/index", "/data/config"]

# 暴露端口（如果未来有Web API）
EXPOSE 8080

# 健康检查
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD ./Screen2MD --health-check || exit 1

# 入口点
ENTRYPOINT ["./Screen2MD"]
