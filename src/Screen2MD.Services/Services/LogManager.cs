using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Services.Services;

/// <summary>
/// 文件日志记录器 - 实现 Services.IKernelLogger
/// </summary>
public sealed class FileLogger : Screen2MD.Services.IKernelLogger
{
    private readonly string _category;
    private readonly string _logDirectory;
    private readonly int _maxFileSizeMB;
    private readonly int _maxFiles;
    private readonly object _lock = new();
    private string _currentLogFile = "";
    private DateTime _currentDate;

    public FileLogger(string category, string logDirectory, int maxFileSizeMB = 100, int maxFiles = 10)
    {
        _category = category ?? "Default";
        _logDirectory = logDirectory;
        _maxFileSizeMB = maxFileSizeMB;
        _maxFiles = maxFiles;
        _currentDate = DateTime.MinValue;

        Directory.CreateDirectory(_logDirectory);
        EnsureLogFile();
    }

    private void EnsureLogFile()
    {
        var today = DateTime.Now.Date;
        if (_currentDate != today || string.IsNullOrEmpty(_currentLogFile))
        {
            _currentDate = today;
            _currentLogFile = Path.Combine(
                _logDirectory,
                $"screen2md_{today:yyyyMMdd}.log");
        }
    }

    private void WriteLog(string level, string message, Exception? exception = null)
    {
        lock (_lock)
        {
            EnsureLogFile();

            var sb = new System.Text.StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(" [");
            sb.Append(level.PadRight(5));
            sb.Append("] ");
            sb.Append("[");
            sb.Append(_category);
            sb.Append("] ");
            sb.Append(message);

            if (exception != null)
            {
                sb.AppendLine();
                sb.Append("Exception: ");
                sb.Append(exception.GetType().Name);
                sb.Append(" - ");
                sb.Append(exception.Message);
            }

            var logEntry = sb.ToString();
            
            try
            {
                // 检查文件大小，需要轮转
                if (File.Exists(_currentLogFile))
                {
                    var fileInfo = new FileInfo(_currentLogFile);
                    if (fileInfo.Length > _maxFileSizeMB * 1024 * 1024)
                    {
                        RotateLogFile();
                    }
                }

                File.AppendAllText(_currentLogFile, logEntry + Environment.NewLine);
            }
            catch
            {
                // 日志写入失败不应影响程序运行
            }
        }
    }

    private void RotateLogFile()
    {
        var baseFileName = Path.GetFileNameWithoutExtension(_currentLogFile);
        var extension = Path.GetExtension(_currentLogFile);
        var directory = Path.GetDirectoryName(_currentLogFile) ?? _logDirectory;

        // 移动现有备份
        for (int i = _maxFiles - 1; i >= 1; i--)
        {
            var oldFile = Path.Combine(directory, $"{baseFileName}.{i}{extension}");
            var newFile = Path.Combine(directory, $"{baseFileName}.{i + 1}{extension}");

            if (File.Exists(oldFile))
            {
                if (i == _maxFiles - 1)
                {
                    File.Delete(oldFile);
                }
                else
                {
                    File.Move(oldFile, newFile, overwrite: true);
                }
            }
        }

        // 移动当前文件
        var backupFile = Path.Combine(directory, $"{baseFileName}.1{extension}");
        File.Move(_currentLogFile, backupFile, overwrite: true);
    }

    public void LogTrace(string message) => WriteLog("TRACE", message);
    public void LogDebug(string message) => WriteLog("DEBUG", message);
    public void LogInformation(string message) => WriteLog("INFO", message);
    public void LogWarning(string message) => WriteLog("WARN", message);
    public void LogError(string message) => WriteLog("ERROR", message);
    public void LogFatal(string message) => WriteLog("FATAL", message);
    public void LogError(string message, Exception? exception) => WriteLog("ERROR", message, exception);
    public void LogFatal(string message, Exception? exception) => WriteLog("FATAL", message, exception);
}

/// <summary>
/// 复合日志记录器（同时输出到多个目标）
/// </summary>
public sealed class CompositeLogger : Screen2MD.Services.IKernelLogger
{
    private readonly List<Screen2MD.Services.IKernelLogger> _loggers;

    public CompositeLogger(params Screen2MD.Services.IKernelLogger[] loggers)
    {
        _loggers = loggers.ToList();
    }

    public void AddLogger(Screen2MD.Services.IKernelLogger logger)
    {
        _loggers.Add(logger);
    }

    public void LogTrace(string message)
    {
        foreach (var logger in _loggers) logger.LogTrace(message);
    }

    public void LogDebug(string message)
    {
        foreach (var logger in _loggers) logger.LogDebug(message);
    }

    public void LogInformation(string message)
    {
        foreach (var logger in _loggers) logger.LogInformation(message);
    }

    public void LogWarning(string message)
    {
        foreach (var logger in _loggers) logger.LogWarning(message);
    }

    public void LogError(string message)
    {
        foreach (var logger in _loggers) logger.LogError(message);
    }

    public void LogFatal(string message)
    {
        foreach (var logger in _loggers) logger.LogFatal(message);
    }

    public void LogError(string message, Exception? exception)
    {
        foreach (var logger in _loggers) logger.LogError(message, exception);
    }

    public void LogFatal(string message, Exception? exception)
    {
        foreach (var logger in _loggers) logger.LogFatal(message, exception);
    }
}

/// <summary>
/// 日志级别过滤装饰器
/// </summary>
public sealed class LevelFilterLogger : Screen2MD.Services.IKernelLogger
{
    private readonly Screen2MD.Services.IKernelLogger _innerLogger;
    private readonly LogLevel _minLevel;

    public LevelFilterLogger(Screen2MD.Services.IKernelLogger innerLogger, LogLevel minLevel)
    {
        _innerLogger = innerLogger;
        _minLevel = minLevel;
    }

    public void LogTrace(string message)
    {
        if (_minLevel <= LogLevel.Trace) _innerLogger.LogTrace(message);
    }

    public void LogDebug(string message)
    {
        if (_minLevel <= LogLevel.Debug) _innerLogger.LogDebug(message);
    }

    public void LogInformation(string message)
    {
        if (_minLevel <= LogLevel.Information) _innerLogger.LogInformation(message);
    }

    public void LogWarning(string message)
    {
        if (_minLevel <= LogLevel.Warning) _innerLogger.LogWarning(message);
    }

    public void LogError(string message)
    {
        if (_minLevel <= LogLevel.Error) _innerLogger.LogError(message);
    }

    public void LogFatal(string message)
    {
        if (_minLevel <= LogLevel.Fatal) _innerLogger.LogFatal(message);
    }

    public void LogError(string message, Exception? exception)
    {
        if (_minLevel <= LogLevel.Error) _innerLogger.LogError(message, exception);
    }

    public void LogFatal(string message, Exception? exception)
    {
        if (_minLevel <= LogLevel.Fatal) _innerLogger.LogFatal(message, exception);
    }
}