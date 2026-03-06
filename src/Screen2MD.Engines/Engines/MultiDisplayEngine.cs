using Screen2MD.Engines.Interfaces;
using Screen2MD.Kernel.Interfaces;
using System.Runtime.InteropServices;

namespace Screen2MD.Engines.Engines;

/// <summary>
/// 多显示器引擎
/// </summary>
public sealed class MultiDisplayEngine : IMultiDisplayEngine, IDisposable
{
    private bool _disposed;
    private readonly List<DisplayInfo> _displays = new();
    private readonly object _lock = new();

    #region Windows API

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    #endregion

    /// <inheritsdoc />
    public string Name => nameof(MultiDisplayEngine);

    /// <inheritsdoc />
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    /// <inheritsdoc />
    public IReadOnlyList<DisplayInfo> Displays
    {
        get
        {
            lock (_lock)
            {
                return _displays.ToList().AsReadOnly();
            }
        }
    }

    /// <inheritsdoc />
    public DisplayInfo? PrimaryDisplay
    {
        get
        {
            lock (_lock)
            {
                return _displays.FirstOrDefault(d => d.IsPrimary);
            }
        }
    }

    /// <summary>
    /// 初始化多显示器引擎
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            RefreshDisplayInfo();
            HealthStatus = _displays.Count > 0 ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            return Task.CompletedTask;
        }
        catch (Exception)
        {
            HealthStatus = HealthStatus.Unhealthy;
            throw;
        }
    }

    /// <inheritsdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritsdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// 刷新显示器信息
    /// </summary>
    public void RefreshDisplayInfo()
    {
        lock (_lock)
        {
            _displays.Clear();
            int index = 0;

            MonitorEnumProc callback = delegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
            {
                var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };
                
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    var display = new DisplayInfo
                    {
                        Index = index,
                        Handle = hMonitor,
                        DeviceName = mi.szDevice,
                        Bounds = new Rectangle(
                            mi.rcMonitor.Left,
                            mi.rcMonitor.Top,
                            mi.rcMonitor.Right - mi.rcMonitor.Left,
                            mi.rcMonitor.Bottom - mi.rcMonitor.Top),
                        WorkArea = new Rectangle(
                            mi.rcWork.Left,
                            mi.rcWork.Top,
                            mi.rcWork.Right - mi.rcWork.Left,
                            mi.rcWork.Bottom - mi.rcWork.Top),
                        IsPrimary = (mi.dwFlags & 0x00000001) != 0
                    };
                    
                    _displays.Add(display);
                    index++;
                }
                
                return true;
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        }
    }

    /// <inheritsdoc />
    public DisplayInfo? GetDisplayByIndex(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < _displays.Count)
            {
                return _displays[index];
            }
            return null;
        }
    }

    /// <inheritsdoc />
    public DisplayInfo? GetDisplayByPoint(int x, int y)
    {
        lock (_lock)
        {
            return _displays.FirstOrDefault(d => 
                x >= d.Bounds.X && 
                x < d.Bounds.X + d.Bounds.Width &&
                y >= d.Bounds.Y && 
                y < d.Bounds.Y + d.Bounds.Height);
        }
    }

    /// <inheritsdoc />
    public DisplayInfo? GetDisplayByWindow(IntPtr hwnd)
    {
        IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        
        lock (_lock)
        {
            return _displays.FirstOrDefault(d => d.Handle == hMonitor);
        }
    }

    /// <inheritsdoc />
    public Rectangle GetVirtualScreenBounds()
    {
        lock (_lock)
        {
            if (_displays.Count == 0)
            {
                return new Rectangle(0, 0, 1920, 1080); // 默认
            }

            int minX = _displays.Min(d => d.Bounds.X);
            int minY = _displays.Min(d => d.Bounds.Y);
            int maxX = _displays.Max(d => d.Bounds.X + d.Bounds.Width);
            int maxY = _displays.Max(d => d.Bounds.Y + d.Bounds.Height);

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }
    }

    /// <inheritsdoc />
    public event EventHandler<DisplayChangedEventArgs>? DisplayChanged;

    /// <summary>
    /// 触发显示器变化事件
    /// </summary>
    private void OnDisplayChanged(DisplayChangedEventArgs e)
    {
        DisplayChanged?.Invoke(this, e);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                _displays.Clear();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// 多显示器引擎接口
/// </summary>
public interface IMultiDisplayEngine : IKernelComponent
{
    /// <summary>
    /// 所有显示器列表
    /// </summary>
    IReadOnlyList<DisplayInfo> Displays { get; }

    /// <summary>
    /// 主显示器
    /// </summary>
    DisplayInfo? PrimaryDisplay { get; }

    /// <summary>
    /// 根据索引获取显示器
    /// </summary>
    DisplayInfo? GetDisplayByIndex(int index);

    /// <summary>
    /// 根据坐标点获取显示器
    /// </summary>
    DisplayInfo? GetDisplayByPoint(int x, int y);

    /// <summary>
    /// 根据窗口句柄获取显示器
    /// </summary>
    DisplayInfo? GetDisplayByWindow(IntPtr hwnd);

    /// <summary>
    /// 获取虚拟屏幕边界（所有显示器的并集）
    /// </summary>
    Rectangle GetVirtualScreenBounds();

    /// <summary>
    /// 刷新显示器信息
    /// </summary>
    void RefreshDisplayInfo();

    /// <summary>
    /// 显示器配置变化事件
    /// </summary>
    event EventHandler<DisplayChangedEventArgs>? DisplayChanged;
}

/// <summary>
/// 显示器信息
/// </summary>
public class DisplayInfo
{
    /// <summary>
    /// 显示器索引
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 显示器句柄
    /// </summary>
    public IntPtr Handle { get; set; }

    /// <summary>
    /// 设备名称
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// 显示器边界（包含任务栏）
    /// </summary>
    public Rectangle Bounds { get; set; }

    /// <summary>
    /// 工作区域（不包含任务栏）
    /// </summary>
    public Rectangle WorkArea { get; set; }

    /// <summary>
    /// 是否为主显示器
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// DPI缩放比例
    /// </summary>
    public float DpiScale { get; set; } = 1.0f;

    /// <summary>
    /// 分辨率描述
    /// </summary>
    public string Resolution => $"{Bounds.Width}x{Bounds.Height}";
}

/// <summary>
/// 显示器变化事件参数
/// </summary>
public class DisplayChangedEventArgs : EventArgs
{
    /// <summary>
    /// 变化类型
    /// </summary>
    public DisplayChangeType ChangeType { get; set; }

    /// <summary>
    /// 受影响的显示器
    /// </summary>
    public DisplayInfo? Display { get; set; }
}

/// <summary>
/// 显示器变化类型
/// </summary>
public enum DisplayChangeType
{
    /// <summary>
    /// 添加显示器
    /// </summary>
    Added,

    /// <summary>
    /// 移除显示器
    /// </summary>
    Removed,

    /// <summary>
    /// 分辨率变化
    /// </summary>
    ResolutionChanged,

    /// <summary>
    /// 位置变化
    /// </summary>
    PositionChanged
}
