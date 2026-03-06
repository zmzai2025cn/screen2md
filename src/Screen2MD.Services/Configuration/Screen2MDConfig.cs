namespace Screen2MD.Services.Configuration;

/// <summary>
/// Screen2MD 配置模型 - 类型安全的配置访问
/// </summary>
public class Screen2MDConfig
{
    /// <summary>
    /// 截图配置
    /// </summary>
    public CaptureConfig Capture { get; set; } = new();

    /// <summary>
    /// OCR 配置
    /// </summary>
    public OCRConfig OCR { get; set; } = new();

    /// <summary>
    /// 存储配置
    /// </summary>
    public StorageConfig Storage { get; set; } = new();

    /// <summary>
    /// 显示配置
    /// </summary>
    public DisplayConfig Display { get; set; } = new();

    /// <summary>
    /// 隐私配置
    /// </summary>
    public PrivacyConfig Privacy { get; set; } = new();

    /// <summary>
    /// 日志配置
    /// </summary>
    public LogConfig Log { get; set; } = new();
}

/// <summary>
/// 截图配置
/// </summary>
public class CaptureConfig
{
    /// <summary>
    /// 自动截图间隔（秒）
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 输出目录
    /// </summary>
    public string OutputDirectory { get; set; } = "";

    /// <summary>
    /// 图片格式
    /// </summary>
    public string[] Formats { get; set; } = new[] { "BMP" };

    /// <summary>
    /// 启用 OCR
    /// </summary>
    public bool EnableOCR { get; set; } = true;

    /// <summary>
    /// 变化检测相似度阈值（0-1）
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.9;

    /// <summary>
    /// 手动模式（不自动截图）
    /// </summary>
    public bool ManualMode { get; set; } = false;
}

/// <summary>
/// OCR 配置
/// </summary>
public class OCRConfig
{
    /// <summary>
    /// Tesseract 可执行文件路径
    /// </summary>
    public string TesseractPath { get; set; } = "";

    /// <summary>
    /// 识别语言列表
    /// </summary>
    public string[] Languages { get; set; } = new[] { "chi_sim", "eng" };

    /// <summary>
    /// OCR 超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 并行处理数量
    /// </summary>
    public int Parallelism { get; set; } = 2;

    /// <summary>
    /// DPI 设置
    /// </summary>
    public int DPI { get; set; } = 300;

    /// <summary>
    /// PSM 模式（页面分割模式）
    /// </summary>
    public int PSM { get; set; } = 3;
}

/// <summary>
/// 存储配置
/// </summary>
public class StorageConfig
{
    /// <summary>
    /// 最大存储空间（GB）
    /// </summary>
    public int MaxStorageGB { get; set; } = 10;

    /// <summary>
    /// 自动清理天数
    /// </summary>
    public int AutoCleanupDays { get; set; } = 30;

    /// <summary>
    /// 启用自动清理
    /// </summary>
    public bool CleanupEnabled { get; set; } = true;

    /// <summary>
    /// 保留策略（保留最新N个或保留N天）
    /// </summary>
    public string CleanupStrategy { get; set; } = "Days"; // "Days" 或 "Count"

    /// <summary>
    /// 保留数量（当策略为 Count 时）
    /// </summary>
    public int KeepCount { get; set; } = 1000;
}

/// <summary>
/// 显示配置
/// </summary>
public class DisplayConfig
{
    /// <summary>
    /// 捕获所有显示器
    /// </summary>
    public bool CaptureAllDisplays { get; set; } = true;

    /// <summary>
    /// 仅捕获主显示器
    /// </summary>
    public bool PrimaryDisplayOnly { get; set; } = false;

    /// <summary>
    /// 指定捕获的显示器索引（-1 表示全部）
    /// </summary>
    public int[] CaptureDisplayIndices { get; set; } = Array.Empty<int>();

    /// <summary>
    /// 捕获区域（相对于虚拟屏幕）
    /// </summary>
    public CaptureRegion? CaptureRegion { get; set; }
}

/// <summary>
/// 捕获区域
/// </summary>
public class CaptureRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// 隐私配置
/// </summary>
public class PrivacyConfig
{
    /// <summary>
    /// 启用隐私过滤
    /// </summary>
    public bool EnablePrivacyFilter { get; set; } = false;

    /// <summary>
    /// 模糊区域列表
    /// </summary>
    public BlurRegion[] BlurRegions { get; set; } = Array.Empty<BlurRegion>();

    /// <summary>
    /// 自动模糊密码输入框
    /// </summary>
    public bool BlurPasswordFields { get; set; } = false;

    /// <summary>
    /// 敏感词过滤（OCR结果中的敏感词会被替换）
    /// </summary>
    public string[] SensitiveWords { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 进程黑名单（这些进程的截图会被跳过）
    /// </summary>
    public string[] ProcessBlacklist { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 模糊区域
/// </summary>
public class BlurRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int BlurRadius { get; set; } = 10;
}

/// <summary>
/// 日志配置
/// </summary>
public class LogConfig
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public string Level { get; set; } = "Information";

    /// <summary>
    /// 单个日志文件最大大小（MB）
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 100;

    /// <summary>
    /// 保留的日志文件数量
    /// </summary>
    public int MaxFiles { get; set; } = 10;

    /// <summary>
    /// 是否输出到控制台
    /// </summary>
    public bool OutputToConsole { get; set; } = true;

    /// <summary>
    /// 是否输出到文件
    /// </summary>
    public bool OutputToFile { get; set; } = true;
}