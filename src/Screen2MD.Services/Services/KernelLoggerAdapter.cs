using Screen2MD.Kernel.Interfaces;

namespace Screen2MD.Services.Services;

/// <summary>
/// 将 Services.IKernelLogger 适配为 Kernel.Interfaces.IKernelLogger
/// </summary>
public sealed class KernelLoggerAdapter : Kernel.Interfaces.IKernelLogger
{
    private readonly IKernelLogger _innerLogger;

    public KernelLoggerAdapter(IKernelLogger innerLogger)
    {
        _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
    }

    public void LogTrace(string message, Exception? exception = null)
    {
        _innerLogger.LogTrace(message);
    }

    public void LogDebug(string message, Exception? exception = null)
    {
        _innerLogger.LogDebug(message);
    }

    public void LogInformation(string message, Exception? exception = null)
    {
        _innerLogger.LogInformation(message);
    }

    public void LogWarning(string message, Exception? exception = null)
    {
        _innerLogger.LogWarning(message);
    }

    public void LogError(string message, Exception? exception = null)
    {
        if (exception != null)
            _innerLogger.LogError(message, exception);
        else
            _innerLogger.LogError(message);
    }

    public void LogFatal(string message, Exception? exception = null)
    {
        if (exception != null)
            _innerLogger.LogFatal(message, exception);
        else
            _innerLogger.LogFatal(message);
    }
}