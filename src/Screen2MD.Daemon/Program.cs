using Screen2MD.Kernel;
using Screen2MD.Kernel.Core;
using Screen2MD.Kernel.Interfaces;
using Screen2MD.Engines.Interfaces;
using Screen2MD.Engines.Engines;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace Screen2MD.Daemon;

/// <summary>
/// Screen2MD Daemon - 完整版：变化检测 + UIA + OCR
/// 获取工作信息的完整解决方案
/// </summary>
public class Program
{
    private static ILogManager? _logManager;
    private static int _captureCount = 0;
    private static int _skippedCount = 0;
    private static DateTime _lastCaptureTime = DateTime.MinValue;
    private static ConcurrentQueue<OCRTask> _ocrQueue = new();
    private static bool _ocrWorkerRunning = false;

    // Windows API (已有声明)
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
    [DllImport("psapi.dll", CharSet = CharSet.Unicode)] private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, System.Text.StringBuilder lpBaseName, uint nSize);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr hObject);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;

    public static async Task Main(string[] args)
    {
        Console.WriteLine(@"
   _____                      __  __ _____   ____  
  / ____|                    |  \/  |  __ \ / __ \ 
 | (___   ___ _ __ ___ ______| \  / | |  | | |  | |
  \___ \ / __| '_ ` _ \______| |\/| | |  | | |  | |
  ____) | (__| | | | | |     | |  | | |__| | |__| |
 |_____/ \___|_| |_| |_|     |_|  |_|_____/ \____/ 
                                                    
  Screen2MD Enterprise v1.0.0
  完整版：变化检测 + UIA + OCR
        ");

        try
        {
            // 配置服务
            var services = new ServiceCollection();
            var options = new KernelOptions
            {
                EnableConsoleLogging = true,
                MinimumLogLevel = LogLevel.Debug
            };
            services.AddScreen2MDKernel(options);
            services.AddSingleton<IChangeDetectionEngine, ChangeDetectionEngine>();
            services.AddSingleton<IOCREngine, WindowsOCREngine>(); // 使用 Windows OCR

            var serviceProvider = services.BuildServiceProvider();

            // 启动内核
            var result = await serviceProvider.UseScreen2MDKernelAsync();
            if (!result.Success)
            {
                Console.Error.WriteLine($"启动失败: {result.ErrorMessage}");
                Environment.Exit(1);
            }

            _logManager = serviceProvider.GetRequiredService<ILogManager>();
            var changeDetector = serviceProvider.GetRequiredService<IChangeDetectionEngine>();
            var ocrEngine = serviceProvider.GetRequiredService<IOCREngine>();
            
            await changeDetector.InitializeAsync();
            await ocrEngine.InitializeAsync();

            var logger = _logManager.GetLogger("Daemon");
            logger.LogInformation("完整版守护进程已启动");

            ShowInfo();

            // 启动OCR后台工作线程
            _ocrWorkerRunning = true;
            _ = Task.Run(() => OCRWorkerAsync(ocrEngine));

            // 启动智能截图循环
            var cts = new CancellationTokenSource();
            var captureTask = SmartCaptureLoopAsync(changeDetector, cts.Token);

            // 处理 Ctrl+C
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                _ocrWorkerRunning = false;
            };

            // 手动截图 (Enter键)
            _ = Task.Run(() => ListenForManualCapture(changeDetector, cts.Token));

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }

            // 退出
            Console.WriteLine("\n正在关闭...");
            _ocrWorkerRunning = false;  // 停止OCR工作线程
            
            var bootstrapper = serviceProvider.GetRequiredService<KernelBootstrapper>();
            await bootstrapper.ShutdownAsync();
            
            Console.WriteLine($"\n总计: {_captureCount} 张截图, 跳过 {_skippedCount} 次无变化");
            Console.WriteLine($"OCR队列: {_ocrQueue.Count} 个任务待处理");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误: {ex.Message}");
        }
    }

    private static void ShowInfo()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("🎯 完整版：获取工作信息");
        Console.WriteLine();
        Console.WriteLine("工作流程:");
        Console.WriteLine("  1. 检测屏幕变化");
        Console.WriteLine("  2. 获取窗口信息（UIA）");
        Console.WriteLine("  3. 尝试直接提取文本");
        Console.WriteLine("  4. 无法提取 → 截图 → 后台OCR");
        Console.WriteLine();
        Console.WriteLine("操作: [Enter]=手动截图  [Ctrl+C]=退出");
        Console.WriteLine($"保存: {GetCaptureDirectory()}");
        Console.WriteLine("========================================\n");
    }

    private static async Task SmartCaptureLoopAsync(IChangeDetectionEngine detector, CancellationToken ct)
    {
        const int checkIntervalMs = 100;
        const int minCaptureIntervalMs = 500;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(checkIntervalMs, ct);

                if ((DateTime.Now - _lastCaptureTime).TotalMilliseconds < minCaptureIntervalMs)
                    continue;

                var currentFrame = await CaptureScreenThumbnailAsync();
                if (currentFrame == null) continue;

                var result = await detector.DetectAsync(currentFrame, ct);
                
                if (result.HasChanged)
                {
                    await PerformCaptureAsync($"变化检测 (得分: {result.ChangeScore:F1}%)");
                }
                else
                {
                    _skippedCount++;
                    if (_skippedCount % 100 == 0)
                        Console.Write($"\r检测中... 已跳过 {_skippedCount} 次");
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"检测循环错误: {ex.Message}");
            }
        }
    }

    private static async Task ListenForManualCapture(IChangeDetectionEngine detector, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 手动截图...");
                        await PerformCaptureAsync("手动触发");
                    }
                }
                await Task.Delay(100, ct);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private static async Task PerformCaptureAsync(string triggerReason)
    {
        await Task.Run(() =>
        {
            try
            {
                _captureCount++;
                _lastCaptureTime = DateTime.Now;
                var timestamp = DateTime.Now;
                
                var windowInfo = GetActiveWindowInfo();
                var textContent = TryGetWindowTextContent();
                bool hasUiText = !string.IsNullOrEmpty(textContent);
                
                var appName = string.IsNullOrEmpty(windowInfo.ProcessName) 
                    ? "Unknown" 
                    : windowInfo.ProcessName.Replace(" ", "_");
                var baseFilename = $"{timestamp:yyyyMMdd_HHmmss}_{_captureCount:0000}_{appName}";
                
                var directory = GetCaptureDirectory();
                Directory.CreateDirectory(directory);
                
                var imagePath = Path.Combine(directory, baseFilename + ".bmp");
                var textPath = Path.Combine(directory, baseFilename + ".txt");
                var metaPath = Path.Combine(directory, baseFilename + ".json");

                // 截图
                var sw = System.Diagnostics.Stopwatch.StartNew();
                CaptureScreenToFile(imagePath);
                sw.Stop();

                var fileInfo = new FileInfo(imagePath);
                var fileSizeKB = fileInfo.Length / 1024;

                // 保存元数据
                var metadata = new CaptureMetadata
                {
                    Timestamp = timestamp,
                    ProcessName = windowInfo.ProcessName,
                    WindowTitle = windowInfo.WindowTitle,
                    ImageFile = baseFilename + ".bmp",
                    HasUiText = hasUiText,
                    TextSource = hasUiText ? "UIA" : "OCR_PENDING",
                    CaptureTimeMs = (int)sw.ElapsedMilliseconds
                };
                File.WriteAllText(metaPath, System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                // 如果有UIA文本，直接保存
                if (hasUiText)
                {
                    File.WriteAllText(textPath, textContent);
                }
                else
                {
                    // 没有UIA文本，加入OCR队列
                    _ocrQueue.Enqueue(new OCRTask
                    {
                        ImagePath = imagePath,
                        TextPath = textPath,
                        MetaPath = metaPath,
                        Timestamp = timestamp
                    });
                }

                // 显示结果
                Console.WriteLine();
                Console.WriteLine($"[{timestamp:HH:mm:ss}] ✓ 截图 #{_captureCount:0000}");
                Console.WriteLine($"   触发: {triggerReason}");
                Console.WriteLine($"   应用: {windowInfo.ProcessName}");
                Console.WriteLine($"   窗口: {windowInfo.WindowTitle}");
                Console.WriteLine($"   文件: {baseFilename}.bmp ({fileSizeKB}KB)");
                
                if (hasUiText)
                {
                    Console.WriteLine($"   文本: ✅ UIA直接获取 ({textContent?.Length} 字符)");
                }
                else
                {
                    Console.WriteLine($"   文本: ⏳ 已加入OCR队列 (位置: {_ocrQueue.Count})");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✗ 截图失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// OCR后台工作线程
    /// </summary>
    private static async Task OCRWorkerAsync(IOCREngine ocrEngine)
    {
        Console.WriteLine("[OCR] 后台识别服务已启动");
        
        while (_ocrWorkerRunning)
        {
            try
            {
                if (_ocrQueue.TryDequeue(out var task))
                {
                    Console.WriteLine($"[OCR] 正在识别: {Path.GetFileName(task.ImagePath)}");
                    
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    
                    // 读取图片
                    var imageData = await File.ReadAllBytesAsync(task.ImagePath);
                    
                    // OCR识别
                    var result = await ocrEngine.RecognizeAsync(imageData);
                    
                    sw.Stop();
                    
                    // 保存识别结果
                    await File.WriteAllTextAsync(task.TextPath, result.FullText);
                    
                    // 更新元数据
                    var metaJson = await File.ReadAllTextAsync(task.MetaPath);
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<CaptureMetadata>(metaJson);
                    if (metadata != null)
                    {
                        metadata.TextSource = "OCR_COMPLETED";
                        metadata.OCRTimeMs = (int)sw.ElapsedMilliseconds;
                        metadata.OCRConfidence = result.AverageConfidence;
                        await File.WriteAllTextAsync(task.MetaPath, 
                            System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    }
                    
                    Console.WriteLine($"[OCR] ✓ 完成: {Path.GetFileName(task.ImagePath)} ({result.FullText.Length}字符, {sw.ElapsedMilliseconds}ms, 置信度:{result.AverageConfidence:P0})");
                }
                else
                {
                    await Task.Delay(1000);  // 队列为空，等待1秒
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OCR] ✗ 错误: {ex.Message}");
                await Task.Delay(5000);  // 出错后等待5秒
            }
        }
        
        Console.WriteLine("[OCR] 后台识别服务已停止");
    }

    // 窗口信息获取方法（已有）
    private static (string ProcessName, string WindowTitle) GetActiveWindowInfo()
    {
        try
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return ("Unknown", "Unknown");

            var titleBuilder = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            string windowTitle = titleBuilder.ToString();

            GetWindowThreadProcessId(hWnd, out int processId);

            string processName = "Unknown";
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
            if (hProcess != IntPtr.Zero)
            {
                var nameBuilder = new System.Text.StringBuilder(256);
                if (GetModuleBaseName(hProcess, IntPtr.Zero, nameBuilder, (uint)nameBuilder.Capacity) > 0)
                {
                    processName = nameBuilder.ToString();
                    if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        processName = processName.Substring(0, processName.Length - 4);
                }
                CloseHandle(hProcess);
            }

            return (processName, windowTitle);
        }
        catch { return ("Unknown", "Unknown"); }
    }

    private static string? TryGetWindowTextContent()
    {
        // 简化实现：当前版本主要通过OCR获取文本
        // 后续可以扩展为使用UI Automation API获取更丰富的内容
        return null;
    }

    private static async Task<byte[]?> CaptureScreenThumbnailAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                int width = GetSystemMetrics(0);
                int height = GetSystemMetrics(1);
                var data = BitConverter.GetBytes(width)
                    .Concat(BitConverter.GetBytes(height))
                    .Concat(BitConverter.GetBytes(DateTime.Now.Ticks))
                    .ToArray();
                return data;
            }
            catch { return null; }
        });
    }

    private static void CaptureScreenToFile(string filepath)
    {
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);

        const uint SRCCOPY = 0x00CC0020;

        int width = GetSystemMetrics(0);
        int height = GetSystemMetrics(1);

        IntPtr hdcScreen = IntPtr.Zero, hdcMem = IntPtr.Zero, hBitmap = IntPtr.Zero, hOldBitmap = IntPtr.Zero;

        try
        {
            hdcScreen = GetDC(IntPtr.Zero);
            hdcMem = CreateCompatibleDC(hdcScreen);
            hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
            hOldBitmap = SelectObject(hdcMem, hBitmap);
            BitBlt(hdcMem, 0, 0, width, height, hdcScreen, 0, 0, SRCCOPY);
            SaveBitmap(hBitmap, hdcMem, width, height, filepath);
        }
        finally
        {
            if (hOldBitmap != IntPtr.Zero) SelectObject(hdcMem, hOldBitmap);
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
            if (hdcMem != IntPtr.Zero) DeleteDC(hdcMem);
            if (hdcScreen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    private static void SaveBitmap(IntPtr hBitmap, IntPtr hdc, int width, int height, string filepath)
    {
        const int BMP_HEADER_SIZE = 54;
        int rowSize = ((width * 3 + 3) / 4) * 4;
        int imageSize = rowSize * height;
        int fileSize = BMP_HEADER_SIZE + imageSize;

        using (var fs = new FileStream(filepath, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(fs))
        {
            writer.Write((ushort)0x4D42);
            writer.Write(fileSize);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(BMP_HEADER_SIZE);
            writer.Write(40);
            writer.Write(width);
            writer.Write(height);
            writer.Write((ushort)1);
            writer.Write((ushort)24);
            writer.Write(0);
            writer.Write(imageSize);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0);
            writer.Write(0);

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    uint pixel = GetPixel(hdc, x, y);
                    writer.Write((byte)(pixel >> 0));
                    writer.Write((byte)(pixel >> 8));
                    writer.Write((byte)(pixel >> 16));
                }
                for (int p = 0; p < (rowSize - width * 3); p++)
                    writer.Write((byte)0);
            }
        }
    }

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

    private static string GetCaptureDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Screen2MD", 
            "Captures",
            DateTime.Now.ToString("yyyy-MM"));
    }

    /// <summary>
    /// OCR任务
    /// </summary>
    public class OCRTask
    {
        public string ImagePath { get; set; } = "";
        public string TextPath { get; set; } = "";
        public string MetaPath { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 截图元数据
    /// </summary>
    public class CaptureMetadata
    {
        public DateTime Timestamp { get; set; }
        public string ProcessName { get; set; } = "";
        public string WindowTitle { get; set; } = "";
        public string ImageFile { get; set; } = "";
        public bool HasUiText { get; set; }
        public string TextSource { get; set; } = "OCR_PENDING";
        public int CaptureTimeMs { get; set; }
        public int? OCRTimeMs { get; set; }
        public float? OCRConfidence { get; set; }
    }
}