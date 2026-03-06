using Screen2MD.Abstractions;
using Microsoft.Extensions.Logging;

namespace Screen2MD.Core.Services;

/// <summary>
/// OCR 服务 - 跨平台业务逻辑
/// </summary>
public sealed class OcrService
{
    private readonly IOcrEngine _ocrEngine;
    private readonly ILogger<OcrService>? _logger;
    private readonly string[] _defaultLanguages;
    private readonly int _timeoutMs;

    public OcrService(
        IOcrEngine ocrEngine,
        string[]? defaultLanguages = null,
        int timeoutMs = 30000,
        ILogger<OcrService>? logger = null)
    {
        _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));
        _logger = logger;
        _defaultLanguages = defaultLanguages ?? new[] { "eng", "chi_sim" };
        _timeoutMs = timeoutMs;
    }

    /// <summary>
    /// 识别图像中的文字
    /// </summary>
    public async Task<OcrResult> RecognizeAsync(
        ICapturedImage image, 
        string[]? languages = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new OcrOptions
            {
                Languages = languages ?? _defaultLanguages,
                TimeoutMs = _timeoutMs,
                PreserveFormatting = true
            };

            _logger?.LogDebug($"Starting OCR for image {image.Width}x{image.Height}");
            
            var result = await _ocrEngine.RecognizeAsync(image, options, cancellationToken);
            
            if (result.Success)
            {
                _logger?.LogInformation($"OCR completed: {result.Text.Length} chars, confidence: {result.Confidence:P}");
            }
            else
            {
                _logger?.LogWarning($"OCR failed: {result.ErrorMessage}");
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // 取消操作应该重新抛出，而不是返回失败结果
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "OCR recognition failed");
            return new OcrResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 检查 OCR 引擎是否可用
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        return await _ocrEngine.IsAvailableAsync();
    }

    /// <summary>
    /// 获取支持的语言
    /// </summary>
    public async Task<IReadOnlyList<string>> GetSupportedLanguagesAsync()
    {
        return await _ocrEngine.GetSupportedLanguagesAsync();
    }
}