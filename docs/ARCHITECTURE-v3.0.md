# Screen2MD v3.0 架构设计文档

## 设计目标

解决 v2.x 的核心痛点：**Linux 无法测试 Windows 软件**

通过以下手段实现 90% 代码可在 Linux 测试：
1. **抽象层架构** - 接口隔离平台依赖
2. **SkiaSharp** - 跨平台图像处理
3. **平台属性标记** - 明确测试边界

## 项目结构

```
Screen2MD/
├── src/
│   ├── Screen2MD.Abstractions/          # 核心接口（无外部依赖）
│   │   ├── IScreenCaptureEngine         # 截图引擎接口
│   │   ├── IImageProcessor              # 图像处理器接口
│   │   ├── IOcrEngine                   # OCR 接口
│   │   └── ...
│   │
│   ├── Screen2MD.Platform.Common/       # 跨平台实现
│   │   ├── SkiaCapturedImage            # SkiaSharp 图像包装
│   │   ├── SkiaImageProcessor           # 图像处理算法
│   │   └── ...
│   │
│   ├── Screen2MD.Platform.Mock/         # Mock 实现（测试用）
│   │   ├── MockCaptureEngine            # 模拟截图
│   │   ├── MockOcrEngine                # 模拟 OCR
│   │   └── MockPlatformServiceFactory   # 工厂
│   │
│   ├── Screen2MD.Platform.Windows/      # Windows 实现
│   │   ├── WindowsCaptureEngine         # GDI32 + Skia
│   │   ├── WindowsOcrEngine             # Tesseract 封装
│   │   └── WindowsPlatformServiceFactory
│   │
│   ├── Screen2MD.Platform.Linux/        # Linux 实现（可选）
│   │   ├── LinuxCaptureEngine           # X11/Wayland + Skia
│   │   └── ...
│   │
│   ├── Screen2MD.Testing/               # 测试框架
│   │   ├── PlatformSpecificAttribute    # 平台标记
│   │   ├── UsesMockDataAttribute        # Mock 标记
│   │   └── ContractTestAttribute        # 契约标记
│   │
│   └── Screen2MD.Core/                  # 业务逻辑（完全可测试）
│       ├── CaptureService               # 截图服务
│       ├── StorageService               # 存储服务
│       ├── SearchService                # 搜索服务
│       └── ...
│
├── tests/
│   ├── Screen2MD.Abstractions.Tests/    # 契约测试（Linux 运行）
│   ├── Screen2MD.Core.Tests/            # 业务逻辑测试（Linux 运行）
│   ├── Screen2MD.Integration.Tests/     # 集成测试（Windows 运行）
│   └── Screen2MD.E2E.Tests/             # 端到端测试（Windows 运行）
```

## 核心设计原则

### 1. 依赖倒置原则（DIP）

```csharp
// 高层模块依赖抽象
public class CaptureService : IKernelComponent
{
    private readonly IScreenCaptureEngine _engine;  // 依赖接口，非具体实现
    
    public CaptureService(IScreenCaptureEngine engine)
    {
        _engine = engine;
    }
}

// 运行时注入实现
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    services.AddSingleton<IScreenCaptureEngine, WindowsCaptureEngine>();
else
    services.AddSingleton<IScreenCaptureEngine, MockCaptureEngine>();
```

### 2. 单一职责原则（SRP）

| 类 | 职责 |
|----|------|
| `IScreenCaptureEngine` | 截图（平台相关） |
| `IImageProcessor` | 图像处理（跨平台） |
| `ICaptureService` | 截图业务逻辑（跨平台） |
| `IStorageService` | 存储管理（跨平台） |

### 3. 开闭原则（OCP）

新增平台支持只需添加新实现，无需修改现有代码：

```csharp
// 新增 Mac 支持只需添加
public class MacCaptureEngine : IScreenCaptureEngine { }
```

## 测试策略

### 测试金字塔

```
       /\
      /  \     E2E Tests (5%)  - Windows only
     /----\    端到端测试，验证完整流程
    /      \
   /--------\  Integration Tests (15%)  - Windows only
  /          \ 集成测试，验证平台实现
 /------------\
/              \
/----------------\  Unit Tests (80%)  - Linux + Windows
/                  \ 单元测试，验证业务逻辑
```

### 测试标记使用

```csharp
// 1. 完全跨平台测试（Linux 运行）
[Fact]
public void ComputeSimilarity_SameImage_ShouldReturnOne()
{
    // 使用 Mock 数据，纯算法
}

// 2. 契约测试（Linux 运行，验证接口合规）
[Fact]
[ContractTest(typeof(IScreenCaptureEngine))]
public void CaptureDisplay_ShouldReturnValidImage()
{
    // 验证任何实现都符合契约
}

// 3. 平台特定测试（仅 Windows 运行）
[Fact]
[PlatformSpecific(OSPlatform.Windows, "Requires GDI32")]
public void Capture_RealScreen_ShouldWork()
{
    // 真实截图测试
}
```

### CI/CD 配置

```yaml
# .github/workflows/test.yml
jobs:
  linux-test:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet test --filter "PlatformSpecific!=Windows"
      # 运行 80% 的测试

  windows-test:
    runs-on: windows-latest
    steps:
      - run: dotnet test
      # 运行 100% 的测试
```

## 技术选型理由

### SkiaSharp vs System.Drawing

| 特性 | System.Drawing | SkiaSharp |
|------|---------------|-----------|
| 平台支持 | Windows only | 全平台 |
| 性能 | 中等 | 高（Google 出品） |
| 许可 | Windows 专属 | MIT |
| 维护 | 微软弃用中 | 活跃开发 |
| 功能 | 基础 | 丰富（滤镜、路径等） |

### 为什么不用 ImageSharp?

虽然 ImageSharp 是纯 C#，但：
- 性能低于 SkiaSharp（C++ 核心）
- 不支持某些高级功能（如硬件加速）

## 迁移路径

### 从 v2.x 到 v3.0

1. **阶段1：添加抽象层**（1-2 天）
   - 创建 `Screen2MD.Abstractions`
   - 定义 `IScreenCaptureEngine`, `IImageProcessor`
   - 保持现有代码运行

2. **阶段2：SkiaSharp 迁移**（2-3 天）
   - 替换 `System.Drawing.Bitmap` → `SkiaSharp.SKBitmap`
   - 替换图像处理算法
   - 验证功能一致

3. **阶段3：平台实现分离**（1-2 天）
   - 提取 Windows 特定代码到 `Screen2MD.Platform.Windows`
   - 创建 `Screen2MD.Platform.Mock`

4. **阶段4：测试重构**（1-2 天）
   - 重写测试使用 Mock
   - 添加平台属性标记
   - 验证 Linux CI 通过

总时间：**5-9 天**（vs 之前几个月的技术债务）

## 预期收益

| 指标 | v2.x | v3.0 | 提升 |
|------|------|------|------|
| Linux 可测试代码 | 30% | 90% | +60% |
| CI/CD 覆盖 | 30% | 90% | +60% |
| 新增功能开发时间 | 2x | 1x | 50%↓ |
| Bug 修复时间 | 2x | 1x | 50%↓ |
| 新平台支持成本 | 重构级 | 添加实现 | 80%↓ |

## 总结

v3.0 架构的核心是**通过抽象和依赖注入，将平台特定代码压缩到最小范围**，使 90% 的代码成为可测试、可维护的跨平台业务逻辑。

这种架构不仅解决了 Linux 测试问题，还为未来可能支持的 macOS、Android、iOS 等平台打下了坚实基础。
