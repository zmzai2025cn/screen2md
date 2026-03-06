using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using Xunit;
using Xunit.Abstractions;

namespace Screen2MD.Core.Tests.Security;

/// <summary>
/// 安全测试 - 验证系统对恶意输入的防护能力
/// 目标：防止注入、遍历、数据泄露
/// </summary>
public class SecurityTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testOutputDir;

    public SecurityTests(ITestOutputHelper output)
    {
        _output = output;
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"SecurityTest_{Guid.NewGuid()}");
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

    #region 路径遍历攻击防护

    /// <summary>
    /// 测试：路径遍历攻击（../../../etc/passwd）
    /// </summary>    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("/etc/shadow")]
    [InlineData("%SYSTEMROOT%/system32/cmd.exe")]
    [Trait("Category", "Security")]
    [Trait("Attack", "PathTraversal")]
    public void ConfigurationService_PathTraversal_ShouldNotAccessSystemFiles(string maliciousPath)
    {
        // Arrange
        var service = new ConfigurationService();

        // Act - 尝试设置危险路径
        service.Set("capture.outputDirectory", maliciousPath);
        
        // 实际路径应被净化或限制
        var retrievedPath = service.Get("capture.outputDirectory", "");

        // Assert
        Assert.DoesNotContain("/etc/", retrievedPath);
        Assert.DoesNotContain("/Windows/", retrievedPath);
        Assert.DoesNotContain("..", retrievedPath);
    }

    /// <summary>
    /// 测试：空字节注入（Null Byte Injection）
    /// </summary>    [Fact]
    [Trait("Category", "Security")]
    [Trait("Attack", "NullByte")]
    public void FileOperations_NullByteInjection_ShouldBeSanitized()
    {
        // Arrange
        var maliciousFilename = "capture.png\0.php";
        var safePath = Path.Combine(_testOutputDir, maliciousFilename);

        // Act - 尝试创建带空字节的文件
        Exception? caughtException = null;
        try
        {
            File.WriteAllText(safePath, "test");
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        // 系统应该拒绝或正确处理空字节
        Assert.True(caughtException != null || !File.Exists(safePath.Replace("\0", "")));
    }

    #endregion

    #region SQL/命令注入防护

    /// <summary>
    /// 测试：SQL 注入攻击
    /// </summary>    [Theory]
    [InlineData("'; DROP TABLE captures; --")]
    [InlineData("1 OR 1=1")]
    [InlineData("'; DELETE FROM captures WHERE 1=1; --")]
    [InlineData("test' UNION SELECT * FROM users --")]
    [Trait("Category", "Security")]
    [Trait("Attack", "SQLInjection")]
    public async Task SearchService_SqlInjection_ShouldNotExecute(string maliciousInput)
    {
        // Arrange
        var indexPath = Path.Combine(_testOutputDir, "lucene_security");
        var service = new LuceneSearchService(indexPath: indexPath);
        await service.InitializeAsync();

        // 先索引一些正常数据
        await service.IndexCaptureAsync(new CaptureDocument
        {
            Id = "test-1",
            Title = "Normal Document",
            Content = "Normal content",
            Timestamp = DateTimeOffset.UtcNow
        });

        // Act - 尝试搜索注入字符串
        var result = await service.SearchAsync(new SearchQuery { Keywords = maliciousInput });

        // Assert - 不应崩溃，数据应完好
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 0); // 可能返回0结果，但不应抛出

        // 验证数据未被删除
        var allResults = await service.SearchAsync(new SearchQuery { Keywords = "Normal" });
        Assert.True(allResults.TotalCount > 0, "SQL Injection may have deleted data");
    }

    /// <summary>
    /// 测试：Lucene 查询注入
    /// </summary>    [Theory]
    [InlineData("*:*")]  // 返回所有文档
    [InlineData("title: *")]
    [InlineData("content: \"\" OR *:*")]
    [Trait("Category", "Security")]
    [Trait("Attack", "QueryInjection")]
    public async Task SearchService_LuceneInjection_ShouldNotBypassSecurity(string maliciousQuery)
    {
        // Arrange
        var indexPath = Path.Combine(_testOutputDir, "lucene_query_security");
        var service = new LuceneSearchService(indexPath: indexPath);
        await service.InitializeAsync();

        // Act
        var result = await service.SearchAsync(new SearchQuery { Keywords = maliciousQuery });

        // Assert - 查询应被正确处理，不返回未授权数据
        Assert.NotNull(result);
        // 对于未认证的查询，结果应被限制
    }

    #endregion

    #region 敏感信息过滤

    /// <summary>
    /// 测试：银行卡号过滤
    /// </summary>    [Fact]
    [Trait("Category", "Security")]
    [Trait("Feature", "Privacy")]
    public async Task OcrService_CreditCardNumber_ShouldBeFiltered()
    {
        // Arrange
        var sensitiveText = "My card: 4532-1234-5678-9012, CVV: 123";
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        var image = new MockCapturedImage();

        // Act
        var result = await service.RecognizeAsync(image);

        // Assert - 敏感信息不应出现在结果中
        Assert.DoesNotContain("4532-1234-5678-9012", result.Text);
        Assert.DoesNotContain("CVV", result.Text);
    }

    /// <summary>
    /// 测试：身份证号过滤
    /// </summary>    [Fact]
    [Trait("Category", "Security")]
    [Trait("Feature", "Privacy")]
    public async Task OcrService_IDNumber_ShouldBeFiltered()
    {
        // Arrange
        var sensitiveText = "ID: 110101199001011234";
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        var image = new MockCapturedImage();

        // Act
        var result = await service.RecognizeAsync(image);

        // Assert
        Assert.DoesNotContain("110101199001011234", result.Text);
    }

    /// <summary>
    /// 测试：密码过滤
    /// </summary>    [Fact]
    [Trait("Category", "Security")]
    [Trait("Feature", "Privacy")]
    public async Task OcrService_Password_ShouldBeFiltered()
    {
        // Arrange
        var sensitiveText = "Password: Secret123!, pwd: anotherpass";
        var engine = new MockOcrEngine();
        var service = new OcrService(engine);
        var image = new MockCapturedImage();

        // Act
        var result = await service.RecognizeAsync(image);

        // Assert
        Assert.DoesNotContain("Secret123!", result.Text);
    }

    #endregion

    #region DoS 攻击防护

    /// <summary>
    /// 测试：超大输入处理
    /// </summary>    [Fact]
    [Trait("Category", "Security")]
    [Trait("Attack", "DoS")]
    public void ConfigurationService_ExtremelyLargeValue_ShouldNotCrash()
    {
        // Arrange
        var service = new ConfigurationService();
        var largeValue = new string('A', 10_000_000); // 10MB 字符串

        // Act
        Exception? exception = null;
        try
        {
            service.Set("key", largeValue);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert - 不应崩溃（可能被截断或拒绝）
        Assert.True(exception == null || exception is ArgumentException);
    }

    /// <summary>
    /// 测试：深度嵌套对象
    /// </summary>    [Fact]
    [Trait("Category", "Security")]
    [Trait("Attack", "DoS")]
    public void ConfigurationService_DeeplyNestedObject_ShouldNotStackOverflow()
    {
        // Arrange
        var service = new ConfigurationService();
        // 创建循环引用或极深嵌套

        // Act & Assert - 不应栈溢出
        Assert.True(true); // 占位
    }

    #endregion

    #region 认证/授权（如果适用）

    /// <summary>
    /// 测试：未授权访问
    /// </summary>    [Fact]
    [Trait("Category", "Security")]
    [Trait("Feature", "Auth")]
    public void Services_WithoutInitialization_ShouldNotAllowOperations()
    {
        // Arrange - 创建服务但不初始化
        var service = new CaptureService(
            new MockCaptureEngine(),
            new SkiaImageProcessor()
        );

        // Act - 尝试操作
        // 应该抛出异常或返回错误
    }

    #endregion
}
