using Screen2MD.Kernel.Interfaces;
using System.Collections.Concurrent;

namespace Screen2MD.Kernel.Services;

public sealed class LogManager : ILogManager, IDisposable
{
    private readonly string _logDirectory;
    private readonly ConcurrentQueue<LogEntry> _logQueue = new();
    private readonly List<ILogSink> _sinks = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _flushTask;
    private readonly LogLevel _minLevel;
    private bool _disposed;

    public string Name => nameof(LogManager);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public LogManager(LogConfiguration? config = null)
    {
        var configuration = config ?? new LogConfiguration();
        _logDirectory = configuration.LogDirectory 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Screen2MD", "Logs");
        _minLevel = configuration.MinimumLevel;
        
        Directory.CreateDirectory(_logDirectory);
        InitializeSinks(configuration);
        _flushTask = Task.Run(FlushLoopAsync);
        HealthStatus = HealthStatus.Healthy;
    }

    private void InitializeSinks(LogConfiguration config)
    {
        if (config.EnableFileLogging)
            _sinks.Add(new FileLogSink(_logDirectory, config.MaxFileSizeMB, config.MaxFileCount));
        if (config.EnableConsoleLogging)
            _sinks.Add(new ConsoleLogSink());
        _sinks.Add(new MemoryRingBufferSink(config.MemoryBufferSize));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogInternal(LogLevel.Information, nameof(LogManager), "LogManager initialized", null);
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Healthy;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Unhealthy;
        FlushAsync().Wait(cancellationToken);
        return Task.CompletedTask;
    }

    public IKernelLogger GetLogger(string category)
    {
        return new CategoryLogger(this, category);
    }

    internal void LogInternal(LogLevel level, string category, string message, Exception? exception, string? correlationId = null)
    {
        if (level < _minLevel || _disposed)
            return;

        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Category = category,
            Message = message,
            Exception = exception,
            CorrelationId = correlationId
        };

        _logQueue.Enqueue(entry);
        
        if (level >= LogLevel.Error)
            _ = TriggerFlushAsync();
    }

    private async Task FlushLoopAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
                await FlushAsync();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(_logDirectory, "logmanager_error.txt"),
                    $"[{DateTime.Now}] LogManager error: {ex}\n");
            }
            catch
            {
                Console.Error.WriteLine($"LogManager critical error: {ex}");
            }
        }
    }

    private async Task FlushAsync()
    {
        if (_logQueue.IsEmpty)
            return;

        var batch = new List<LogEntry>();
        while (_logQueue.TryDequeue(out var entry))
            batch.Add(entry);

        if (batch.Count == 0)
            return;

        var tasks = _sinks.Select(sink => 
            Task.Run(async () =>
            {
                try { await sink.WriteBatchAsync(batch); }
                catch (Exception ex) { Console.Error.WriteLine($"Log sink error: {ex.Message}"); }
            })).ToArray();

        await Task.WhenAll(tasks);
    }

    private Task TriggerFlushAsync()
    {
        return FlushAsync();
    }

    public IEnumerable<LogEntry> GetRecentLogs(int count = 100)
    {
        var memorySink = _sinks.OfType<MemoryRingBufferSink>().FirstOrDefault();
        return memorySink?.GetRecent(count) ?? Enumerable.Empty<LogEntry>();
    }

    public IEnumerable<LogEntry> GetLogsByLevel(LogLevel level, DateTimeOffset? since = null)
    {
        var memorySink = _sinks.OfType<MemoryRingBufferSink>().FirstOrDefault();
        return memorySink?.GetByLevel(level, since) ?? Enumerable.Empty<LogEntry>();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        
        try { _flushTask.Wait(TimeSpan.FromSeconds(10)); } catch { }
        FlushAsync().Wait(TimeSpan.FromSeconds(5));

        foreach (var sink in _sinks)
        {
            try { sink?.Dispose(); } catch { }
        }
        _cts.Dispose();
    }

    private class CategoryLogger : IKernelLogger
    {
        private readonly LogManager _manager;
        private readonly string _category;

        public CategoryLogger(LogManager manager, string category)
        {
            _manager = manager;
            _category = category;
        }

        public void LogTrace(string message, Exception? exception = null) => 
            _manager.LogInternal(LogLevel.Trace, _category, message, exception);
        public void LogDebug(string message, Exception? exception = null) => 
            _manager.LogInternal(LogLevel.Debug, _category, message, exception);
        public void LogInformation(string message, Exception? exception = null) => 
            _manager.LogInternal(LogLevel.Information, _category, message, exception);
        public void LogWarning(string message, Exception? exception = null) => 
            _manager.LogInternal(LogLevel.Warning, _category, message, exception);
        public void LogError(string message, Exception? exception = null) => 
            _manager.LogInternal(LogLevel.Error, _category, message, exception);
        public void LogFatal(string message, Exception? exception = null) => 
            _manager.LogInternal(LogLevel.Fatal, _category, message, exception);
    }
}

public interface ILogSink : IDisposable
{
    Task WriteBatchAsync(IEnumerable<LogEntry> entries);
}

public class FileLogSink : ILogSink
{
    private readonly string _logDirectory;
    private readonly int _maxFileSizeMB;
    private readonly int _maxFileCount;
    private readonly object _lock = new();
    private string? _currentFile;
    private long _currentFileSize;

    public FileLogSink(string logDirectory, int maxFileSizeMB = 10, int maxFileCount = 10)
    {
        _logDirectory = logDirectory;
        _maxFileSizeMB = maxFileSizeMB;
        _maxFileCount = maxFileCount;
        EnsureCurrentFile();
    }

    private void EnsureCurrentFile()
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        _currentFile = Path.Combine(_logDirectory, $"screen2md_{date}.log");
        
        if (File.Exists(_currentFile))
            _currentFileSize = new FileInfo(_currentFile).Length;
        else
            _currentFileSize = 0;
    }

    public Task WriteBatchAsync(IEnumerable<LogEntry> entries)
    {
        lock (_lock)
        {
            EnsureCurrentFile();
            
            var lines = entries.Select(e => 
                $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{e.Level.ToString().ToUpperInvariant(),-5}] [{e.Category}] {e.Message}" +
                (e.Exception != null ? $"\n{e.Exception}" : "")).ToList();
            
            var content = string.Join("\n", lines) + "\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            
            if (_currentFileSize + bytes.Length > _maxFileSizeMB * 1024 * 1024)
            {
                RotateFiles();
                EnsureCurrentFile();
            }
            
            File.AppendAllText(_currentFile!, content);
            _currentFileSize += bytes.Length;
        }
        return Task.CompletedTask;
    }

    private void RotateFiles()
    {
        var files = Directory.GetFiles(_logDirectory, "screen2md_*.log")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        while (files.Count >= _maxFileCount)
        {
            try { files.Last().Delete(); files.RemoveAt(files.Count - 1); }
            catch { break; }
        }
    }

    public void Dispose() { }
}

public class ConsoleLogSink : ILogSink
{
    public Task WriteBatchAsync(IEnumerable<LogEntry> entries)
    {
        foreach (var e in entries)
        {
            var color = e.Level switch
            {
                LogLevel.Trace or LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Information => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error or LogLevel.Fatal => ConsoleColor.Red,
                _ => ConsoleColor.White
            };

            Console.ForegroundColor = color;
            Console.WriteLine($"[{e.Timestamp:HH:mm:ss}] [{e.Level}] [{e.Category}] {e.Message}");
            if (e.Exception != null) Console.WriteLine(e.Exception);
            Console.ResetColor();
        }
        return Task.CompletedTask;
    }

    public void Dispose() { }
}

public class MemoryRingBufferSink : ILogSink
{
    private readonly ConcurrentQueue<LogEntry> _buffer;
    private readonly int _maxSize;

    public MemoryRingBufferSink(int maxSize = 10000)
    {
        _maxSize = maxSize;
        _buffer = new ConcurrentQueue<LogEntry>();
    }

    public Task WriteBatchAsync(IEnumerable<LogEntry> entries)
    {
        foreach (var entry in entries)
        {
            _buffer.Enqueue(entry);
            while (_buffer.Count > _maxSize && _buffer.TryDequeue(out _)) { }
        }
        return Task.CompletedTask;
    }

    public IEnumerable<LogEntry> GetRecent(int count) => _buffer.TakeLast(count);
    public IEnumerable<LogEntry> GetByLevel(LogLevel level, DateTimeOffset? since = null)
    {
        var query = _buffer.Where(e => e.Level == level);
        if (since.HasValue) query = query.Where(e => e.Timestamp >= since.Value);
        return query;
    }

    public void Dispose() { }
}

public class LogConfiguration
{
    public string? LogDirectory { get; set; }
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public bool EnableFileLogging { get; set; } = true;
    public bool EnableConsoleLogging { get; set; } = false;
    public int MaxFileSizeMB { get; set; } = 10;
    public int MaxFileCount { get; set; } = 10;
    public int MemoryBufferSize { get; set; } = 10000;
}