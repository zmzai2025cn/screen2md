using Screen2MD.Kernel.Interfaces;
using Screen2MD.Kernel.Services;
using FluentAssertions;
using Xunit;

namespace Screen2MD.Kernel.Tests.Services;

/// <summary>
/// 配置管理器测试 - 热重载和变更通知验证
/// </summary>
public class ConfigurationManagerTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly ConfigurationManager _configManager;

    public ConfigurationManagerTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"s2md_test_config_{Guid.NewGuid()}.json");
        _configManager = new ConfigurationManager(_testConfigPath);
        _configManager.InitializeAsync().Wait();
    }

    public void Dispose()
    {
        _configManager.Dispose();
        
        try
        {
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
        }
        catch { }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetValue_ShouldReturnDefaultValue_WhenKeyNotExists()
    {
        // Act
        var value = _configManager.GetValue("nonexistent.key", "default");

        // Assert
        value.Should().Be("default");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetValue_ShouldReturnTypedValue()
    {
        // Act
        var interval = _configManager.GetValue("capture.interval_seconds", 0);

        // Assert - 应该有默认值
        interval.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SetValue_ShouldStoreValue()
    {
        // Arrange
        var key = "test.key";
        var value = "test_value";

        // Act
        _configManager.SetValue(key, value);

        // Assert
        var retrieved = _configManager.GetValue(key, string.Empty);
        retrieved.Should().Be(value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SetValue_ShouldTriggerChangeEvent()
    {
        // Arrange
        var key = "event.test.key";
        var value = "new_value";
        ConfigurationChangedEventArgs? capturedEvent = null;
        
        _configManager.ConfigurationChanged += (s, e) =>
        {
            if (e.Key == key)
            {
                capturedEvent = e;
            }
        };

        // Act
        _configManager.SetValue(key, value);
        Thread.Sleep(100); // 给事件处理一点时间

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Key.Should().Be(key);
        capturedEvent.NewValue.Should().Be(value);
        capturedEvent.ChangedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("capture.interval_seconds", 5)]
    [InlineData("capture.adaptive", true)]
    [InlineData("privacy.block_passwords", true)]
    [InlineData("storage.local.enabled", true)]
    [Trait("Category", "Unit")]
    public void DefaultValues_ShouldBeSet(string key, object expected)
    {
        // Act
        var value = _configManager.GetValue<object>(key, null);

        // Assert
        value.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetValue_ShouldHandleTypeConversion()
    {
        // Arrange
        _configManager.SetValue("int.key", 42);
        _configManager.SetValue("bool.key", true);
        _configManager.SetValue("string.key", "hello");

        // Act & Assert
        _configManager.GetValue<int>("int.key", 0).Should().Be(42);
        _configManager.GetValue<bool>("bool.key", false).Should().BeTrue();
        _configManager.GetValue<string>("string.key", "").Should().Be("hello");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReloadAsync_ShouldReloadFromFile()
    {
        // Arrange
        var key = "reload.test";
        var value = "before_reload";
        _configManager.SetValue(key, value);
        
        // 直接修改文件
        var json = $"{{\"{key}\": \"after_reload\"}}";
        File.WriteAllText(_testConfigPath, json);

        // Act
        await _configManager.ReloadAsync();
        Thread.Sleep(100);

        // Assert
        var reloaded = _configManager.GetValue(key, "");
        reloaded.Should().Be("after_reload");
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public void ConfigurationManager_ShouldHandleMissingFile()
    {
        // Arrange - 使用不存在的文件路径
        var missingPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");
        
        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() =>
        {
            var manager = new ConfigurationManager(missingPath);
            manager.InitializeAsync().Wait();
            manager.Dispose();
        });

        exception.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public void ConfigurationManager_ShouldHandleInvalidJson()
    {
        // Arrange - 写入无效JSON
        File.WriteAllText(_testConfigPath, "invalid json {{{");

        // Act & Assert - 不应抛出异常，应使用默认值
        var exception = Record.Exception(() =>
        {
            var manager = new ConfigurationManager(_testConfigPath);
            manager.InitializeAsync().Wait();
            
            // 应该能读取默认值
            var interval = manager.GetValue("capture.interval_seconds", 0);
            interval.Should().BeGreaterThan(0);
            
            manager.Dispose();
        });

        exception.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SetValue_ShouldPersistToFile()
    {
        // Arrange
        var key = "persist.test";
        var value = "persisted_value";

        // Act
        _configManager.SetValue(key, value);
        Thread.Sleep(1000); // 增加等待时间确保异步保存完成

        // Assert
        File.Exists(_testConfigPath).Should().BeTrue();
        var json = File.ReadAllText(_testConfigPath);
        json.Should().Contain(key.Replace('.', '_')); // JSON key可能使用下划线
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public void ConfigurationManager_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var tasks = new List<Task>();
        var iterations = 100;

        // Act - 并发读写
        for (int i = 0; i < 10; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    _configManager.SetValue($"concurrent.{taskId}.{j}", $"value_{j}");
                    _configManager.GetValue<string>($"concurrent.{taskId}.{j}", "");
                }
            }));
        }

        // Assert - 不应抛出异常
        Task.WaitAll(tasks.ToArray());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ConfigurationManager_ShouldReturnCorrectHealthStatus()
    {
        // Assert
        _configManager.HealthStatus.Should().Be(HealthStatus.Healthy);
    }
}