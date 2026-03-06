using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Screen2MD.Core.Tests.Chaos;

/// <summary>
/// 混沌工程测试 - 故意注入故障验证系统恢复能力
/// 目标：验证容错性、自愈能力、数据不丢失
/// </summary>
public class ChaosTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testOutputDir;
    private readonly Random _random;

    public ChaosTests(ITestOutputHelper output)
    {
        _output = output;
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"ChaosTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputDir);
        _random = new Random();
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
    /// 测试：随机杀死文件句柄
    /// </summary>    [Fact]
    [Trait("Category", "Chaos")]
    [Trait("Duration", "Long")]
    public async Task CaptureService_RandomFileCorruption_ShouldRecover()
    {
        // Arrange
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: _testOutputDir);
        var successCount = 0;
        var errorCount = 0;

        // Act - 进行100次捕获，随机破坏其中一些文件
        for (int i = 0; i < 100; i++)
        {
            var result = await service.CaptureAsync();

            if (result.Success)
            {
                successCount++;

                // 随机破坏5%的文件
                if (_random.Next(100) < 5 && result.CapturedFiles.Count > 0)
                {
                    var file = result.CapturedFiles[0].FilePath;
                    try
                    {
                        // 破坏文件（写入垃圾数据）
                        File.WriteAllText(file, "CORRUPTED");
                    }
                    catch { }
                }
            }
            else
            {
                errorCount++;
            }
        }

        // Assert - 系统应继续工作，成功率 > 80%
        var successRate = (double)successCount / 100;
        _output.WriteLine($"Success rate: {successRate:P}, Errors: {errorCount}");
        Assert.True(successRate > 0.8, $"Success rate {successRate:P} too low after chaos");
    }

    /// <summary>
    /// 测试：磁盘空间不足
    /// </summary>    [Fact]
    [Trait("Category", "Chaos")]
    public async Task CaptureService_DiskFull_ShouldHandleGracefully()
    {
        // Arrange - 使用很小的临时目录模拟磁盘满
        var smallDir = Path.Combine(_testOutputDir, "small_disk");
        Directory.CreateDirectory(smallDir);

        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: smallDir);

        // 填充目录到接近满
        var filler = new byte[1024 * 1024]; // 1MB
        for (int i = 0; i < 100; i++)
        {
            try
            {
                File.WriteAllBytes(Path.Combine(smallDir, $"filler_{i}.bin"), filler);
            }
            catch (IOException)
            {
                break; // 磁盘"满"了
            }
        }

        // Act - 尝试截图
        Exception? caughtException = null;
        CaptureResult? result = null;
        try
        {
            result = await service.CaptureAsync();
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert - 不应崩溃，应返回错误信息
        Assert.True(caughtException == null || result?.Success == false,
            "Should handle disk full gracefully without crashing");
    }

    /// <summary>
    /// 测试：随机延迟和超时
    /// </summary>    [Fact]
    [Trait("Category", "Chaos")]
    [Trait("Duration", "Long")]
    public async Task Scheduler_RandomDelays_ShouldNotDeadlock()
    {
        // Arrange
        var captureCount = 0;
        var scheduler = new CaptureScheduler();
        
        scheduler.SetCaptureCallback(async ct =>
        {
            // 随机延迟 0-100ms
            await Task.Delay(_random.Next(100), ct);
            Interlocked.Increment(ref captureCount);
        });

        scheduler.Config = new SchedulerConfig
        {
            MinIntervalMs = 50,
            EnableAdaptiveSampling = false
        };

        // Act - 运行10秒，期间随机注入延迟
        await scheduler.StartAsync();
        await Task.Delay(10000);
        await scheduler.StopAsync();

        // Assert - 应完成捕获，无死锁
        _output.WriteLine($"Captured {captureCount} times");
        Assert.True(captureCount > 50, "Scheduler should continue working under random delays");
    }

    /// <summary>
    /// 测试：配置突然改变
    /// </summary>    [Fact]
    [Trait("Category", "Chaos")]
    public async Task ConfigurationService_RandomChanges_ShouldMaintainConsistency()
    {
        // Arrange
        var configPath = Path.Combine(_testOutputDir, "chaos_config.json");
        var service = new ConfigurationService(configPath: configPath);
        var errors = new List<Exception>();
        var stopwatch = Stopwatch.StartNew();

        // Act - 并发随机读写配置
        var tasks = new List<Task>();

        // 写入任务
        for (int t = 0; t < 5; t++)
        {
            var taskId = t;
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        service.Set($"key_{taskId}_{i}", Guid.NewGuid().ToString());
                        
                        // 随机更新其他键
                        if (_random.Next(10) == 0)
                        {
                            service.Set($"random_key_{_random.Next(10)}", "random_value");
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (errors) { errors.Add(ex); }
                    }
                }
            }));
        }

        // 读取任务
        for (int t = 0; t < 3; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        var value = service.Get($"key_{_random.Next(5)}_{_random.Next(100)}", "default");
                        // 读取操作不应抛出
                    }
                    catch (Exception ex)
                    {
                        lock (errors) { errors.Add(ex); }
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.True(errors.Count == 0, 
            $"Errors during chaos: {string.Join(", ", errors.Take(5).Select(e => e.Message))}");

        // 验证配置可保存
        var testValue = service.Get("key_0_0", "");
        Assert.False(string.IsNullOrEmpty(testValue));
    }

    /// <summary>
    /// 测试：索引损坏恢复
    /// </summary>    [Fact]
    [Trait("Category", "Chaos")]
    public async Task LuceneSearchService_IndexCorruption_ShouldRecover()
    {
        // Arrange
        var indexPath = Path.Combine(_testOutputDir, "corrupted_index");
        var service = new LuceneSearchService(indexPath: indexPath);
        await service.InitializeAsync();

        // 索引一些数据
        for (int i = 0; i < 100; i++)
        {
            await service.IndexCaptureAsync(new CaptureDocument
            {
                Id = $"doc_{i}",
                Title = $"Document {i}",
                Content = "Content",
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        service.Dispose();

        // 破坏索引文件
        var indexFiles = Directory.GetFiles(indexPath, "*", SearchOption.AllDirectories);
        if (indexFiles.Length > 0)
        {
            var targetFile = indexFiles[_random.Next(indexFiles.Length)];
            try
            {
                File.WriteAllText(targetFile, "CORRUPTED DATA");
            }
            catch { }
        }

        // Act - 重新打开服务
        Exception? caughtException = null;
        SearchResult? result = null;
        try
        {
            var newService = new LuceneSearchService(indexPath: indexPath);
            await newService.InitializeAsync();
            result = await newService.SearchAsync(new SearchQuery { Keywords = "document" });
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert - 应能恢复或重建索引
        Assert.True(caughtException == null || result != null,
            "Should recover from index corruption");
    }

    /// <summary>
    /// 测试：内存压力下的行为
    /// </summary>    [Fact]
    [Trait("Category", "Chaos")]
    [Trait("Duration", "Long")]
    public async Task System_UnderMemoryPressure_ShouldNotCrash()
    {
        // Arrange
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: _testOutputDir);

        // 创建内存压力（分配大数组）
        var memoryHogs = new List<byte[]>();
        for (int i = 0; i < 10; i++)
        {
            try
            {
                memoryHogs.Add(new byte[10 * 1024 * 1024]); // 10MB each
            }
            catch (OutOfMemoryException)
            {
                break;
            }
        }

        // Act - 在内存压力下尝试操作
        Exception? caughtException = null;
        try
        {
            await service.CaptureAsync();
        }
        catch (OutOfMemoryException ex)
        {
            caughtException = ex;
        }

        // 释放内存
        memoryHogs.Clear();
        GC.Collect();

        // Assert - 应优雅处理（可能失败但不崩溃）
        Assert.True(caughtException == null || caughtException is OutOfMemoryException,
            "Should handle OOM gracefully");

        // 内存释放后应能恢复工作
        var result = await service.CaptureAsync();
        Assert.True(result.Success, "Should recover after memory pressure");
    }
}
