using Screen2MD.Kernel.Interfaces;
using Screen2MD.Kernel.Services;
using FluentAssertions;
using Xunit;

namespace Screen2MD.Kernel.Tests.Services;

/// <summary>
/// 日志管理器测试 - 零分配/高性能日志验证
/// </summary>
public class LogManagerTests : IDisposable
{
    private readonly string _testLogDir;
    private readonly LogManager _logManager;

    public LogManagerTests()
    {
        _testLogDir = Path.Combine(Path.GetTempPath(), $"s2md_test_logs_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testLogDir);
        
        var config = new LogConfiguration
        {
            LogDirectory = _testLogDir,
            MinimumLevel = LogLevel.Debug,
            EnableConsoleLogging = false,
            EnableFileLogging = true,
            MemoryBufferSize = 1000
        };
        
        _logManager = new LogManager(config);
        _logManager.InitializeAsync().Wait();
    }

    public void Dispose()
    {
        _logManager.Dispose();
        
        // 清理测试目录
        try
        {
            if (Directory.Exists(_testLogDir))
            {
                Directory.Delete(_testLogDir, true);
            }
        }
        catch { }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetLogger_ShouldReturnLogger()
    {
        // Act
        var logger = _logManager.GetLogger("TestCategory");

        // Assert
        logger.Should().NotBeNull();
    }

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Fatal)]
    [Trait("Category", "Unit")]
    public void Logger_ShouldLogAllLevels(LogLevel level)
    {
        // Arrange
        var logger = _logManager.GetLogger("TestCategory");
        var message = $"Test message for {level}";

        // Act
        switch (level)
        {
            case LogLevel.Trace:
                logger.LogTrace(message);
                break;
            case LogLevel.Debug:
                logger.LogDebug(message);
                break;
            case LogLevel.Information:
                logger.LogInformation(message);
                break;
            case LogLevel.Warning:
                logger.LogWarning(message);
                break;
            case LogLevel.Error:
                logger.LogError(message);
                break;
            case LogLevel.Fatal:
                logger.LogFatal(message);
                break;
        }

        // 给日志系统一点时间写入 (Linux下异步写入可能更慢)
        Thread.Sleep(500);

        // Assert
        var logs = _logManager.GetRecentLogs(10);
        logs.Should().Contain(l => l.Message == message && l.Level == level);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Logger_ShouldIncludeExceptionDetails()
    {
        // Arrange
        var logger = _logManager.GetLogger("TestCategory");
        var message = "Error with exception";
        var exception = new InvalidOperationException("Test exception");

        // Act
        logger.LogError(message, exception);
        Thread.Sleep(500);

        // Assert
        var logs = _logManager.GetRecentLogs(10);
        var log = logs.FirstOrDefault(l => l.Message == message);
        log.Should().NotBeNull();
        log!.Exception.Should().NotBeNull();
        log.Exception!.Message.Should().Be("Test exception");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Logger_ShouldIncludeCategory()
    {
        // Arrange
        var category = "MyTestCategory";
        var logger = _logManager.GetLogger(category);

        // Act
        logger.LogInformation("Test message");
        Thread.Sleep(500);

        // Assert
        var logs = _logManager.GetRecentLogs(10);
        logs.Should().Contain(l => l.Category == category);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetRecentLogs_ShouldReturnSpecifiedCount()
    {
        // Arrange
        var logger = _logManager.GetLogger("Test");
        
        // 写入50条日志
        for (int i = 0; i < 50; i++)
        {
            logger.LogInformation($"Message {i}");
        }
        Thread.Sleep(1000); // 增加等待时间确保异步写入完成

        // Act
        var logs = _logManager.GetRecentLogs(10);

        // Assert
        logs.Should().HaveCount(10);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetLogsByLevel_ShouldFilterByLevel()
    {
        // Arrange
        var logger = _logManager.GetLogger("Test");
        logger.LogInformation("Info message");
        logger.LogWarning("Warning message");
        logger.LogError("Error message");
        Thread.Sleep(500);

        // Act
        var errorLogs = _logManager.GetLogsByLevel(LogLevel.Error);

        // Assert
        errorLogs.Should().HaveCount(1);
        errorLogs.First().Message.Should().Be("Error message");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void LogManager_ShouldHandleHighThroughput()
    {
        // Arrange
        var logger = _logManager.GetLogger("PerformanceTest");
        var messageCount = 10000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - 快速写入大量日志
        for (int i = 0; i < messageCount; i++)
        {
            logger.LogInformation($"High throughput message {i}");
        }

        stopwatch.Stop();

        // Assert - 应该快速完成（无阻塞）
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, 
            $"Logging {messageCount} messages took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public void LogManager_ShouldNotCrash_On_NullMessage()
    {
        // Arrange
        var logger = _logManager.GetLogger("Test");

        // Act & Assert - 不应抛出异常
        var exception = Record.Exception(() =>
        {
            logger.LogInformation(null!);
        });

        exception.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public void LogManager_ShouldHandleConcurrentLogging()
    {
        // Arrange
        var logger = _logManager.GetLogger("ConcurrentTest");
        var tasks = new List<Task>();
        var messageCount = 100;

        // Act - 多线程并发写入
        for (int i = 0; i < 10; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < messageCount; j++)
                {
                    logger.LogInformation($"Task {taskId} Message {j}");
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());
        Thread.Sleep(2000); // 增加等待时间确保异步刷新完成

        // Assert - 应该成功写入所有日志
        var logs = _logManager.GetRecentLogs(2000);
        logs.Count().Should().BeGreaterOrEqualTo(1000);
    }

    [Fact]
    [Trait("Category", "ZeroBug")]
    public void LogManager_ShouldCreateLogFiles()
    {
        // Arrange
        var logger = _logManager.GetLogger("FileTest");
        
        // Act
        logger.LogInformation("Test message for file");
        Thread.Sleep(2000); // 增加等待时间确保文件写入

        // Assert
        var logFiles = Directory.GetFiles(_testLogDir, "screen2md_*.log");
        logFiles.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Logger_ShouldAssignUniqueIds()
    {
        // Arrange
        var logger = _logManager.GetLogger("Test");
        
        // Act
        logger.LogInformation("Message 1");
        logger.LogInformation("Message 2");
        logger.LogInformation("Message 3");
        Thread.Sleep(100);

        // Assert
        var logs = _logManager.GetRecentLogs(10).ToList();
        var ids = logs.Select(l => l.Id).ToList();
        ids.Distinct().Should().HaveCount(ids.Count, "All log entries should have unique IDs");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Logger_ShouldAssignTimestamps()
    {
        // Arrange
        var logger = _logManager.GetLogger("Test");
        var beforeLog = DateTimeOffset.UtcNow;

        // Act
        logger.LogInformation("Test message");
        Thread.Sleep(100);
        var afterLog = DateTimeOffset.UtcNow;

        // Assert
        var logs = _logManager.GetRecentLogs(10);
        var log = logs.First(l => l.Message == "Test message");
        log.Timestamp.Should().BeOnOrAfter(beforeLog);
        log.Timestamp.Should().BeOnOrBefore(afterLog);
    }
}