using System.Text.Json;

namespace Screen2MD.Core.Services;

/// <summary>
/// 配置服务 - JSON 配置管理
/// </summary>
public sealed class ConfigurationService
{
    private readonly string _configPath;
    private readonly Dictionary<string, object?> _config;
    private readonly ReaderWriterLockSlim _lock = new();

    public ConfigurationService(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "config.json");
        
        _config = LoadConfig();
        SetDefaults();
    }

    /// <summary>
    /// 获取配置值
    /// </summary>
    public T? Get<T>(string key, T? defaultValue = default)
    {
        _lock.EnterReadLock();
        try
        {
            if (_config.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    if (value is T typedValue)
                        return typedValue;
                    
                    var json = JsonSerializer.Serialize(value);
                    return JsonSerializer.Deserialize<T>(json);
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 设置配置值
    /// </summary>
    public void Set<T>(string key, T value)
    {
        _lock.EnterWriteLock();
        try
        {
            _config[key] = value;
            SaveConfig();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 保存配置到文件
    /// </summary>
    private void SaveConfig()
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(_config, options);
        File.WriteAllText(_configPath, json);
    }

    /// <summary>
    /// 加载配置文件
    /// </summary>
    private Dictionary<string, object?> LoadConfig()
    {
        if (!File.Exists(_configPath))
            return new Dictionary<string, object?>();

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    /// <summary>
    /// 设置默认值
    /// </summary>
    private void SetDefaults()
    {
        // 截图配置
        SetDefault("capture.intervalSeconds", 30);
        SetDefault("capture.outputDirectory", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "Captures"));
        SetDefault("capture.formats", new[] { "png" });
        SetDefault("capture.enableOcr", true);
        SetDefault("capture.similarityThreshold", 0.95);

        // OCR 配置
        SetDefault("ocr.languages", new[] { "chi_sim", "eng" });
        SetDefault("ocr.timeoutSeconds", 30);

        // 存储配置
        SetDefault("storage.maxStorageGB", 10);
        SetDefault("storage.autoCleanupDays", 30);
        SetDefault("storage.cleanupEnabled", true);
    }

    private void SetDefault<T>(string key, T value)
    {
        if (!_config.ContainsKey(key))
            _config[key] = value!;
    }
}