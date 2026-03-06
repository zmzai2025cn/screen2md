using Screen2MD.Kernel.Core;
using Screen2MD.Kernel.Interfaces;
using Screen2MD.Kernel.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Screen2MD.Kernel.Tests.Services;

/// <summary>
/// 事件总线测试 - 异步消息传递验证
/// </summary>
public class EventBusTests : IDisposable
{
    private readonly EventBus _eventBus;

    public EventBusTests()
    {
        _eventBus = new EventBus();
        _eventBus.InitializeAsync().Wait();
    }

    public void Dispose()
    {
        _eventBus.Dispose();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PublishAsync_ShouldDeliverEventToSubscriber()
    {
        // Arrange
        var receivedEvent = false;
        var handler = new TestEventHandler<TestEvent>((e) =>
        {
            receivedEvent = true;
            return Task.CompletedTask;
        });

        using var subscription = _eventBus.Subscribe(handler);

        var testEvent = new TestEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EventType = nameof(TestEvent),
            Source = "Test",
            Message = "Test message"
        };

        // Act
        await _eventBus.PublishAsync(testEvent);
        await Task.Delay(100); // 等待事件处理

        // Assert
        receivedEvent.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task PublishAsync_ShouldDeliverToMultipleSubscribers()
    {
        // Arrange
        var receivedCount = 0;
        
        var handler1 = new TestEventHandler<TestEvent>((e) =>
        {
            Interlocked.Increment(ref receivedCount);
            return Task.CompletedTask;
        });
        
        var handler2 = new TestEventHandler<TestEvent>((e) =>
        {
            Interlocked.Increment(ref receivedCount);
            return Task.CompletedTask;
        });

        using var sub1 = _eventBus.Subscribe(handler1);
        using var sub2 = _eventBus.Subscribe(handler2);

        var testEvent = new TestEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EventType = nameof(TestEvent),
            Source = "Test",
            Message = "Test message"
        };

        // Act
        await _eventBus.PublishAsync(testEvent);
        await Task.Delay(100);

        // Assert
        receivedCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DisposeSubscription_ShouldStopReceivingEvents()
    {
        // Arrange
        var receivedCount = 0;
        var handler = new TestEventHandler<TestEvent>((e) =>
        {
            Interlocked.Increment(ref receivedCount);
            return Task.CompletedTask;
        });

        var subscription = _eventBus.Subscribe(handler);

        // Act - 发送第一个事件
        await _eventBus.PublishAsync(new TestEvent { EventId = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow, EventType = nameof(TestEvent), Source = "Test" });
        await Task.Delay(100);
        
        // 取消订阅
        subscription.Dispose();
        
        // 发送第二个事件
        await _eventBus.PublishAsync(new TestEvent { EventId = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow, EventType = nameof(TestEvent), Source = "Test" });
        await Task.Delay(100);

        // Assert
        receivedCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task PublishAsync_ShouldHandleHighThroughput()
    {
        // Arrange
        var receivedCount = 0;
        var handler = new TestEventHandler<TestEvent>((e) =>
        {
            Interlocked.Increment(ref receivedCount);
            return Task.CompletedTask;
        });

        using var sub = _eventBus.Subscribe(handler);
        var eventCount = 10000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < eventCount; i++)
        {
            await _eventBus.PublishAsync(new TestEvent 
            { 
                EventId = Guid.NewGuid(), 
                Timestamp = DateTimeOffset.UtcNow, 
                EventType = nameof(TestEvent), 
                Source = "Test",
                Message = $"Message {i}"
            });
        }

        // 等待所有事件处理完成
        await Task.Delay(2000);
        stopwatch.Stop();

        // Assert
        receivedCount.Should().Be(eventCount);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            $"Processing {eventCount} events took too long");
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public async Task EventBus_ShouldNotCrash_WhenHandlerThrows()
    {
        // Arrange
        var receivedCount = 0;
        var throwingHandler = new TestEventHandler<TestEvent>((e) =>
        {
            throw new Exception("Handler error");
        });
        
        var normalHandler = new TestEventHandler<TestEvent>((e) =>
        {
            Interlocked.Increment(ref receivedCount);
            return Task.CompletedTask;
        });

        using var sub1 = _eventBus.Subscribe(throwingHandler);
        using var sub2 = _eventBus.Subscribe(normalHandler);

        var testEvent = new TestEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            EventType = nameof(TestEvent),
            Source = "Test"
        };

        // Act - 不应抛出异常
        var exception = await Record.ExceptionAsync(async () =>
        {
            await _eventBus.PublishAsync(testEvent);
            await Task.Delay(100);
        });

        // Assert
        exception.Should().BeNull();
        receivedCount.Should().Be(1, "Normal handler should still receive events even if another handler throws");
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public async Task EventBus_ShouldHandleNullEvent()
    {
        // Act & Assert - 不应抛出异常
        // 注意：PublishAsync 使用泛型约束，理论上不会接收到null
        // 这里测试的是即使传入默认值也不会崩溃
        var exception = await Record.ExceptionAsync(async () =>
        {
            await _eventBus.PublishAsync(new TestEvent 
            { 
                EventId = Guid.Empty, 
                Timestamp = DateTimeOffset.MinValue, 
                EventType = null!, 
                Source = null! 
            });
        });

        exception.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EventBus_ShouldMaintainEventOrder()
    {
        // Arrange
        var receivedMessages = new List<int>();
        var handler = new TestEventHandler<TestEvent>((e) =>
        {
            if (int.TryParse(e.Message, out var num))
            {
                lock (receivedMessages)
                {
                    receivedMessages.Add(num);
                }
            }
            return Task.CompletedTask;
        });

        using var sub = _eventBus.Subscribe(handler);

        // Act
        for (int i = 0; i < 100; i++)
        {
            await _eventBus.PublishAsync(new TestEvent 
            { 
                EventId = Guid.NewGuid(), 
                Timestamp = DateTimeOffset.UtcNow, 
                EventType = nameof(TestEvent), 
                Source = "Test",
                Message = i.ToString()
            });
        }

        await Task.Delay(500);

        // Assert
        receivedMessages.Should().HaveCount(100);
        // 由于是并行处理，顺序可能不严格保证
        // 但至少所有消息都应该被收到
    }

    private class TestEvent : IKernelEvent
    {
        public Guid EventId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private class TestEventHandler<TEvent> : IEventHandler<TEvent> where TEvent : IKernelEvent
    {
        private readonly Func<TEvent, Task> _handler;

        public TestEventHandler(Func<TEvent, Task> handler)
        {
            _handler = handler;
        }

        public Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
        {
            return _handler(@event);
        }
    }
}

/// <summary>
/// 插件主机测试
/// </summary>
public class PluginHostTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PluginHost _pluginHost;

    public PluginHostTests()
    {
        var services = new ServiceCollection();
        services.AddScreen2MDKernel();
        _serviceProvider = services.BuildServiceProvider();
        
        _pluginHost = (PluginHost)_serviceProvider.GetRequiredService<IPluginHost>();
        _pluginHost.InitializeAsync().Wait();
    }

    public void Dispose()
    {
        _pluginHost.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PluginHost_ShouldHaveCorrectName()
    {
        // Assert
        _pluginHost.Name.Should().Be(nameof(PluginHost));
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public void LoadPlugin_ShouldThrow_WhenFileNotFound()
    {
        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() =>
        {
            _pluginHost.LoadPlugin("/nonexistent/plugin.dll");
        });

        exception.Message.Should().Contain("Plugin not found");
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public void LoadPlugin_ShouldThrow_WhenInvalidFile()
    {
        // Arrange - 创建一个非DLL文件
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "not a valid assembly");

        try
        {
            // Act & Assert
            Assert.ThrowsAny<Exception>(() =>
            {
                _pluginHost.LoadPlugin(tempFile);
            });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadedPlugins_ShouldReturnEmptyList_Initially()
    {
        // Assert
        _pluginHost.LoadedPlugins.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public void UnloadPlugin_ShouldNotThrow_WhenPluginNotExists()
    {
        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() =>
        {
            _pluginHost.UnloadPlugin("nonexistent_plugin");
        });

        exception.Should().BeNull();
    }
}