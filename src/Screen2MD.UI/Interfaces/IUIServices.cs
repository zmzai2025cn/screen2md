using Screen2MD.Kernel.Interfaces;
using Screen2MD.Services.Interfaces;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Screen2MD.UI.Interfaces;

/// <summary>
/// 系统托盘服务接口
/// </summary>
public interface ISystemTrayService : IKernelComponent
{
    /// <summary>
    /// 显示托盘图标
    /// </summary>
    void ShowIcon();
    
    /// <summary>
    /// 隐藏托盘图标
    /// </summary>
    void HideIcon();
    
    /// <summary>
    /// 显示气球提示
    /// </summary>
    void ShowBalloonTip(string title, string message, int timeout = 3000);
    
    /// <summary>
    /// 设置暂停/恢复状态
    /// </summary>
    void SetPausedState(bool isPaused);
    
    /// </summary>
    /// 托盘图标双击事件
    /// </summary>
    event EventHandler? IconDoubleClicked;
    
    /// <summary>
    /// 暂停菜单点击事件
    /// </summary>
    event EventHandler? PauseClicked;
    
    /// <summary>
    /// 退出菜单点击事件
    /// </summary>
    event EventHandler? ExitClicked;
}

/// <summary>
/// 主窗口服务接口
/// </summary>
public interface IMainWindowService : IKernelComponent
{
    /// <summary>
    /// 显示主窗口
    /// </summary>
    void Show();
    
    /// <summary>
    /// 隐藏主窗口
    /// </summary>
    void Hide();
    
    /// <summary>
    /// 关闭主窗口
    /// </summary>
    void Close();
    
    /// <summary>
    /// 窗口是否可见
    /// </summary>
    bool IsVisible { get; }
    
    /// <summary>
    /// 激活窗口（前置）
    /// </summary>
    void Activate();
}
