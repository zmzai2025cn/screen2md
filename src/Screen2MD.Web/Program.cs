using Screen2MD.Kernel.Interfaces;
using Screen2MD.Kernel.Services;
using Screen2MD.Services.Interfaces;
using Screen2MD.Services.Services;
using Screen2MD.Web.Services;

namespace Screen2MD.Web;

/// <summary>
/// Web 服务器入口 - Kestrel/9999
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 配置 Kestrel 监听端口 9999
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(9999);
        });

        // 添加 Razor Pages 和 Blazor
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();

        // 注册内核服务
        builder.Services.AddSingleton<Screen2MD.Kernel.Interfaces.IConfigurationManager, Screen2MD.Kernel.Services.ConfigurationManager>();
        builder.Services.AddSingleton<Screen2MD.Kernel.Interfaces.ILogManager, Screen2MD.Kernel.Services.LogManager>();
        builder.Services.AddSingleton<Screen2MD.Kernel.Interfaces.IResourceMonitor, Screen2MD.Kernel.Services.ResourceMonitor>();
        builder.Services.AddSingleton<Screen2MD.Kernel.Interfaces.IEventBus, Screen2MD.Kernel.Services.EventBus>();

        // 存储服务
        builder.Services.AddSingleton<IStorageService, SqliteStorageService>();

        // Web 服务
        builder.Services.AddSingleton<DownloadService>();
        builder.Services.AddSingleton<DocumentationService>();

        var app = builder.Build();

        // 初始化服务
        await InitializeServicesAsync(app.Services);

        // 配置中间件
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        // 启动 Web 服务器
        Console.WriteLine("[Web] Starting Screen2MD Web Console on http://0.0.0.0:9999");
        app.Run();
    }

    private static async Task InitializeServicesAsync(IServiceProvider services)
    {
        try
        {
            // 初始化存储服务
            var storage = services.GetRequiredService<IStorageService>();
            await storage.InitializeAsync();
            
            // 初始化 Web 服务
            var download = services.GetRequiredService<DownloadService>();
            await download.InitializeAsync();
            await download.CreateDummyDownloadsAsync();
            
            var docs = services.GetRequiredService<DocumentationService>();
            await docs.InitializeAsync();
            
            Console.WriteLine("[Web] Services initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Web] Service initialization failed: {ex.Message}");
        }
    }
}
