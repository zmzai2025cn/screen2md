namespace Screen2MD.Services;

/// <summary>
/// 日志级别
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// 简化日志接口 - 兼容 Kernel.IKernelLogger
/// </summary>
public interface IKernelLogger
{
    void LogTrace(string message);
    void LogDebug(string message);
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogFatal(string message);
    
    // 带异常的重载
    void LogError(string message, Exception? exception);
    void LogFatal(string message, Exception? exception);
}

/// <summary>
/// 空日志实现（用于测试）
/// </summary>
public class NullLogger : IKernelLogger
{
    public static IKernelLogger Instance { get; } = new NullLogger();
    public void LogTrace(string message) { }
    public void LogDebug(string message) { }
    public void LogInformation(string message) { }
    public void LogWarning(string message) { }
    public void LogError(string message) { }
    public void LogFatal(string message) { }
    public void LogError(string message, Exception? exception) { }
    public void LogFatal(string message, Exception? exception) { }
}
