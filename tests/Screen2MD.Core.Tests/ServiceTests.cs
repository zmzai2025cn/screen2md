using Xunit;
using Screen2MD.Core.Services;
using Screen2MD.Platform.Mock;
using Screen2MD.Platform.Common;

namespace Screen2MD.Core.Tests;

public class CaptureServiceTests
{
    [Fact]
    public void Constructor_ShouldNotThrow()
    {
        var engine = new MockCaptureEngine();
        var processor = new SkiaImageProcessor();
        var service = new CaptureService(engine, processor);
        
        Assert.NotNull(service);
    }
}

public class ConfigurationServiceTests
{
    [Fact]
    public void Get_ShouldReturnDefaultValue()
    {
        var service = new ConfigurationService();
        var value = service.Get("capture.intervalSeconds", 0);
        
        Assert.True(value > 0);
    }
}

public class StorageServiceTests
{
    [Fact]
    public void GetStats_ShouldReturnStats()
    {
        var service = new StorageService();
        var stats = service.GetStats();
        
        Assert.NotNull(stats);
    }
}