using Screen2MD.Kernel.Interfaces;
using Screen2MD.UI.Interfaces;

namespace Screen2MD.UI.Services;

/// <summary>
/// 系统托盘服务实现 - Windows NotifyIcon 封装
/// 注意：Linux 环境下为模拟实现
/// </summary>
public sealed class SystemTrayService : ISystemTrayService, IDisposable
{
    private bool _isPaused;
    private bool _disposed;

    public string Name => nameof(SystemTrayService);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public event EventHandler? IconDoubleClicked;
    public event EventHandler? PauseClicked;
    public event EventHandler? ExitClicked;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Healthy;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ShowIcon();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        HideIcon();
        return Task.CompletedTask;
    }

    public void ShowIcon()
    {
        // Windows: notifyIcon.Visible = true;
        // Linux: 模拟实现
    }

    public void HideIcon()
    {
        // Windows: notifyIcon.Visible = false;
    }

    public void ShowBalloonTip(string title, string message, int timeout = 3000)
    {
        // Windows: notifyIcon.ShowBalloonTip(timeout, title, message, ToolTipIcon.Info);
        // Linux: 可调用 notify-send 或其他桌面通知
    }

    public void SetPausedState(bool isPaused)
    {
        _isPaused = isPaused;
        // Windows: 更新托盘图标（暂停/运行状态）
    }

    // 模拟触发事件
    public void SimulateIconDoubleClick() => IconDoubleClicked?.Invoke(this, EventArgs.Empty);
    public void SimulatePauseClick() => PauseClicked?.Invoke(this, EventArgs.Empty);
    public void SimulateExitClick() => ExitClicked?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (!_disposed)
        {
            HideIcon();
            _disposed = true;
        }
    }
}
