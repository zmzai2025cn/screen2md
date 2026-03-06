using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using Screen2MD.Platform.Common;
using Screen2MD.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Screen2MD.Core.Tests.Stress;

/// <summary>
/// 并发压力测试 - 验证系统在极端并发下的正确性
/// 目标：发现竞态条件、死锁、资源竞争
/// </summary>
public class ConcurrencyStressTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testOutputDir;
    private readonly ConcurrentBag<Exception> _capturedExceptions;

    public ConcurrencyStressTests(ITestOutputHelper output)
    {
        _output = output;
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"StressTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputDir);
        _capturedExceptions = new ConcurrentBag<Exception>();
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
    /// 测试：100个线程同时截图
    /// 验证：无文件覆盖、无崩溃、结果正确
    /// </summary>    [Fact]
    [Trait("Category", "Stress")]
    [Trait("Duration", "Long")]
    public async Task CaptureService_100ConcurrentThreads_ShouldNotCorruptData()
    {
        // Arrange
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: _testOutputDir);
        var results = new ConcurrentBag<CaptureResult>();
        var threadCount = 100;
        var capturesPerThread = 10;

        _output.WriteLine($"Starting stress test: {threadCount} threads x {capturesPerThread} captures = {threadCount * capturesPerThread} total");

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            for (int i = 0; i < capturesPerThread; i++)
            {
                try
                {
                    var result = await service.CaptureAsync(new CaptureOptions { DetectChanges = false });
                    results.Add(result);
                    
                    if (!result.Success)
                    {
                        _capturedExceptions.Add(new Exception($"Thread {threadId}, Capture {i}: {result.ErrorMessage}"));
                    }
                }
                catch (Exception ex)
                {
                    _capturedExceptions.Add(new Exception($"Thread {threadId}, Capture {i}", ex));
                }
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Completed in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Success: {results.Count(r => r.Success)}, Failed: {results.Count(r => !r.Success)}");

        // 1. 所有任务应完成，无未处理异常
        Assert.True(_capturedExceptions.IsEmpty, 
            $"Exceptions occurred: {string.Join("\n", _capturedExceptions.Take(5).Select(e => e.Message))}");

        // 2. 成功率应 > 95%
        var successRate = (double)results.Count(r => r.Success) / results.Count;
        Assert.True(successRate > 0.95, $"Success rate {successRate:P} is below 95%");

        // 3. 文件数量应正确（无覆盖）
        var capturedFiles = results.Where(r => r.Success).SelectMany(r => r.CapturedFiles).ToList();
        var uniqueFiles = capturedFiles.Select(f => f.FilePath).Distinct().Count();
        Assert.Equal(capturedFiles.Count, uniqueFiles); // 无重复路径 = 无覆盖

        // 4. 文件应真实存在
        var existingFiles = capturedFiles.Count(f => File.Exists(f.FilePath));
        Assert.Equal(capturedFiles.Count, existingFiles);

        // 5. 性能检查：平均每捕获 < 100ms
        var avgTime = stopwatch.ElapsedMilliseconds / (double)(threadCount * capturesPerThread);
        Assert.True(avgTime < 100, $"Average capture time {avgTime:F2}ms is too high");
    }

    /// <summary>
    /// 测试：并发读写配置服务
    /// 验证：数据一致性、无死锁
    /// </summary>    [Fact]
    [Trait("Category", "Stress")]
    public void ConfigurationService_ConcurrentReadWrite_ShouldMaintainConsistency()
    {
        // Arrange
        var configPath = Path.Combine(_testOutputDir, "config.json");
        var service = new ConfigurationService(configPath: configPath);
        var iterations = 1000;
        var readErrors = new ConcurrentBag<string>();
        var writeErrors = new ConcurrentBag<string>();

        // Act
        var writers = Enumerable.Range(0, 10).Select(threadId => Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    service.Set($"key{threadId}_{i}", $"value{i}");
                }
                catch (Exception ex)
                {
                    writeErrors.Add($"Write error: {ex.Message}");
                }
            }
        })).ToArray();

        var readers = Enumerable.Range(0, 10).Select(threadId => Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    var value = service.Get<string>($"key{threadId}_{i}", "default");
                    // 如果key存在，值不应为default
                    // 如果key不存在，值为default也是正确的
                }
                catch (Exception ex)
                {
                    readErrors.Add($"Read error: {ex.Message}");
                }
            }
        })).ToArray();

        Task.WaitAll(writers.Concat(readers).ToArray());

        // Assert
        Assert.True(writeErrors.IsEmpty, $"Write errors: {string.Join(", ", writeErrors.Take(5))}");
        Assert.True(readErrors.IsEmpty, $"Read errors: {string.Join(", ", readErrors.Take(5))}");

        // 验证配置可保存和加载
        var value = service.Get<string>("key0_0", "");
        Assert.False(string.IsNullOrEmpty(value), "Configuration should be persisted");
    }

    /// <summary>
    /// 测试：并发索引操作（Lucene）
    /// 验证：无死锁、数据完整性
    /// </summary>    [Fact]
    [Trait("Category", "Stress")]
    [Trait("Duration", "Long")]
    public async Task LuceneSearchService_ConcurrentIndexAndSearch_ShouldNotCorrupt()
    {
        // Arrange
        var indexPath = Path.Combine(_testOutputDir, "lucene_stress");
        var service = new LuceneSearchService(indexPath: indexPath);
        await service.InitializeAsync();

        var documentCount = 1000;
        var errors = new ConcurrentBag<Exception>();

        // Act - 并发索引
        var indexTasks = Enumerable.Range(0, 10).Select(async threadId =>
        {
            for (int i = 0; i < documentCount / 10; i++)
            {
                try
                {
                    var doc = new CaptureDocument
                    {
                        Id = $"doc_{threadId}_{i}",
                        Title = $"Document {threadId}_{i}",
                        Content = $"Content for document {threadId}_{i} with searchable text.",
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    await service.IndexCaptureAsync(doc);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        }).ToArray();

        // 同时并发搜索
        var searchTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    var result = await service.SearchAsync(new SearchQuery { Keywords = "document" });
                    // 搜索结果不应抛出异常
                    Assert.NotNull(result);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
                await Task.Delay(10);
            }
        });

        await Task.WhenAll(indexTasks.Concat(new[] { searchTask }));

        // Assert
        Assert.True(errors.IsEmpty, $"Errors: {string.Join("\n", errors.Take(5).Select(e => e.Message))}");

        // 验证所有文档可搜索
        var finalResult = await service.SearchAsync(new SearchQuery { Keywords = "document", PageSize = 2000 });
        Assert.True(finalResult.TotalCount >= documentCount * 0.9, // 允许少量丢失
            $"Expected ~{documentCount} documents, found {finalResult.TotalCount}");
    }

    /// <summary>
    /// 测试：OCR 服务并发处理
    /// 验证：队列不溢出、内存不爆炸
    /// </summary>    [Fact]
    [Trait("Category", "Stress")]
    [Trait("Duration", "Long")]
    public async Task OcrService_100ConcurrentRequests_ShouldNotCrash()
    {
        // Arrange
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        var image = new MockCapturedImage { Width = 1920, Height = 1080 };

        var results = new ConcurrentBag<OcrResult>();
        var errors = new ConcurrentBag<Exception>();

        // Act - 100个并发OCR请求
        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            try
            {
                var result = await service.RecognizeAsync(image);
                results.Add(result);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        Assert.True(errors.IsEmpty, $"OCR errors: {string.Join(", ", errors.Select(e => e.Message))}");
        Assert.Equal(100, results.Count);
        Assert.All(results, r => Assert.True(r.Success || !string.IsNullOrEmpty(r.ErrorMessage)));
    }

    /// <summary>
    /// 测试：调度器在极端负载下的稳定性
    /// </summary>    [Fact]
    [Trait("Category", "Stress")]
    [Trait("Duration", "Long")]
    public async Task CaptureScheduler_ExtremeLoad_ShouldNotCrash()
    {
        // Arrange
        var captureCount = 0;
        var errors = new ConcurrentBag<Exception>();
        
        var scheduler = new CaptureScheduler();
        scheduler.SetCaptureCallback(async ct =>
        {
            try
            {
                Interlocked.Increment(ref captureCount);
                await Task.Delay(1, ct); // 模拟1ms工作
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });

        scheduler.Config = new SchedulerConfig
        {
            MinIntervalMs = 10,  // 每10ms触发一次 = 每秒100次
            EnableAdaptiveSampling = false
        };

        // Act - 运行5秒 = 500次触发
        await scheduler.StartAsync();
        await Task.Delay(5000);
        await scheduler.StopAsync();

        // Assert
        Assert.True(errors.IsEmpty, $"Scheduler errors: {string.Join(", ", errors.Select(e => e.Message))}");
        Assert.True(captureCount >= 400, $"Expected ~500 captures, got {captureCount}"); // 允许20%误差
        Assert.True(scheduler.HealthStatus == HealthStatus.Healthy, "Scheduler should remain healthy");
    }
}
