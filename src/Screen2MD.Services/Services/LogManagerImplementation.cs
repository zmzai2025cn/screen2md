using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Services.Services;

/// <summary>
/// 日志管理器实现
/// </summary>
public sealed class LogManager : ILogManager, IDisposable
{
    private readonly string _logDirectory;
    private readonly Dictionary<string, Kernel.Interfaces.IKernelLogger> _loggers = new();
    private readonly List<LogEntry> _recentLogs = new();
    private readonly int _maxRecentLogs;
    private readonly object _lock = new();

    public string Name => nameof(LogManager);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public LogManager(string? logDirectory = null, int maxRecentLogs = 1000)
    {
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "logs");
        _maxRecentLogs = maxRecentLogs;
        
        Directory.CreateDirectory(_logDirectory);
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

    public Kernel.Interfaces.IKernelLogger GetLogger(string category)
    {
        lock (_lock)
        {
            if (!_loggers.TryGetValue(category, out var logger))
            {
                var fileLogger = new FileLogger(category, _logDirectory);
                logger = new KernelLoggerAdapter(fileLogger);
                _loggers[category] = logger;
            }
            return logger;
        }
    }

    public IEnumerable<LogEntry> GetRecentLogs(int count = 100)
    {
        lock (_lock)
        {
            return _recentLogs.TakeLast(count).ToList();
        }
    }

    public IEnumerable<LogEntry> GetLogsByLevel(Kernel.Interfaces.LogLevel level, DateTimeOffset? since = null)
    {
        lock (_lock)
        {
            var query = _recentLogs.Where(l => l.Level == level);
            if (since.HasValue)
            {
                query = query.Where(l => l.Timestamp >= since.Value);
            }
            return query.ToList();
        }
    }

    public void AddRecentLog(LogEntry entry)
    {
        lock (_lock)
        {
            _recentLogs.Add(entry);
            if (_recentLogs.Count > _maxRecentLogs)
            {
                _recentLogs.RemoveAt(0);
            }
        }
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}