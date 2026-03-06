using Microsoft.Extensions.DependencyInjection;
using Screen2MD.Abstractions;
using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using Screen2MD.Platform.Common;
using System.Runtime.InteropServices;

namespace Screen2MD.Core;

/// <summary>
/// Screen2MD v3.0 依赖注入扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Screen2MD 核心服务
    /// 自动根据平台选择实现
    /// </summary>
    public static IServiceCollection AddScreen2MDCore(this IServiceCollection services)
    {
        // 注册图像处理器（跨平台）
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        
        // 根据平台注册截图引擎
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows：使用真实实现（需要创建 WindowsPlatform 项目）
            // services.AddSingleton<IScreenCaptureEngine, WindowsCaptureEngine>();
            // services.AddSingleton<IWindowManager, WindowsWindowManager>();
            
            // 暂时使用 Mock
            services.AddSingleton<IScreenCaptureEngine, MockCaptureEngine>();
            services.AddSingleton<IWindowManager, MockWindowManager>();
            services.AddSingleton<IOcrEngine, MockOcrEngine>();
        }
        else
        {
            // Linux/Mac：使用 Mock 实现（用于测试）
            services.AddSingleton<IScreenCaptureEngine, MockCaptureEngine>();
            services.AddSingleton<IWindowManager, MockWindowManager>();
            services.AddSingleton<IOcrEngine, MockOcrEngine>();
        }
        
        // 注册核心服务
        services.AddSingleton<CaptureService>(sp =>
        {
            var engine = sp.GetRequiredService<IScreenCaptureEngine>();
            var processor = sp.GetRequiredService<IImageProcessor>();
            var windowManager = sp.GetService<IWindowManager>();
            return new CaptureService(engine, processor, windowManager);
        });
        
        services.AddSingleton<OcrService>(sp =>
        {
            var engine = sp.GetRequiredService<IOcrEngine>();
            return new OcrService(engine);
        });
        
        return services;
    }
    
    /// <summary>
    /// 添加 Screen2MD 核心服务（显式指定平台工厂）
    /// 用于测试或自定义实现
    /// </summary>
    public static IServiceCollection AddScreen2MDCore(
        this IServiceCollection services, 
        IPlatformServiceFactory factory)
    {
        services.AddSingleton(factory.CreateImageProcessor());
        services.AddSingleton(factory.CreateCaptureEngine());
        services.AddSingleton(factory.CreateWindowManager());
        services.AddSingleton(factory.CreateOcrEngine());
        
        return services;
    }
}