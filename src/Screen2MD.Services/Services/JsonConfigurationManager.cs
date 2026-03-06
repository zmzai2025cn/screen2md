using Screen2MD.Kernel.Interfaces;
using IKernelLogger = Screen2MD.Services.IKernelLogger;
using System.Text.Json;

namespace Screen2MD.Services.Services;

/// <summary>
/// JSON 配置管理器 - 支持热重载
/// </summary>
public sealed class JsonConfigurationManager : IConfigurationManager, IDisposable
{
    private readonly string _configPath;
    private readonly IKernelLogger? _logger;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly Dictionary<string, object?> _config = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private bool _disposed;

    public string Name => nameof(JsonConfigurationManager);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    public JsonConfigurationManager(string? configPath = null, IKernelLogger? logger = null)
    {
        _configPath = configPath ?? GetDefaultConfigPath();
        _logger = logger;

        // 初始化配置目录
        var configDir = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        // 设置文件监视器
        if (!string.IsNullOrEmpty(configDir) && File.Exists(_configPath))
        {
            _fileWatcher = new FileSystemWatcher(configDir, Path.GetFileName(_configPath))
            {
                NotifyFilter = NotifyFilters.LastWrite
            };
            _fileWatcher.Changed += OnConfigFileChanged;
            _fileWatcher.EnableRaisingEvents = true;
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(_configPath))
            {
                await LoadAsync();
                _logger?.LogInformation($"Configuration loaded from {_configPath}");
            }
            else
            {
                // 创建默认配置
                SetDefaults();
                await SaveAsync();
                _logger?.LogInformation("Default configuration created");
            }

            HealthStatus = HealthStatus.Healthy;
        }
        catch (Exception ex)
        {
            HealthStatus = HealthStatus.Unhealthy;
            _logger?.LogError($"Failed to initialize configuration: {ex.Message}");
            throw;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Configuration manager started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _fileWatcher?.Dispose();
        _logger?.LogInformation("Configuration manager stopped");
        return Task.CompletedTask;
    }

    public T? GetValue<T>(string key, T? defaultValue = default)
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
                    
                    // 尝试转换
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

    public void SetValue<T>(string key, T value)
    {
        _lock.EnterWriteLock();
        try
        {
            var oldValue = _config.TryGetValue(key, out var existing) ? existing : default;
            _config[key] = value;

            // 触发变更事件
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
            {
                Key = key,
                OldValue = oldValue,
                NewValue = value,
                ChangedAt = DateTimeOffset.UtcNow
            });

            _logger?.LogDebug($"Configuration updated: {key}");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public async Task ReloadAsync()
    {
        await LoadAsync();
        _logger?.LogInformation("Configuration reloaded");
    }

    public async Task SaveAsync()
    {
        _lock.EnterReadLock();
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var json = JsonSerializer.Serialize(_config, options);
            await File.WriteAllTextAsync(_configPath, json);
            
            _logger?.LogDebug("Configuration saved");
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private async Task LoadAsync()
    {
        var json = await File.ReadAllTextAsync(_configPath);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new();

        _lock.EnterWriteLock();
        try
        {
            _config.Clear();
            foreach (var item in loaded)
            {
                _config[item.Key] = item.Value;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void SetDefaults()
    {
        // 截图配置
        SetValue("capture.intervalSeconds", 30);
        SetValue("capture.outputDirectory", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "Captures"));
        SetValue("capture.formats", new[] { "BMP" });
        SetValue("capture.enableOcr", true);
        SetValue("capture.similarityThreshold", 0.9);

        // OCR 配置
        SetValue("ocr.tesseractPath", "");
        SetValue("ocr.languages", new[] { "chi_sim", "eng" });
        SetValue("ocr.timeoutSeconds", 30);

        // 存储配置
        SetValue("storage.maxStorageGB", 10);
        SetValue("storage.autoCleanupDays", 30);
        SetValue("storage.cleanupEnabled", true);

        // 显示配置
        SetValue("display.captureAllDisplays", true);
        SetValue("display.primaryDisplayOnly", false);

        // 隐私配置
        SetValue("privacy.enablePrivacyFilter", false);
        SetValue("privacy.blurRegions", Array.Empty<object>());
        SetValue("privacy.blurPasswordFields", false);

        // 日志配置
        SetValue("log.level", "Information");
        SetValue("log.maxFileSizeMB", 100);
        SetValue("log.maxFiles", 10);
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // 延迟加载避免文件锁定
            Task.Delay(100).Wait();
            LoadAsync().Wait();
            _logger?.LogInformation("Configuration auto-reloaded from file change");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to auto-reload configuration: {ex.Message}");
        }
    }

    private static string GetDefaultConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "config.json");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _fileWatcher?.Dispose();
            _lock?.Dispose();
            _disposed = true;
        }
    }
}