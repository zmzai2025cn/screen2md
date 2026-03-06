using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Screen2MD.Core.Tests.Stress;

/// <summary>
/// 内存和资源泄漏检测测试
/// 目标：确保长时间运行不会耗尽系统资源
/// </summary>
public class ResourceLeakTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testOutputDir;

    public ResourceLeakTests(ITestOutputHelper output)
    {
        _output = output;
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"LeakTest_{Guid.NewGuid()}");
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

    [Fact]
    [Trait("Category", "Memory")]
    [Trait("Duration", "Long")]
    public async Task CaptureService_1000Captures_MemoryGrowthShouldBeMinimal()
    {
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: _testOutputDir);
        var captureCount = 1000;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);
        _output.WriteLine($"Initial memory: {initialMemory / 1024 / 1024} MB");

        for (int i = 0; i < captureCount; i++)
        {
            await service.CaptureAsync(new CaptureOptions { DetectChanges = false });
            if (i % 100 == 0) GC.Collect();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryGrowth = finalMemory - initialMemory;

        _output.WriteLine($"Final memory: {finalMemory / 1024 / 1024} MB");
        _output.WriteLine($"Memory growth: {memoryGrowth / 1024 / 1024} MB");

        Assert.True(memoryGrowth < 10 * 1024 * 1024, 
            $"Memory leak detected: {memoryGrowth / 1024 / 1024} MB growth after {captureCount} captures");
    }

    [Fact]
    [Trait("Category", "Memory")]
    [Trait("Duration", "Long")]
    public async Task LuceneSearchService_10000Documents_MemoryShouldNotExplode()
    {
        var indexPath = Path.Combine(_testOutputDir, "lucene_memory_test");
        var service = new LuceneSearchService(indexPath: indexPath);
        await service.InitializeAsync();
        var documentCount = 10000;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);
        _output.WriteLine($"Initial memory: {initialMemory / 1024 / 1024} MB");

        for (int i = 0; i < documentCount; i++)
        {
            await service.IndexCaptureAsync(new CaptureDocument
            {
                Id = $"doc_{i}",
                Title = $"Document {i}",
                Content = $"Content for document {i} with searchable text content to make it realistic.",
                ProcessName = "test.exe",
                Timestamp = DateTimeOffset.UtcNow
            });

            if (i % 1000 == 0) _output.WriteLine($"Indexed {i} documents...");
        }

        service.Dispose();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryGrowth = finalMemory - initialMemory;

        _output.WriteLine($"Final memory: {finalMemory / 1024 / 1024} MB");
        _output.WriteLine($"Memory growth: {memoryGrowth / 1024 / 1024} MB");

        Assert.True(memoryGrowth < 50 * 1024 * 1024,
            $"Memory explosion: {memoryGrowth / 1024 / 1024} MB for {documentCount} documents");

        var indexSize = Directory.GetFiles(indexPath, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
        _output.WriteLine($"Index size on disk: {indexSize / 1024 / 1024} MB");
        Assert.True(indexSize < 100 * 1024 * 1024, "Index files too large");
    }

    [Fact]
    [Trait("Category", "Resource")]
    public async Task CaptureService_FileHandleLeak_Detection()
    {
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: _testOutputDir);

        var initialHandles = GetCurrentProcessHandleCount();
        _output.WriteLine($"Initial handles: {initialHandles}");

        for (int i = 0; i < 100; i++)
        {
            var result = await service.CaptureAsync();
            Assert.True(result.Success);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(1000);

        var finalHandles = GetCurrentProcessHandleCount();
        _output.WriteLine($"Final handles: {finalHandles}");

        var handleGrowth = finalHandles - initialHandles;
        _output.WriteLine($"Handle growth: {handleGrowth}");

        Assert.True(handleGrowth < 10, $"Handle leak detected: {handleGrowth} handles not released");
    }

    [Fact]
    [Trait("Category", "Resource")]
    public async Task Services_ShouldCleanupTempFiles()
    {
        var tempDir = Path.GetTempPath();
        var initialTempFiles = Directory.GetFiles(tempDir, "Screen2MD*").Length;

        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor, outputDirectory: _testOutputDir);

        for (int i = 0; i < 100; i++)
        {
            await service.CaptureAsync();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(2000);

        var finalTempFiles = Directory.GetFiles(tempDir, "Screen2MD*").Length;
        var leakedFiles = finalTempFiles - initialTempFiles;

        Assert.True(leakedFiles <= 5, $"Temp file leak: {leakedFiles} files not cleaned up in {tempDir}");
    }

    private static int GetCurrentProcessHandleCount()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var fdPath = "/proc/self/fd";
                if (Directory.Exists(fdPath))
                {
                    return Directory.GetFiles(fdPath).Length;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Process.GetCurrentProcess().HandleCount;
            }
        }
        catch { }
        return 0;
    }
}
