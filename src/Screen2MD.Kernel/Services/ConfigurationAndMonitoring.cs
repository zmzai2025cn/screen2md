using Screen2MD.Kernel.Interfaces;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Screen2MD.Kernel.Services;

/// <summary>
/// 配置管理器 - 支持热重载和变更通知
/// </summary>
public sealed class ConfigurationManager : IConfigurationManager, IDisposable
{
    private readonly string _configPath;
    private readonly Dictionary<string, object> _configValues = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly FileSystemWatcher? _watcher;
    private DateTime _lastWriteTime;
    private bool _disposed;

    public string Name => nameof(ConfigurationManager);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    public ConfigurationManager(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD",
            "config.json");

        // 确保目录存在
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);

        // 初始化默认配置
        InitializeDefaults();

        // 加载现有配置
        LoadFromFile();

        // 设置文件监控
        if (File.Exists(_configPath))
        {
            var directory = Path.GetDirectoryName(_configPath)!;
            var filename = Path.GetFileName(_configPath);
            
            _watcher = new FileSystemWatcher(directory, filename)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Changed += OnConfigFileChanged;
            _watcher.EnableRaisingEvents = true;
        }
    }

    private void InitializeDefaults()
    {
        // 采集配置
        SetValueInternal("capture.interval_seconds", 5);
        SetValueInternal("capture.adaptive", true);
        SetValueInternal("capture.on_change_only", true);
        SetValueInternal("capture.min_interval_seconds", 2);
        SetValueInternal("capture.max_interval_seconds", 60);

        // 隐私配置
        SetValueInternal("privacy.block_passwords", true);
        SetValueInternal("privacy.sensitive_keywords", new[] { "密码", "身份证号", "银行卡" });
        SetValueInternal("privacy.auto_delete_days", 30);

        // 存储配置
        SetValueInternal("storage.local.enabled", true);
        SetValueInternal("storage.local.path", @"C:\Screen2MD\Data");
        SetValueInternal("storage.local.retention_days", 30);
        SetValueInternal("storage.timeseries.enabled", false);
        SetValueInternal("storage.timeseries.url", "http://localhost:8086");
        SetValueInternal("storage.timeseries.database", "screen2md");

        // 资源限制
        SetValueInternal("limits.max_cpu_percent", 1.0);
        SetValueInternal("limits.max_memory_mb", 50);
        SetValueInternal("limits.max_disk_mb_per_day", 100);

        // Web配置
        SetValueInternal("web.enabled", true);
        SetValueInternal("web.port", 9999);
        SetValueInternal("web.bind", "127.0.0.1");
    }

    private void LoadFromFile()
    {
        if (!File.Exists(_configPath))
            return;

        try
        {
            var json = File.ReadAllText(_configPath);
            var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            
            if (values != null)
            {
                foreach (var (key, element) in values)
                {
                    var value = ConvertJsonElement(element);
                    if (value != null)
                    {
                        SetValueInternal(key, value);
                    }
                }
            }

            _lastWriteTime = File.GetLastWriteTime(_configPath);
        }
        catch (Exception ex)
        {
            // 加载失败时使用默认配置
            Console.WriteLine($"Failed to load config: {ex.Message}");
        }
    }

    private object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(e => e.GetString()).ToArray(),
            JsonValueKind.Object => element,
            _ => null
        };
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // 防抖处理
        var writeTime = File.GetLastWriteTime(_configPath);
        if (writeTime == _lastWriteTime)
            return;

        _lastWriteTime = writeTime;
        
        // 延迟加载避免文件锁定
        Task.Delay(100).ContinueWith(_ => ReloadAsync());
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Healthy;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public T? GetValue<T>(string key, T? defaultValue = default)
    {
        _lock.EnterReadLock();
        try
        {
            if (_configValues.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
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
            var oldValue = _configValues.TryGetValue(key, out var existing) ? existing : null;
            SetValueInternal(key, value!);
            
            // 触发变更事件
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
            {
                Key = key,
                OldValue = oldValue,
                NewValue = value,
                ChangedAt = DateTimeOffset.UtcNow
            });
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // 异步保存到文件
        _ = SaveToFileAsync();
    }

    private void SetValueInternal(string key, object value)
    {
        _configValues[key] = value;
    }

    public async Task ReloadAsync()
    {
        var oldValues = new Dictionary<string, object>(_configValues);
        
        LoadFromFile();
        
        // 找出变更的键
        foreach (var key in _configValues.Keys.Union(oldValues.Keys))
        {
            var oldValue = oldValues.TryGetValue(key, out var ov) ? ov : null;
            var newValue = _configValues.TryGetValue(key, out var nv) ? nv : null;
            
            if (!Equals(oldValue, newValue))
            {
                ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
                {
                    Key = key,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task SaveToFileAsync()
    {
        try
        {
            _lock.EnterReadLock();
            Dictionary<string, object> valuesToSave;
            try
            {
                valuesToSave = new Dictionary<string, object>(_configValues);
            }
            finally
            {
                _lock.ExitReadLock();
            }

            var json = JsonSerializer.Serialize(valuesToSave, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            // 原子写入
            var tempPath = _configPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _configPath, true);
            
            _lastWriteTime = File.GetLastWriteTime(_configPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _watcher?.Dispose();
        _lock?.Dispose();
    }
}

/// <summary>
/// 资源监控器 - 确保CPU<1%, 内存<50MB
/// </summary>
public sealed class ResourceMonitor : IResourceMonitor, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _monitorTask;
    private readonly List<ResourceSnapshot> _history = new();
    private readonly object _historyLock = new();
    private readonly double _cpuAlertThreshold;
    private readonly double _memoryAlertThreshold;
    
    private Process? _currentProcess;
    private TimeSpan _lastCpuTime;
    private DateTime _lastCheckTime;

    public string Name => nameof(ResourceMonitor);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;
    public double CpuUsagePercent { get; private set; }
    public double MemoryUsageMB { get; private set; }

    public event EventHandler<ResourceAlertEventArgs>? ResourceAlert;

    public ResourceMonitor(IConfigurationManager? config = null)
    {
        _cpuAlertThreshold = config?.GetValue("limits.max_cpu_percent", 1.0) ?? 1.0;
        _memoryAlertThreshold = config?.GetValue("limits.max_memory_mb", 50.0) ?? 50.0;
        
        _currentProcess = Process.GetCurrentProcess();
        _lastCpuTime = _currentProcess.TotalProcessorTime;
        _lastCheckTime = DateTime.UtcNow;
        
        _monitorTask = Task.Run(MonitorLoopAsync);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Healthy;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Unhealthy;
        return Task.CompletedTask;
    }

    private async Task MonitorLoopAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token);
                CheckResources();
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
    }

    private void CheckResources()
    {
        if (_currentProcess == null)
            return;

        _currentProcess.Refresh();

        // 计算CPU使用率
        var currentCpuTime = _currentProcess.TotalProcessorTime;
        var currentTime = DateTime.UtcNow;
        
        var cpuTimeDelta = currentCpuTime - _lastCpuTime;
        var timeDelta = currentTime - _lastCheckTime;
        
        if (timeDelta.TotalSeconds > 0)
        {
            // CPU时间 / (时间 * 核心数) * 100
            var cpuPercent = (cpuTimeDelta.TotalSeconds / 
                (timeDelta.TotalSeconds * Environment.ProcessorCount)) * 100;
            
            CpuUsagePercent = Math.Round(cpuPercent, 2);
        }

        _lastCpuTime = currentCpuTime;
        _lastCheckTime = currentTime;

        // 获取内存使用 (Linux/Windows 兼容)
        var memoryBytes = GetMemoryUsageBytes();
        MemoryUsageMB = Math.Round(memoryBytes / (1024.0 * 1024.0), 2);

        // 记录历史
        lock (_historyLock)
        {
            _history.Add(new ResourceSnapshot
            {
                Timestamp = currentTime,
                CpuPercent = CpuUsagePercent,
                MemoryMB = MemoryUsageMB
            });

            // 保留最近1小时的数据
            var cutoff = currentTime.AddHours(-1);
            _history.RemoveAll(h => h.Timestamp < cutoff);
        }

        // 检查告警
        if (CpuUsagePercent > _cpuAlertThreshold)
        {
            ResourceAlert?.Invoke(this, new ResourceAlertEventArgs
            {
                ResourceType = ResourceType.Cpu,
                CurrentValue = CpuUsagePercent,
                Threshold = _cpuAlertThreshold,
                AlertAt = DateTimeOffset.UtcNow
            });
        }

        if (MemoryUsageMB > _memoryAlertThreshold)
        {
            ResourceAlert?.Invoke(this, new ResourceAlertEventArgs
            {
                ResourceType = ResourceType.Memory,
                CurrentValue = MemoryUsageMB,
                Threshold = _memoryAlertThreshold,
                AlertAt = DateTimeOffset.UtcNow
            });
        }
    }

    public ResourceUsageReport GetReport(TimeSpan? period = null)
    {
        var lookback = period ?? TimeSpan.FromMinutes(5);
        var cutoff = DateTime.UtcNow - lookback;

        lock (_historyLock)
        {
            var relevant = _history.Where(h => h.Timestamp >= cutoff).ToList();
            
            if (!relevant.Any())
            {
                return new ResourceUsageReport
                {
                    GeneratedAt = DateTimeOffset.UtcNow,
                    Period = lookback,
                    AvgCpuPercent = 0,
                    MaxCpuPercent = 0,
                    AvgMemoryMB = 0,
                    MaxMemoryMB = 0
                };
            }

            return new ResourceUsageReport
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                Period = lookback,
                AvgCpuPercent = Math.Round(relevant.Average(h => h.CpuPercent), 2),
                MaxCpuPercent = Math.Round(relevant.Max(h => h.CpuPercent), 2),
                AvgMemoryMB = Math.Round(relevant.Average(h => h.MemoryMB), 2),
                MaxMemoryMB = Math.Round(relevant.Max(h => h.MemoryMB), 2)
            };
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _monitorTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch { }
        
        _cts.Dispose();
        _currentProcess?.Dispose();
    }

    private double GetMemoryUsageBytes()
    {
        try
        {
            // 首选: Process.WorkingSet64 (Windows)
            var workingSet = _currentProcess?.WorkingSet64 ?? 0;
            if (workingSet > 0)
                return workingSet;
            
            // 备选: GC.GetTotalMemory (跨平台)
            var gcMemory = GC.GetTotalMemory(false);
            if (gcMemory > 0)
                return gcMemory;
            
            // 最后尝试: /proc/self/status (Linux)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    var status = File.ReadAllText("/proc/self/status");
                    var match = System.Text.RegularExpressions.Regex.Match(status, @"VmRSS:\s*(\d+)\s*kB");
                    if (match.Success)
                    {
                        return long.Parse(match.Groups[1].Value) * 1024;
                    }
                }
                catch { }
            }
            
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private class ResourceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double CpuPercent { get; set; }
        public double MemoryMB { get; set; }
    }
}