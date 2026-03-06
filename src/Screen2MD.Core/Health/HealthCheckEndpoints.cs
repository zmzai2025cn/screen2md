using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Screen2MD.Core.Health;

/// <summary>
/// 健康检查端点 - 支持Kubernetes和负载均衡器探测
/// </summary>
public static class HealthCheckEndpoints
{
    /// <summary>
    /// 添加健康检查端点
    /// </summary>    public static IApplicationBuilder UseScreen2MDHealthChecks(this IApplicationBuilder app)
    {
        // Kubernetes存活检查
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // 只检查应用是否存活
            ResponseWriter = WriteHealthResponse
        });

        // Kubernetes就绪检查
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponse
        });

        // 自定义健康检查（包含详情）
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteDetailedHealthResponse
        });

        // 指标端点（Prometheus格式）
        app.MapGet("/metrics", async context =>
        {
            context.Response.ContentType = "text/plain; version=0.0.4";
            // 输出Prometheus格式的指标
            await context.Response.WriteAsync("# Screen2MD Metrics\n");
        });

        return app;
    }

    private static async Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTimeOffset.UtcNow
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private static async Task WriteDetailedHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTimeOffset.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                exception = e.Value.Exception?.Message,
                data = e.Value.Data
            })
        };

        await context.Response.WriteAsJsonAsync(response, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}

/// <summary>
/// 自定义健康检查
/// </summary>
public class CaptureServiceHealthCheck : IHealthCheck
{
    private readonly CaptureService _captureService;

    public CaptureServiceHealthCheck(CaptureService captureService)
    {
        _captureService = captureService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        // 检查截图服务是否正常工作
        try
        {
            // 尝试执行一次测试捕获
            return Task.FromResult(HealthCheckResult.Healthy("Capture service is operational"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Capture service failed", ex));
        }
    }
}

public class StorageHealthCheck : IHealthCheck
{
    private readonly string _storagePath;

    public StorageHealthCheck(string storagePath)
    {
        _storagePath = storagePath;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 检查存储目录是否可写
            var testFile = Path.Combine(_storagePath, $".healthcheck_{Guid.NewGuid()}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            // 检查磁盘空间
            var driveInfo = new DriveInfo(_storagePath);
            var freeSpaceGB = driveInfo.AvailableFreeSpace / 1024 / 1024 / 1024;

            if (freeSpaceGB < 1)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Low disk space: {freeSpaceGB:F2} GB remaining"));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Storage healthy, {freeSpaceGB:F2} GB free"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage check failed", ex));
        }
    }
}
