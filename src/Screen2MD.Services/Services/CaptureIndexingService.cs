using Screen2MD.Kernel.Interfaces;
using Screen2MD.Services.Interfaces;
using IKernelLogger = Screen2MD.Services.IKernelLogger;

namespace Screen2MD.Services.Services;

/// <summary>
/// 截图索引服务 - 自动将截图内容索引到全文搜索
/// </summary>
public sealed class CaptureIndexingService : IKernelComponent
{
    private readonly IFullTextSearchService _searchService;
    private readonly IEventBus _eventBus;
    private readonly IKernelLogger? _logger;
    private IDisposable? _subscription;
    private bool _disposed;

    public string Name => nameof(CaptureIndexingService);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public CaptureIndexingService(
        IFullTextSearchService searchService,
        IEventBus eventBus,
        IKernelLogger? logger = null)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 订阅截图完成事件
            _subscription = _eventBus.Subscribe<CaptureCompletedEvent>(
                new CaptureCompletedEventHandler(this));

            HealthStatus = HealthStatus.Healthy;
            _logger?.LogInformation("CaptureIndexingService initialized");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            HealthStatus = HealthStatus.Unhealthy;
            _logger?.LogError($"Failed to initialize CaptureIndexingService: {ex.Message}");
            throw;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("CaptureIndexingService started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _subscription?.Dispose();
        _logger?.LogInformation("CaptureIndexingService stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理截图完成事件
    /// </summary>
    private async Task OnCaptureCompletedAsync(CaptureCompletedEvent @event)
    {
        try
        {
            var document = new CaptureDocument
            {
                Id = @event.CaptureId,
                Timestamp = @event.Timestamp,
                Title = @event.WindowTitle ?? @event.ProcessName ?? "Unknown",
                Content = @event.OcrText ?? "",
                FilePath = @event.FilePath,
                ProcessName = @event.ProcessName ?? "",
                WindowTitle = @event.WindowTitle ?? "",
                Tags = @event.Tags ?? Array.Empty<string>(),
                DisplayIndex = @event.DisplayIndex
            };

            await _searchService.IndexCaptureAsync(document);
            _logger?.LogDebug($"Indexed capture: {@event.CaptureId}");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to index capture {@event.CaptureId}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _subscription?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// 截图完成事件
    /// </summary>
    public class CaptureCompletedEvent : IKernelEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
        public string EventType => nameof(CaptureCompletedEvent);
        public string Source => nameof(CaptureIndexingService);
        public string? CorrelationId { get; init; }

        public string CaptureId { get; init; } = "";
        public string FilePath { get; init; } = "";
        public string? ProcessName { get; init; }
        public string? WindowTitle { get; init; }
        public string? OcrText { get; init; }
        public string[]? Tags { get; init; }
        public int DisplayIndex { get; init; }
    }

    /// <summary>
    /// 截图完成事件处理器
    /// </summary>
    private class CaptureCompletedEventHandler : IEventHandler<CaptureCompletedEvent>
    {
        private readonly CaptureIndexingService _service;

        public CaptureCompletedEventHandler(CaptureIndexingService service)
        {
            _service = service;
        }

        public Task HandleAsync(CaptureCompletedEvent @event, CancellationToken cancellationToken)
        {
            return _service.OnCaptureCompletedAsync(@event);
        }
    }
}