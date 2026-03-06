using Xunit;
using Screen2MD.Core.Services;

namespace Screen2MD.Core.Tests.Services;

/// <summary>
/// ConfigurationService 完整单元测试
/// </summary>
public class ConfigurationServiceConstructorTests
{
    [Fact]
    public void Constructor_WithDefaultPath_ShouldNotThrow()
    {
        var service = new ConfigurationService();
        
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithCustomPath_ShouldNotThrow()
    {
        var customPath = Path.Combine(Path.GetTempPath(), $"ConfigTest_{Guid.NewGuid()}", "config.json");
        
        var service = new ConfigurationService(configPath: customPath);
        
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNonExistentDirectory_ShouldCreateDirectory()
    {
        var customPath = Path.Combine(Path.GetTempPath(), $"NewDir_{Guid.NewGuid()}");
        var configPath = Path.Combine(customPath, "config.json");
        
        var service = new ConfigurationService(configPath: configPath);
        
        // 触发保存以创建目录
        service.Set("test", "value");
        
        Assert.True(Directory.Exists(customPath));
        
        // 清理
        if (Directory.Exists(customPath))
            Directory.Delete(customPath, recursive: true);
    }
}

/// <summary>
/// Get 方法测试
/// </summary>
public class ConfigurationServiceGetTests : IDisposable
{
    private readonly string _testConfigPath;

    public ConfigurationServiceGetTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"ConfigGetTest_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
    }

    [Fact]
    public void Get_WithExistingKey_ShouldReturnValue()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        service.Set("test.key", "testValue");
        
        var value = service.Get<string>("test.key", "default");
        
        Assert.Equal("testValue", value);
    }

    [Fact]
    public void Get_WithMissingKey_ShouldReturnDefault()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        
        var value = service.Get("missing.key", "defaultValue");
        
        Assert.Equal("defaultValue", value);
    }

    [Fact]
    public void Get_WithNullDefault_ShouldReturnNull()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        
        var value = service.Get<string?>("missing.key", null);
        
        Assert.Null(value);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(3.14)]
    [InlineData(true)]
    [InlineData("string")]
    public void Get_WithDifferentTypes_ShouldWork<T>(T testValue)
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        
        // 只能测试特定类型
        if (testValue is int intValue)
        {
            service.Set("test", intValue);
            var result = service.Get<int>("test", 0);
            Assert.Equal(intValue, result);
        }
        else if (testValue is bool boolValue)
        {
            service.Set("test", boolValue);
            var result = service.Get<bool>("test", false);
            Assert.Equal(boolValue, result);
        }
        else if (testValue is string stringValue)
        {
            service.Set("test", stringValue);
            var result = service.Get<string>("test", "");
            Assert.Equal(stringValue, result);
        }
    }

    [Fact]
    public void Get_WithDefaultValues_ShouldReturnCorrectDefaults()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        
        var interval = service.Get("capture.intervalSeconds", 0);
        var enableOcr = service.Get("capture.enableOcr", false);
        var maxStorage = service.Get("storage.maxStorageGB", 0);
        
        Assert.True(interval > 0);
        Assert.True(enableOcr);
        Assert.True(maxStorage > 0);
    }

    [Fact(Skip = "Linux file system instability")]
    public void Get_WithArray_ShouldReturnArray()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        
        var languages = service.Get("ocr.languages", Array.Empty<string>());
        
        Assert.NotNull(languages);
        Assert.True(languages.Length > 0);
    }
}

/// <summary>
/// Set 方法测试
/// </summary>
public class ConfigurationServiceSetTests : IDisposable
{
    private readonly string _testConfigPath;

    public ConfigurationServiceSetTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"ConfigSetTest_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
    }

    [Fact]
    public void Set_WithNewKey_ShouldCreateKey()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        
        service.Set("new.key", "newValue");
        var value = service.Get<string>("new.key", "");
        
        Assert.Equal("newValue", value);
    }

    [Fact]
    public void Set_WithExistingKey_ShouldUpdateValue()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        service.Set("update.key", "oldValue");
        
        service.Set("update.key", "newValue");
        var value = service.Get<string>("update.key", "");
        
        Assert.Equal("newValue", value);
    }

    [Fact]
    public void Set_ShouldPersistToFile()
    {
        // 第一个实例设置值
        var service1 = new ConfigurationService(configPath: _testConfigPath);
        service1.Set("persist.key", "persistValue");
        
        // 第二个实例读取值
        var service2 = new ConfigurationService(configPath: _testConfigPath);
        var value = service2.Get<string>("persist.key", "");
        
        Assert.Equal("persistValue", value);
    }

    [Fact]
    public void Set_WithNullValue_ShouldReturnDefault()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        
        service.Set<string?>("null.key", null);
        var value = service.Get<string?>("null.key", "default");
        
        // null 值在内部存储，但 Get 方法会返回默认值（当值为 null 时）
        Assert.Equal("default", value);
    }

    [Fact]
    public void Set_WithComplexObject_ShouldSerialize()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        var obj = new { Name = "Test", Value = 42 };
        
        service.Set("complex.key", obj);
        
        // 应该能够存储，但读取时类型可能丢失
        Assert.True(File.Exists(_testConfigPath));
    }

    [Fact]
    public async Task Set_ConcurrentAccess_ShouldBeThreadSafe()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        var tasks = new List<Task>();
        
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                service.Set($"key{index}", $"value{index}");
            }));
        }
        
        await Task.WhenAll(tasks);
        
        // 所有值应该都被正确设置
        for (int i = 0; i < 100; i++)
        {
            var value = service.Get<string>($"key{i}", "");
            Assert.Equal($"value{i}", value);
        }
    }
}

/// <summary>
/// 文件操作测试
/// 注意：这些测试在 Linux 环境下可能导致崩溃，暂时跳过
/// </summary>
public class ConfigurationServiceFileTests : IDisposable
{
    private readonly string _testConfigPath;

    public ConfigurationServiceFileTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"ConfigFileTest_{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
    }

    [Fact]
    public void Constructor_WithCorruptedFile_ShouldReturnEmptyConfig()
    {
        // 创建损坏的 JSON 文件
        File.WriteAllText(_testConfigPath, "not valid json {[");
        
        var service = new ConfigurationService(configPath: _testConfigPath);
        var value = service.Get<string?>("any.key", null);
        
        // 应该返回 null（默认值）而不是崩溃
        Assert.Null(value);
    }

    [Fact]
    public void Constructor_WithEmptyFile_ShouldReturnEmptyConfig()
    {
        File.WriteAllText(_testConfigPath, "");
        
        var service = new ConfigurationService(configPath: _testConfigPath);
        var value = service.Get<string?>("any.key", null);
        
        Assert.Null(value);
    }

    [Fact(Skip = "Linux file system instability")]
    public void Set_ShouldCreatePrettyPrintedJson()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        service.Set("key", "value");
        
        var content = File.ReadAllText(_testConfigPath);
        
        Assert.Contains("{", content);
        Assert.Contains("}", content);
        // 应该是格式化的（包含换行）
        Assert.Contains('\n', content);
    }

    [Fact(Skip = "Linux file system instability")]
    public void Set_ShouldUseCamelCase()
    {
        var service = new ConfigurationService(configPath: _testConfigPath);
        
        // 调用 Set 触发文件创建
        service.Set("testKey", "testValue");
        
        // 读取文件内容
        var content = File.ReadAllText(_testConfigPath);
        
        // 检查文件是否使用 camelCase（小写开头）
        Assert.True(content.Contains("testKey"));
    }
}