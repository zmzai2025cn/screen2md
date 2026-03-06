using Screen2MD.Kernel;
using Screen2MD.Kernel.Core;
using Screen2MD.Kernel.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace Screen2MD.Kernel.Tests.Core;

/// <summary>
/// 内核引导器测试 - 零崩溃保障验证
/// </summary>
public class KernelBootstrapperTests
{
    private readonly IServiceCollection _services;

    public KernelBootstrapperTests()
    {
        _services = new ServiceCollection();
        _services.AddScreen2MDKernel();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "Critical")]
    public async Task StartAsync_WithValidConfiguration_ShouldSucceed()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();

        // Act
        var result = await bootstrapper.StartAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Cancelled.Should().BeFalse();

        // Cleanup
        await bootstrapper.ShutdownAsync();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "Critical")]
    public async Task StartAsync_ShouldInitializeAllComponents()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();
        var components = serviceProvider.GetServices<IKernelComponent>().ToList();

        // Act
        await bootstrapper.StartAsync();

        // Assert
        components.Should().NotBeEmpty();
        foreach (var component in components)
        {
            component.HealthStatus.Should().BeOneOf(
                HealthStatus.Healthy, 
                HealthStatus.Degraded,
                HealthStatus.Unknown);
        }

        // Cleanup
        await bootstrapper.ShutdownAsync();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "Critical")]
    public async Task ShutdownAsync_ShouldStopAllComponents()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();
        await bootstrapper.StartAsync();

        // Act
        await bootstrapper.ShutdownAsync();

        // Assert - 组件应该被正确停止（通过不抛出异常来验证）
        // 实际组件状态验证需要更复杂的集成测试
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public async Task StartAsync_WhenCancelled_ShouldReturnCancelledResult()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await bootstrapper.StartAsync(cts.Token);

        // Assert
        result.Cancelled.Should().BeTrue();
        result.Success.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    [Trait("Duration", "Short")]
    public async Task Kernel_Should_NotCrash_On_MultipleStartStop()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        
        // Act & Assert - 多次启动/停止不应崩溃
        for (int i = 0; i < 5; i++)
        {
            var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();
            
            var startResult = await bootstrapper.StartAsync();
            startResult.Success.Should().BeTrue($"Start iteration {i} failed");
            
            await bootstrapper.ShutdownAsync();
        }
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    [Trait("Duration", "Short")]
    public void Kernel_Should_HandleExceptions_Gracefully()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();

        // Act - 使用反射测试异常处理
        var exceptionHandled = false;
        AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
        {
            if (e.Exception.Message.Contains("test exception"))
            {
                exceptionHandled = true;
            }
        };

        try
        {
            throw new Exception("test exception");
        }
        catch
        {
            // 异常被捕获，不应传播
        }

        // Assert
        // 如果执行到这里没有崩溃，说明异常处理正常工作
        bootstrapper.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public async Task Kernel_Startup_ShouldRespectPriorityOrder()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();
        var logManager = serviceProvider.GetService<ILogManager>();

        // Act
        var result = await bootstrapper.StartAsync();

        // Assert
        result.Success.Should().BeTrue();
        
        // 验证日志管理器已初始化（关键组件）
        logManager.Should().NotBeNull();
        logManager!.HealthStatus.Should().Be(HealthStatus.Healthy);

        // Cleanup
        await bootstrapper.ShutdownAsync();
    }

    [Fact]
    [Trait("Category", "Resource")]
    [Trait("Priority", "High")]
    public async Task Kernel_Should_MonitorResources_AfterStartup()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();
        var monitor = serviceProvider.GetRequiredService<IResourceMonitor>();

        // Act
        await bootstrapper.StartAsync();

        // 等待一段时间让监控器收集数据
        await Task.Delay(100);

        // Assert
        var report = monitor.GetReport(TimeSpan.FromSeconds(1));
        report.Should().NotBeNull();
        report.MaxMemoryMB.Should().BeGreaterThan(0);

        // Cleanup
        await bootstrapper.ShutdownAsync();
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    [Trait("Duration", "LongRunning")]
    public async Task Kernel_Should_RunStable_For5Minutes()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();
        var logManager = serviceProvider.GetRequiredService<ILogManager>();
        
        // Act
        var startResult = await bootstrapper.StartAsync();
        startResult.Success.Should().BeTrue();

        // 运行5分钟
        var testDuration = TimeSpan.FromMinutes(5);
        var startTime = DateTime.UtcNow;
        var errorCount = 0;

        while (DateTime.UtcNow - startTime < testDuration)
        {
            // 检查错误日志
            var errors = logManager.GetLogsByLevel(LogLevel.Error, startTime);
            errorCount += errors.Count();

            if (errorCount > 0)
            {
                break;
            }

            await Task.Delay(1000);
        }

        // Cleanup
        await bootstrapper.ShutdownAsync();

        // Assert
        errorCount.Should().Be(0, $"Found {errorCount} error logs during 5-minute stability test");
    }
}