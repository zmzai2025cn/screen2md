using Screen2MD.Kernel.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;

namespace Screen2MD.Kernel.Services;

/// <summary>
/// 内存事件总线 - 高性能异步消息传递
/// </summary>
public sealed class EventBus : IEventBus, IKernelComponent
{
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly Channel<IKernelEvent> _eventChannel;
    private readonly Task _dispatchTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly IKernelLogger _logger;
    private bool _disposed;

    public string Name => nameof(EventBus);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;

    public EventBus(ILogManager? logManager = null)
    {
        _logger = logManager?.GetLogger(nameof(EventBus)) ?? new FallbackEventBusLogger();
        _eventChannel = Channel.CreateUnbounded<IKernelEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        
        _dispatchTask = Task.Run(DispatchLoopAsync);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Healthy;
        _logger.LogInformation("EventBus initialized");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Unhealthy;
        _eventChannel.Writer.Complete();
        return Task.CompletedTask;
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : IKernelEvent
    {
        if (_disposed)
            return Task.CompletedTask;

        return _eventChannel.Writer.WriteAsync(@event, cancellationToken).AsTask();
    }

    public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IKernelEvent
    {
        var eventType = typeof(TEvent);
        var subscription = new Subscription(this, eventType, handler);
        
        var subs = _subscriptions.GetOrAdd(eventType, _ => new List<Subscription>());
        lock (subs)
        {
            subs.Add(subscription);
        }
        
        _logger.LogDebug($"Subscribed to {eventType.Name}");
        return subscription;
    }

    private async Task DispatchLoopAsync()
    {
        try
        {
            await foreach (var @event in _eventChannel.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    await DispatchEventAsync(@event);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error dispatching event {@event.EventType}: {ex.Message}", ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger.LogError($"EventBus dispatch loop error: {ex.Message}", ex);
        }
    }

    private async Task DispatchEventAsync(IKernelEvent @event)
    {
        var eventType = @event.GetType();
        
        // 获取该类型及其基类型的订阅
        var typesToCheck = new List<Type> { eventType };
        var baseType = eventType.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            typesToCheck.Add(baseType);
            baseType = baseType.BaseType;
        }

        var handlers = new List<Func<IKernelEvent, Task>>();
        
        foreach (var type in typesToCheck)
        {
            if (_subscriptions.TryGetValue(type, out var subs))
            {
                lock (subs)
                {
                    foreach (var sub in subs.Where(s => !s.IsDisposed))
                    {
                        handlers.Add(sub.InvokeAsync);
                    }
                }
            }
        }

        if (handlers.Count == 0)
            return;

        // 并行执行所有处理器
        var tasks = handlers.Select(h => Task.Run(async () =>
        {
            try
            {
                await h(@event);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Event handler error: {ex.Message}", ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    private void Unsubscribe(Subscription subscription)
    {
        if (_subscriptions.TryGetValue(subscription.EventType, out var subs))
        {
            lock (subs)
            {
                subs.Remove(subscription);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _eventChannel.Writer.Complete();
        
        try
        {
            _dispatchTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch { }

        _cts.Dispose();
    }

    /// <summary>
    /// 订阅对象
    /// </summary>
    private class Subscription : IDisposable
    {
        private readonly EventBus _bus;
        public Type EventType { get; }
        private readonly object _handler;
        public bool IsDisposed { get; private set; }

        public Subscription(EventBus bus, Type eventType, object handler)
        {
            _bus = bus;
            EventType = eventType;
            _handler = handler;
        }

        public Task InvokeAsync(IKernelEvent @event)
        {
            if (IsDisposed)
                return Task.CompletedTask;

            // 使用反射调用处理器
            var method = _handler.GetType().GetMethod("HandleAsync");
            if (method != null)
            {
                return (Task)method.Invoke(_handler, new object[] { @event, CancellationToken.None })!;
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            _bus.Unsubscribe(this);
        }
    }

    /// <summary>
    /// 备用日志
    /// </summary>
    private class FallbackEventBusLogger : IKernelLogger
    {
        public void LogTrace(string message, Exception? exception = null) { }
        public void LogDebug(string message, Exception? exception = null) { }
        public void LogInformation(string message, Exception? exception = null) => 
            Console.WriteLine($"[INFO] {message}");
        public void LogWarning(string message, Exception? exception = null) => 
            Console.WriteLine($"[WARN] {message}");
        public void LogError(string message, Exception? exception = null) => 
            Console.Error.WriteLine($"[ERROR] {message}");
        public void LogFatal(string message, Exception? exception = null) => 
            Console.Error.WriteLine($"[FATAL] {message}");
    }
}

/// <summary>
/// 插件主机 - 动态加载和管理插件
/// </summary>
public sealed class PluginHost : IPluginHost, IDisposable
{
    private readonly Dictionary<string, LoadedPlugin> _plugins = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly IKernelLogger _logger;
    private readonly IConfigurationManager _configuration;
    private readonly object _lock = new();
    private bool _disposed;

    public string Name => nameof(PluginHost);
    public HealthStatus HealthStatus { get; private set; } = HealthStatus.Unknown;
    public IEnumerable<IPlugin> LoadedPlugins => _plugins.Values.Select(p => p.Instance);

    public PluginHost(
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        ILogManager logManager,
        IConfigurationManager configuration)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logManager.GetLogger(nameof(PluginHost));
        _configuration = configuration;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Healthy;
        _logger.LogInformation("PluginHost initialized");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        // 自动加载已配置的插件
        var pluginPaths = _configuration.GetValue<string[]>("plugins.auto_load", Array.Empty<string>());
        foreach (var path in pluginPaths)
        {
            try
            {
                LoadPlugin(path);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to auto-load plugin {path}: {ex.Message}", ex);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        HealthStatus = HealthStatus.Unhealthy;
        
        // 卸载所有插件
        var pluginNames = _plugins.Keys.ToList();
        foreach (var name in pluginNames)
        {
            try
            {
                UnloadPlugin(name);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error unloading plugin {name}: {ex.Message}", ex);
            }
        }

        return Task.CompletedTask;
    }

    public IPlugin LoadPlugin(string pluginPath)
    {
        if (!File.Exists(pluginPath))
            throw new FileNotFoundException($"Plugin not found: {pluginPath}");

        lock (_lock)
        {
            // 加载程序集
            var assembly = Assembly.LoadFrom(pluginPath);
            
            // 查找插件类型
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .ToList();

            if (pluginTypes.Count == 0)
                throw new InvalidOperationException($"No plugin implementation found in {pluginPath}");

            // 实例化插件（使用第一个找到的类型）
            var pluginType = pluginTypes.First();
            var instance = (IPlugin)Activator.CreateInstance(pluginType)!;

            // 检查名称冲突
            if (_plugins.ContainsKey(instance.Name))
            {
                throw new InvalidOperationException($"Plugin with name '{instance.Name}' is already loaded");
            }

            // 创建上下文
            var context = new PluginContext(this, instance.Name);

            // 初始化插件
            try
            {
                instance.Initialize(context);
                
                var loadedPlugin = new LoadedPlugin
                {
                    Instance = instance,
                    Assembly = assembly,
                    Path = pluginPath,
                    LoadedAt = DateTimeOffset.UtcNow
                };

                _plugins[instance.Name] = loadedPlugin;
                
                _logger.LogInformation($"Plugin '{instance.Name}' v{instance.Version} loaded successfully");
                
                // 发布插件加载事件
                _ = _eventBus.PublishAsync(new PluginLoadedEvent
                {
                    EventId = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    EventType = nameof(PluginLoadedEvent),
                    Source = nameof(PluginHost),
                    PluginName = instance.Name,
                    PluginVersion = instance.Version.ToString()
                });

                return instance;
            }
            catch (Exception ex)
            {
                // 初始化失败，清理
                try { instance.Dispose(); } catch { }
                throw new InvalidOperationException($"Failed to initialize plugin '{instance.Name}': {ex.Message}", ex);
            }
        }
    }

    public void UnloadPlugin(string pluginName)
    {
        lock (_lock)
        {
            if (!_plugins.TryGetValue(pluginName, out var loadedPlugin))
            {
                _logger.LogWarning($"Plugin '{pluginName}' not found for unloading");
                return;
            }

            try
            {
                loadedPlugin.Instance.Dispose();
                _logger.LogInformation($"Plugin '{pluginName}' unloaded");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disposing plugin '{pluginName}': {ex.Message}", ex);
            }
            finally
            {
                _plugins.Remove(pluginName);
                
                // 注意：.NET Core不支持真正的程序集卸载
                // 需要等到AppDomain卸载或进程重启
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopAsync().Wait();
    }

    /// <summary>
    /// 已加载的插件信息
    /// </summary>
    private class LoadedPlugin
    {
        public IPlugin Instance { get; set; } = null!;
        public Assembly Assembly { get; set; } = null!;
        public string Path { get; set; } = null!;
        public DateTimeOffset LoadedAt { get; set; }
    }

    /// <summary>
    /// 插件上下文实现
    /// </summary>
    private class PluginContext : IPluginContext
    {
        private readonly PluginHost _host;
        private readonly string _pluginName;

        public PluginContext(PluginHost host, string pluginName)
        {
            _host = host;
            _pluginName = pluginName;
        }

        public IServiceProvider Services => _host._serviceProvider;
        public IConfigurationManager Configuration => _host._configuration;
        public IEventBus EventBus => _host._eventBus;
        public IKernelLogger Logger => _host._logger;
    }
}

/// <summary>
/// 插件加载事件
/// </summary>
public class PluginLoadedEvent : IKernelEvent
{
    public Guid EventId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string PluginName { get; set; } = string.Empty;
    public string PluginVersion { get; set; } = string.Empty;
}