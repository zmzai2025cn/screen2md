using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Screen2MD.Services.Interfaces;
using System.Collections.ObjectModel;

namespace Screen2MD.UI.ViewModels;

/// <summary>
/// 主窗口 ViewModel - 管理控制台核心
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IStorageService _storageService;
    private readonly ICaptureScheduler _scheduler;

    [ObservableProperty]
    private string _windowTitle = "Screen2MD Enterprise - 管理控制台";

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private int _todayCaptureCount;

    [ObservableProperty]
    private int _totalCaptureCount;

    [ObservableProperty]
    private long _totalStorageSize;

    [ObservableProperty]
    private ObservableCollection<CaptureRecordViewModel> _recentCaptures = new();

    [ObservableProperty]
    private ObservableCollection<SoftwareStatisticsViewModel> _softwareStatistics = new();

    public MainViewModel(IStorageService storageService, ICaptureScheduler scheduler)
    {
        _storageService = storageService;
        _scheduler = scheduler;
        
        // 初始化命令
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
        PauseResumeCommand = new AsyncRelayCommand(TogglePauseAsync);
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand PauseResumeCommand { get; }
    public IRelayCommand ExitCommand { get; }

    public event EventHandler? ExitRequested;

    /// <summary>
    /// 加载统计数据
    /// </summary>
    public async Task LoadDataAsync()
    {
        try
        {
            StatusText = "加载中...";
            
            // 加载统计
            var stats = await _storageService.GetStatisticsAsync();
            TotalCaptureCount = stats.TotalRecords;
            TotalStorageSize = stats.TotalSize;
            
            // 加载今日统计
            var today = DateTimeOffset.UtcNow.Date;
            var todayQuery = new CaptureQuery 
            { 
                StartTime = today,
                EndTime = today.AddDays(1),
                PageSize = 1 
            };
            var todayResult = await _storageService.QueryAsync(todayQuery);
            TodayCaptureCount = todayResult.TotalCount;
            
            // 加载最近记录
            var recentQuery = new CaptureQuery { PageSize = 10 };
            var recentResult = await _storageService.QueryAsync(recentQuery);
            RecentCaptures.Clear();
            foreach (var record in recentResult.Items)
            {
                RecentCaptures.Add(new CaptureRecordViewModel(record));
            }
            
            // 加载软件统计
            SoftwareStatistics.Clear();
            foreach (var kv in stats.CountBySoftwareType)
            {
                SoftwareStatistics.Add(new SoftwareStatisticsViewModel
                {
                    SoftwareType = kv.Key,
                    Count = kv.Value
                });
            }
            
            StatusText = $"最后更新: {DateTimeOffset.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText = $"错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 切换暂停/恢复
    /// </summary>
    private async Task TogglePauseAsync()
    {
        if (IsCapturing)
        {
            await _scheduler.PauseAsync();
            IsCapturing = false;
        }
        else
        {
            await _scheduler.ResumeAsync();
            IsCapturing = true;
        }
    }

    /// <summary>
    /// 初始化
    /// </summary>
    public async Task InitializeAsync()
    {
        IsCapturing = true;
        await LoadDataAsync();
    }
}

/// <summary>
/// 采集记录 ViewModel
/// </summary>
public class CaptureRecordViewModel
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; set; }
    public string SoftwareType { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public bool IsCodeContent { get; set; }
    public bool IsUploaded { get; set; }
    public string DisplayText { get; set; } = string.Empty;

    public CaptureRecordViewModel(CaptureRecord record)
    {
        Id = record.Id;
        CapturedAt = record.CapturedAt;
        SoftwareType = record.SoftwareType;
        ProcessName = record.ProcessName;
        WindowTitle = record.WindowTitle;
        IsCodeContent = record.IsCodeContent;
        IsUploaded = record.IsUploaded;
        DisplayText = $"[{SoftwareType}] {WindowTitle}";
    }
}

/// <summary>
/// 软件统计 ViewModel
/// </summary>
public class SoftwareStatisticsViewModel
{
    public string SoftwareType { get; set; } = string.Empty;
    public int Count { get; set; }
    public string DisplayText => $"{SoftwareType}: {Count}";
}
