using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Screen2MD.Core.Tests.Performance;

/// <summary>
/// 性能边界测试 - 验证系统在极限条件下的表现
/// 目标：建立性能基线，发现性能退化
/// </summary>
public class PerformanceBoundaryTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testOutputDir;

    public PerformanceBoundaryTests(ITestOutputHelper output)
    {
        _output = output;
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"PerfTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testOutputDir))
            {
                Directory.Delete(_testOutputDir, recursive: true);
            }
        }
        catch { }
    }

    /// <summary>
    /// 测试：截图性能基准
    /// 标准：单张截图 < 500ms
    /// </summary>    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Metric", "Latency")]
    public async Task CaptureService_SingleCapture_LatencyShouldBeLow()
    {
        // Arrange
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: _testOutputDir);
        var iterations = 100;
        var latencies = new List<long>();

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            await service.CaptureAsync();
        }

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await service.CaptureAsync();
            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        // Assert
        var avgLatency = latencies.Average();
        var p95Latency = latencies.OrderBy(x => x).Skip((int)(iterations * 0.95)).First();
        var maxLatency = latencies.Max();

        _output.WriteLine($"Avg: {avgLatency:F2}ms, P95: {p95Latency}ms, Max: {maxLatency}ms");

        Assert.True(avgLatency < 100, $"Average latency {avgLatency:F2}ms exceeds 100ms");
        Assert.True(p95Latency < 500, $"P95 latency {p95Latency}ms exceeds 500ms");
    }

    /// <summary>
    /// 测试：OCR 性能基准
    /// 标准：1080p 图像 OCR < 5s
    /// </summary>    [Theory]
    [InlineData(1920, 1080, 5000)]   // FHD, 5s
    [InlineData(2560, 1440, 8000)]   // QHD, 8s
    [InlineData(3840, 2160, 15000)]  // 4K, 15s
    [Trait("Category", "Performance")]
    [Trait("Metric", "Latency")]
    public async Task OcrService_DifferentResolutions_ShouldCompleteInTime(
        int width, int height, int maxTimeMs)
    {
        // Arrange
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        var image = new MockCapturedImage { Width = width, Height = height };

        // Act
        var sw = Stopwatch.StartNew();
        var result = await service.RecognizeAsync(image);
        sw.Stop();

        // Assert
        _output.WriteLine($"{width}x{height}: {sw.ElapsedMilliseconds}ms");
        Assert.True(sw.ElapsedMilliseconds < maxTimeMs,
            $"OCR took {sw.ElapsedMilliseconds}ms, exceeds limit {maxTimeMs}ms for {width}x{height}");
    }

    /// <summary>
    /// 测试：搜索性能 - 大数据集
    /// 标准：10万文档搜索 < 100ms
    /// </summary>    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Metric", "Throughput")]
    [Trait("Duration", "Long")]
    public async Task LuceneSearchService_100KDocuments_SearchShouldBeFast()
    {
        // Arrange
        var indexPath = Path.Combine(_testOutputDir, "perf_100k");
        var service = new LuceneSearchService(indexPath: indexPath);
        await service.InitializeAsync();

        // 索引10万文档
        _output.WriteLine("Indexing 100,000 documents...");
        var indexSw = Stopwatch.StartNew();
        for (int i = 0; i < 100000; i++)
        {
            await service.IndexCaptureAsync(new CaptureDocument
            {
                Id = $"doc_{i}",
                Title = $"Document {i} - {Guid.NewGuid()}",
                Content = $"Content for document {i} with searchable text and keywords.",
                ProcessName = i % 10 == 0 ? "chrome.exe" : "notepad.exe",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-i)
            });

            if (i % 10000 == 0)
            {
                _output.WriteLine($"Indexed {i}...");
            }
        }
        indexSw.Stop();
        _output.WriteLine($"Indexing completed in {indexSw.ElapsedMilliseconds}ms");

        // 搜索性能测试
        var searchTimes = new List<long>();
        var queries = new[] { "document", "chrome", "searchable", "keywords", "99999" };

        // Warmup
        foreach (var query in queries)
        {
            await service.SearchAsync(new SearchQuery { Keywords = query });
        }

        // 正式测试
        for (int i = 0; i < 100; i++)
        {
            var query = queries[i % queries.Length];
            var sw = Stopwatch.StartNew();
            var result = await service.SearchAsync(new SearchQuery { Keywords = query, PageSize = 20 });
            sw.Stop();
            searchTimes.Add(sw.ElapsedMilliseconds);
        }

        var avgSearchTime = searchTimes.Average();
        var maxSearchTime = searchTimes.Max();

        _output.WriteLine($"Search Avg: {avgSearchTime:F2}ms, Max: {maxSearchTime}ms");

        // Assert
        Assert.True(avgSearchTime < 100, $"Average search time {avgSearchTime:F2}ms exceeds 100ms");
        Assert.True(maxSearchTime < 500, $"Max search time {maxSearchTime}ms exceeds 500ms");
    }

    /// <summary>
    /// 测试：调度器吞吐量
    /// 标准：每秒至少10次捕获
    /// </summary>    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Metric", "Throughput")]
    public async Task Scheduler_Throughput_ShouldBeHigh()
    {
        // Arrange
        var captureCount = 0;
        var scheduler = new CaptureScheduler();
        scheduler.SetCaptureCallback(async ct =>
        {
            Interlocked.Increment(ref captureCount);
            await Task.CompletedTask;
        });

        scheduler.Config = new SchedulerConfig
        {
            MinIntervalMs = 100, // 每秒10次
            EnableAdaptiveSampling = false
        };

        // Act
        var sw = Stopwatch.StartNew();
        await scheduler.StartAsync();
        await Task.Delay(10000); // 运行10秒
        await scheduler.StopAsync();
        sw.Stop();

        // Assert
        var actualInterval = sw.ElapsedMilliseconds / (double)captureCount;
        var throughput = captureCount / (sw.ElapsedMilliseconds / 1000.0);

        _output.WriteLine($"Captures: {captureCount}, Throughput: {throughput:F2}/s, Avg interval: {actualInterval:F2}ms");

        Assert.True(throughput >= 9, $"Throughput {throughput:F2}/s is below 9/s");
    }

    /// <summary>
    /// 测试：存储服务清理性能
    /// 标准：清理1万文件 < 5s
    /// </summary>    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Metric", "Latency")]
    [Trait("Duration", "Long")]
    public async Task StorageService_Cleanup_10KFiles_ShouldBeFast()
    {
        // Arrange
        var storageDir = Path.Combine(_testOutputDir, "cleanup_perf");
        Directory.CreateDirectory(storageDir);

        // 创建1万个文件
        _output.WriteLine("Creating 10,000 test files...");
        for (int i = 0; i < 10000; i++)
        {
            var dir = Path.Combine(storageDir, $"2025-{(i % 12 + 1):D2}-{(i % 28 + 1):D2}");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"file_{i}.txt"), $"Content {i}");
        }

        var service = new StorageService(baseDirectory: storageDir);

        // Act
        var sw = Stopwatch.StartNew();
        var result = await service.CleanupAsync();
        sw.Stop();

        _output.WriteLine($"Cleanup completed in {sw.ElapsedMilliseconds}ms, deleted {result.DeletedFiles} files");

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 5000, 
            $"Cleanup took {sw.ElapsedMilliseconds}ms, exceeds 5000ms");
    }

    /// <summary>
    /// 测试：配置服务大数据量
    /// 标准：1万个配置项读写 < 1s
    /// </summary>    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Metric", "Throughput")]
    public void ConfigurationService_10KItems_ShouldBeFast()
    {
        // Arrange
        var configPath = Path.Combine(_testOutputDir, "perf_config.json");
        var service = new ConfigurationService(configPath: configPath);

        // Act - 写入
        var writeSw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            service.Set($"key_{i}", $"value_{i}_{Guid.NewGuid()}");
        }
        writeSw.Stop();

        // 读取
        var readSw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            var value = service.Get<string>($"key_{i}", "");
        }
        readSw.Stop();

        _output.WriteLine($"Write 10K: {writeSw.ElapsedMilliseconds}ms, Read 10K: {readSw.ElapsedMilliseconds}ms");

        // Assert
        Assert.True(writeSw.ElapsedMilliseconds < 5000, $"Write too slow: {writeSw.ElapsedMilliseconds}ms");
        Assert.True(readSw.ElapsedMilliseconds < 1000, $"Read too slow: {readSw.ElapsedMilliseconds}ms");
    }
}
