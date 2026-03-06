# Screen2MD Enterprise - Makefile
# 常用开发命令

.PHONY: help build test clean run docker-build docker-run

# 默认目标
help:
	@echo "Screen2MD Enterprise - 开发命令"
	@echo ""
	@echo "构建:"
	@echo "  make build          - 构建项目"
	@echo "  make build-release  - 构建发布版本"
	@echo ""
	@echo "测试:"
	@echo "  make test           - 运行所有测试"
	@echo "  make test-unit      - 运行单元测试"
	@echo "  make test-stress    - 运行压力测试"
	@echo "  make test-security  - 运行安全测试"
	@echo "  make test-coverage  - 运行测试并生成覆盖率报告"
	@echo ""
	@echo "Docker:"
	@echo "  make docker-build   - 构建Docker镜像"
	@echo "  make docker-run     - 运行Docker容器"
	@echo "  make docker-compose - 启动完整环境"
	@echo ""
	@echo "维护:"
	@echo "  make clean          - 清理构建产物"
	@echo "  make format         - 格式化代码"
	@echo "  make lint           - 代码检查"

# 构建
build:
	dotnet build Screen2MD-v3.sln -c Debug

build-release:
	dotnet build Screen2MD-v3.sln -c Release

# 测试
test:
	dotnet test Screen2MD-v3.sln -c Release --verbosity normal

test-unit:
	dotnet test --filter "Category=Unit" -c Release --verbosity normal

test-stress:
	dotnet test --filter "Category=Stress" -c Release --verbosity normal

test-security:
	dotnet test --filter "Category=Security" -c Release --verbosity normal

test-coverage:
	dotnet tool list -g | grep dotnet-coverage || dotnet tool install --global dotnet-coverage
	dotnet-coverage collect "dotnet test Screen2MD-v3.sln -c Release" -f xml -o coverage.xml
	dotnet reportgenerator -reports:coverage.xml -targetdir:coveragereport -reporttypes:Html

# Docker
docker-build:
	docker build -t screen2md:latest .

docker-run:
	docker run -it --rm \
		-v $(PWD)/data/captures:/data/captures \
		-v $(PWD)/data/index:/data/index \
		-v $(PWD)/data/config:/data/config \
		screen2md:latest

docker-compose:
	docker-compose up -d

docker-compose-down:
	docker-compose down -v

docker-compose-logs:
	docker-compose logs -f screen2md

# 维护
clean:
	dotnet clean Screen2MD-v3.sln
	rm -rf ./publish
	rm -rf ./coveragereport
	rm -f coverage.xml
	find . -type d -name "bin" -o -name "obj" | xargs rm -rf

format:
	dotnet format Screen2MD-v3.sln

lint:
	dotnet build Screen2MD-v3.sln -p:TreatWarningsAsErrors=true -c Release

# 发布
publish-linux:
	dotnet publish src/Screen2MD/Screen2MD.csproj \
		-c Release \
		-r linux-x64 \
		--self-contained true \
		--single-file true \
		-o ./publish/linux-x64

tar -czf ./publish/screen2md-linux-x64.tar.gz -C ./publish/linux-x64 .

publish-windows:
	dotnet publish src/Screen2MD/Screen2MD.csproj \
		-c Release \
		-r win-x64 \
		--self-contained true \
		--single-file true \
		-o ./publish/win-x64

# 本地运行
run:
	dotnet run --project src/Screen2MD/Screen2MD.csproj

run-release:
	dotnet run --project src/Screen2MD/Screen2MD.csproj -c Release

# 安装依赖
install-tools:
	dotnet tool install --global dotnet-format || true
	dotnet tool install --global dotnet-coverage || true
	dotnet tool install --global dotnet-reportgenerator-globaltool || true
