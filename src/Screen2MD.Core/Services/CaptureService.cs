using Screen2MD.Abstractions;
using Microsoft.Extensions.Logging;

namespace Screen2MD.Core.Services;

/// <summary>
/// 截图服务 - 跨平台业务逻辑
/// </summary>
public sealed class CaptureService : IDisposable
{
    private readonly IScreenCaptureEngine _captureEngine;
    private readonly IImageProcessor _imageProcessor;
    private readonly IWindowManager? _windowManager;
    private readonly ILogger<CaptureService>? _logger;
    private readonly string _outputDirectory;
    private readonly bool _captureAllDisplays;
    private readonly double _similarityThreshold;
    private readonly Dictionary<int, string> _lastCaptureHashes = new();
    private bool _disposed;

    public CaptureService(
        IScreenCaptureEngine captureEngine,
        IImageProcessor imageProcessor,
        IWindowManager? windowManager = null,
        string? outputDirectory = null,
        bool captureAllDisplays = true,
        double similarityThreshold = 0.95,
        ILogger<CaptureService>? logger = null)
    {
        _captureEngine = captureEngine ?? throw new ArgumentNullException(nameof(captureEngine));
        _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
        _windowManager = windowManager;
        _logger = logger;
        _captureAllDisplays = captureAllDisplays;
        _similarityThreshold = similarityThreshold;
        
        _outputDirectory = outputDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Screen2MD", "Captures");
        
        Directory.CreateDirectory(_outputDirectory);
    }

    /// <summary>
    /// 执行截图
    /// </summary>
    public async Task<CaptureResult> CaptureAsync(CaptureOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new CaptureOptions();
        var results = new List<CapturedFile>();
        
        try
        {
            var displays = await _captureEngine.GetDisplaysAsync(cancellationToken);
            var displaysToCapture = _captureAllDisplays 
                ? displays 
                : displays.Where(d => d.IsPrimary).ToList();

            foreach (var display in displaysToCapture)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var image = await _captureEngine.CaptureDisplayAsync(display.Index, cancellationToken);
                
                // 检查变化
                if (options.DetectChanges && !await HasSignificantChangeAsync(display.Index, image))
                {
                    _logger?.LogDebug($"Display {display.Index}: No significant change detected");
                    image.Dispose();
                    continue;
                }

                // 获取窗口信息
                IWindowInfo? windowInfo = null;
                if (_windowManager != null)
                {
                    var centerX = display.X + display.Width / 2;
                    var centerY = display.Y + display.Height / 2;
                    windowInfo = await _windowManager.GetWindowAtPointAsync(centerX, centerY);
                }

                // 保存文件
                var fileName = GenerateFileName(display.Index, windowInfo);
                var filePath = Path.Combine(_outputDirectory, DateTime.Now.ToString("yyyy-MM-dd"), fileName);
                
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                await image.SaveAsync(filePath, ImageFormat.Png);

                results.Add(new CapturedFile
                {
                    FilePath = filePath,
                    DisplayIndex = display.Index,
                    DisplayInfo = display,
                    WindowInfo = windowInfo,
                    Width = image.Width,
                    Height = image.Height,
                    Timestamp = DateTime.Now
                });

                _logger?.LogInformation($"Captured display {display.Index}: {filePath}");
            }

            return new CaptureResult
            {
                Success = results.Count > 0,
                CapturedFiles = results,
                TotalDisplays = displays.Count,
                CapturedDisplays = results.Count
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Capture failed");
            return new CaptureResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// 检查是否有显著变化
    /// </summary>
    private async Task<bool> HasSignificantChangeAsync(int displayIndex, ICapturedImage currentImage)
    {
        var currentHash = await _imageProcessor.ComputeHashAsync(currentImage);
        
        if (_lastCaptureHashes.TryGetValue(displayIndex, out var lastHash))
        {
            // 计算相似度
            var similarity = ComputeHashSimilarity(lastHash, currentHash);
            _lastCaptureHashes[displayIndex] = currentHash;
            return similarity < _similarityThreshold;
        }
        
        _lastCaptureHashes[displayIndex] = currentHash;
        return true;
    }

    /// <summary>
    /// 计算哈希相似度
    /// </summary>
    private static double ComputeHashSimilarity(string hash1, string hash2)
    {
        if (hash1.Length != hash2.Length)
            return 0;

        int matchCount = 0;
        for (int i = 0; i < hash1.Length; i++)
        {
            if (hash1[i] == hash2[i])
                matchCount++;
        }

        return (double)matchCount / hash1.Length;
    }

    /// <summary>
    /// 生成文件名
    /// </summary>
    private static string GenerateFileName(int displayIndex, IWindowInfo? windowInfo)
    {
        var timestamp = DateTime.Now.ToString("HHmmss");
        var processName = windowInfo?.ProcessName ?? "Unknown";
        var windowTitle = windowInfo?.Title ?? "";
        
        // 清理文件名
        var safeProcessName = string.Join("_", processName.Split(Path.GetInvalidFileNameChars()));
        var safeWindowTitle = string.Join("_", windowTitle.Split(Path.GetInvalidFileNameChars()));
        
        if (safeWindowTitle.Length > 30)
            safeWindowTitle = safeWindowTitle.Substring(0, 30);
        
        var fileName = $"{timestamp}_D{displayIndex}_{safeProcessName}";
        if (!string.IsNullOrEmpty(safeWindowTitle))
            fileName += $"_{safeWindowTitle}";
        
        fileName += $"_{Guid.NewGuid().ToString("N").Substring(0, 8)}.png";
        
        return fileName;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// 截图选项
/// </summary>
public class CaptureOptions
{
    public bool DetectChanges { get; set; } = true;
    public int[]? DisplayIndices { get; set; }
    public Rectangle? Region { get; set; }
}

/// <summary>
/// 截图结果
/// </summary>
public class CaptureResult
{
    public bool Success { get; set; }
    public List<CapturedFile> CapturedFiles { get; set; } = new();
    public int TotalDisplays { get; set; }
    public int CapturedDisplays { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 捕获的文件信息
/// </summary>
public class CapturedFile
{
    public string FilePath { get; set; } = "";
    public int DisplayIndex { get; set; }
    public IDisplayInfo DisplayInfo { get; set; } = null!;
    public IWindowInfo? WindowInfo { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime Timestamp { get; set; }
}