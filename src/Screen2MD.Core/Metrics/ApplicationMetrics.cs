using System.Diagnostics.Metrics;

namespace Screen2MD.Core.Metrics;

/// <summary>
/// 应用性能指标 - 使用System.Diagnostics.Metrics (OpenTelemetry标准)
/// </summary>
public static class ApplicationMetrics
{
    private static readonly Meter Meter = new("Screen2MD", "3.0.0");

    // 截图指标
    public static readonly Counter<long> CapturesTotal = Meter.CreateCounter<long>(
        "screen2md.captures.total",
        description: "Total number of screen captures");

    public static readonly Histogram<double> CaptureLatency = Meter.CreateHistogram<double>(
        "screen2md.capture.latency",
        unit: "ms",
        description: "Screen capture latency in milliseconds");

    // OCR指标
    public static readonly Counter<long> OcrTotal = Meter.CreateCounter<long>(
        "screen2md.ocr.total",
        description: "Total number of OCR operations");

    public static readonly Counter<long> OcrErrors = Meter.CreateCounter<long>(
        "screen2md.ocr.errors",
        description: "Total number of OCR errors");

    public static readonly Histogram<double> OcrLatency = Meter.CreateHistogram<double>(
        "screen2md.ocr.latency",
        unit: "ms",
        description: "OCR processing latency in milliseconds");

    // 搜索指标
    public static readonly Counter<long> SearchesTotal = Meter.CreateCounter<long>(
        "screen2md.searches.total",
        description: "Total number of search operations");

    public static readonly Histogram<double> SearchLatency = Meter.CreateHistogram<double>(
        "screen2md.search.latency",
        unit: "ms",
        description: "Search latency in milliseconds");

    // 存储指标
    public static readonly ObservableGauge<long> StorageUsed = Meter.CreateObservableGauge<long>(
        "screen2md.storage.used.bytes",
        () => GetStorageUsed(),
        unit: "bytes",
        description: "Storage space used in bytes");

    public static readonly ObservableGauge<long> StorageFiles = Meter.CreateObservableGauge<long>(
        "screen2md.storage.files",
        () => GetStorageFiles(),
        description: "Number of stored files");

    // 索引指标
    public static readonly ObservableGauge<long> IndexedDocuments = Meter.CreateObservableGauge<long>(
        "screen2md.index.documents",
        () => GetIndexedDocuments(),
        description: "Number of indexed documents");

    private static long GetStorageUsed()
    {
        // 实际实现中从StorageService获取
        return 0;
    }

    private static long GetStorageFiles()
    {
        // 实际实现中从StorageService获取
        return 0;
    }

    private static long GetIndexedDocuments()
    {
        // 实际实现中从SearchService获取
        return 0;
    }
}
