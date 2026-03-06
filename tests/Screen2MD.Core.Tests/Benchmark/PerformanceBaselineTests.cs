# Screen2MD Enterprise - 性能基准测试套件
# 建立性能基线，确保系统满足SLA要求

using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using Screen2MD.Platform.Common;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Screen2MD.Core.Tests.Benchmark;

/// <summary>
/// 核心性能基准测试 - 建立可重复的性能测量标准
/// </summary>
public class PerformanceBaselineTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testOutputDir;

    // 性能基线标准（SLA）
    private const int MAX_CAPTURE_LATENCY_MS = 100;      // 单张截图P95 < 100ms
    private const int MAX_OCR_LATENCY_MS = 5000;         // OCR处理 < 5s
    private const int MAX_SEARCH_LATENCY_MS = 100;       // 搜索 < 100ms
    private const long MAX_MEMORY_BYTES = 200 * 1024 * 1024; // 空闲内存 < 200MB

    public PerformanceBaselineTests(ITestOutputHelper output)
    {
        _output = output;
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"PerfBaseline_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testOutputDir))
                Directory.Delete(_testOutputDir, recursive: true);
        }
        catch { }
    }

    #region 1. 截图性能基准

    /// <summary>
    /// 基准：单张截图延迟（P50, P95, P99）
    /// 目标：P95 < 100ms
    /// </summary>    [Fact]
    [Trait("Category", "Benchmark")]
    [Trait("Metric", "Latency")]
    [Trait("Component", "Capture")]
    public async Task Capture_Latency_Percentiles_ShouldMeetSLA()
    {
        // Arrange
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: _testOutputDir);
        var iterations = 100;
        var latencies = new List<double>();

        // Warmup
        for (int i = 0; i < 10; i++)
            await service.CaptureAsync();

        // Measure
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await service.CaptureAsync();
            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Calculate percentiles
        latencies.Sort();
        var p50 = latencies[(int)(iterations * 0.50)];
        var p95 = latencies[(int)(iterations * 0.95)];
        var p99 = latencies[(int)(iterations * 0.99)];
        var max = latencies.Max();
        var avg = latencies.Average();

        _output.WriteLine($"Capture Latency:");
        _output.WriteLine($"  P50: {p50:F2}ms");
        _output.WriteLine($"  P95: {p95:F2}ms");
        _output.WriteLine($"  P99: {p99:F2}ms");
        _output.WriteLine($"  Max: {max:F2}ms");
        _output.WriteLine($"  Avg: {avg:F2}ms");

        // Assert SLA
        Assert.True(p95 < MAX_CAPTURE_LATENCY_MS, 
            $"P95 latency {p95:F2}ms exceeds SLA {MAX_CAPTURE_LATENCY_MS}ms");
    }

    /// <summary>
    /// 基准：截图吞吐量（每秒截图数）
    /// </summary>    [Fact]
    [Trait("Category", "Benchmark")]
    [Trait("Metric", "Throughput")]
    [Trait("Component", "Capture")]
    public async Task Capture_Throughput_ShouldMeetSLA()
    {
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: _testOutputDir);
        
        var duration = TimeSpan.FromSeconds(10);
        var stopwatch = Stopwatch.StartNew();
        var count = 0;

        while (stopwatch.Elapsed < duration)
        {
            await service.CaptureAsync();
            count++;
        }

        var throughput = count / duration.TotalSeconds;
        _output.WriteLine($"Capture Throughput: {throughput:F2} captures/sec");

        // 期望：至少10次/秒
        Assert.True(throughput >= 10, $"Throughput {throughput:F2}/s is below 10/s");
    }

    #endregion

    #region 2. OCR性能基准

    /// <summary>
    /// 基准：OCR处理延迟（不同分辨率）
    /// 目标：1080p < 5s
    /// </summary>    [Theory]
    [InlineData(1920, 1080, 5000)]   // FHD
    [InlineData(2560, 1440, 8000)]   // QHD
    [InlineData(3840, 2160, 15000)]  // 4K
    [Trait("Category", "Benchmark")]
    [Trait("Metric", "Latency")]
    [Trait("Component", "OCR")]
    public async Task Ocr_Latency_ByResolution_ShouldMeetSLA(int width, int height, int maxMs)
    {
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        var image = new MockCapturedImage { Width = width, Height = height };

        var sw = Stopwatch.StartNew();
        var result = await service.RecognizeAsync(image);
        sw.Stop();

        _output.WriteLine($"OCR {width}x{height}: {sw.ElapsedMilliseconds}ms");

        Assert.True(sw.ElapsedMilliseconds < maxMs,
            $"OCR latency {sw.ElapsedMilliseconds}ms exceeds {maxMs}ms for {width}x{height}");
    }

    /// <summary>
    /// 基准：OCR吞吐量
    /// </summary>    [Fact]
    [Trait("Category", "Benchmark")]
    [Trait("Metric", "Throughput")]
    [Trait("Component", "OCR")]
    public async Task Ocr_Throughput_ShouldMeetSLA()
    {
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        var image = new MockCapturedImage { Width = 1920, Height = 1080 };
        
        var duration = TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();
        var count = 0;

        while (stopwatch.Elapsed < duration)
        {
            await service.RecognizeAsync(image);
            count++;
        }

        var throughput = count / duration.TotalSeconds;
        _output.WriteLine($"OCR Throughput: {throughput:F2} images/sec");

        // 期望：至少0.5次/秒（2秒/张）
        Assert.True(throughput >= 0.5, $"OCR throughput {throughput:F2}/s is below 0.5/s");
    }

    #endregion

    #region 3. 搜索性能基准

    /// <summary>
    /// 基准：搜索延迟（不同数据量）
    /// 目标：10万文档搜索 < 100ms
    /// </summary>    [Fact]
    [Trait("Category", "Benchmark")]
    [Trait("Metric", "Latency")]
    [Trait("Component", "Search")]
    [Trait("Duration", "Long")]
    public async Task Search_Latency_With100KDocuments_ShouldMeetSLA()
    {
        var indexPath = Path.Combine(_testOutputDir, "search_baseline");
        var service = new LuceneSearchService(indexPath: indexPath);
        await service.InitializeAsync();

        // 索引10万文档
        _output.WriteLine("Indexing 100,000 documents...");
        for (int i = 0; i < 100000; i++)
        {
            await service.IndexCaptureAsync(new CaptureDocument
            {
                Id = $"doc_{i}",
                Title = $"Document {i} - {Guid.NewGuid()}",
                Content = $"Content for document {i} with searchable text.",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-i)
            });

            if (i % 20000 == 0)
                _output.WriteLine($"  Indexed {i}...");
        }

        // 测量搜索延迟
        var queries = new[] { "document", "searchable", "content", "99999" };
        var latencies = new List<double>();

        foreach (var query in queries)
        {
            for (int i = 0; i < 25; i++)
            {
                var sw = Stopwatch.StartNew();
                await service.SearchAsync(new SearchQuery { Keywords = query, PageSize = 20 });
                sw.Stop();
                latencies.Add(sw.Elapsed.TotalMilliseconds);
            }
        }

        var avgLatency = latencies.Average();
        var p95Latency = latencies.OrderBy(x => x).Skip((int)(latencies.Count * 0.95)).First();
        var maxLatency = latencies.Max();

        _output.WriteLine($"Search Latency (100K docs):");
        _output.WriteLine($"  Avg: {avgLatency:F2}ms");
        _output.WriteLine($"  P95: {p95Latency:F2}ms");
        _output.WriteLine($"  Max: {maxLatency:F2}ms");

        Assert.True(p95Latency < MAX_SEARCH_LATENCY_MS,
            $"Search P95 latency {p95Latency:F2}ms exceeds SLA {MAX_SEARCH_LATENCY_MS}ms");
    }

    #endregion

    #region 4. 内存基准

    /// <summary>
    /// 基准：空闲内存占用
    /// 目标：< 200MB
    /// </summary>    [Fact]
    [Trait("Category", "Benchmark")]
    [Trait("Metric", "Memory")]
    public void Memory_Footprint_ShouldMeetSLA()
    {
        // Force GC and measure
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memory = GC.GetTotalMemory(true);
        _output.WriteLine($"Memory Footprint: {memory / 1024 / 1024} MB");

        Assert.True(memory < MAX_MEMORY_BYTES,
            $"Memory footprint {memory / 1024 / 1024} MB exceeds SLA {MAX_MEMORY_BYTES / 1024 / 1024} MB");
    }

    /// <summary>
    /// 基准：操作后内存增长
    /// </summary>    [Fact]
    [Trait("Category", "Benchmark")]
    [Trait("Metric", "Memory")]
    public async Task Memory_Growth_AfterOperations_ShouldBeMinimal()
    {
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: _testOutputDir);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        // 执行100次操作
        for (int i = 0; i < 100; i++)
        {
            await service.CaptureAsync();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);

        var growth = finalMemory - initialMemory;
        var growthMB = growth / 1024 / 1024;

        _output.WriteLine($"Memory Growth after 100 operations: {growthMB:F2} MB");

        // 期望：增长 < 10MB
        Assert.True(growthMB < 10, $"Memory growth {growthMB:F2} MB is too high");
    }

    #endregion

    #region 5. 调度器性能基准

    /// <summary>
    /// 基准：调度器精度
    /// </summary>    [Fact]
    [Trait("Category", "Benchmark")]
    [Trait("Metric", "Accuracy")]
    [Trait("Component", "Scheduler")]
    public async Task Scheduler_TimingAccuracy_ShouldBePrecise()
    {
        var intervals = new List<double>();
        var scheduler = new CaptureScheduler();
        var lastCapture = DateTimeOffset.UtcNow;
        var captureCount = 0;

        scheduler.SetCaptureCallback(ct =>
        {
            var now = DateTimeOffset.UtcNow;
            var interval = (now - lastCapture).TotalMilliseconds;
            intervals.Add(interval);
            lastCapture = now;
            captureCount++;
            return Task.CompletedTask;
        });

        scheduler.Config = new SchedulerConfig
        {
            MinIntervalMs = 100,  // 100ms间隔
            EnableAdaptiveSampling = false
        };

        await scheduler.StartAsync();
        await Task.Delay(5000);  // 运行5秒
        await scheduler.StopAsync();

        // 跳过第一个（启动延迟）
        var validIntervals = intervals.Skip(1).ToList();
        var avgInterval = validIntervals.Average();
        var stdDev = CalculateStdDev(validIntervals);

        _output.WriteLine($"Scheduler Timing:");
        _output.WriteLine($"  Expected: 100ms");
        _output.WriteLine($"  Actual Avg: {avgInterval:F2}ms");
        _output.WriteLine($"  StdDev: {stdDev:F2}ms");
        _output.WriteLine($"  Captures: {captureCount}");

        // 期望：平均间隔误差 < 10%，标准差 < 20ms
        Assert.True(Math.Abs(avgInterval - 100) < 10, 
            $"Average interval deviation too large: {avgInterval:F2}ms");
        Assert.True(stdDev < 20, 
            $"Timing jitter too high: {stdDev:F2}ms");
    }

    #endregion

    #region 辅助方法

    private double CalculateStdDev(List<double> values)
    {
        var avg = values.Average();
        var sumSquares = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSquares / values.Count);
    }

    #endregion
}
