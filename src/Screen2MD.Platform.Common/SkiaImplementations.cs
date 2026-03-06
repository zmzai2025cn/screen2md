using SkiaSharp;
using Screen2MD.Abstractions;

namespace Screen2MD.Platform.Common;

/// <summary>
/// SkiaSharp 实现的捕获图像
/// 跨平台，支持 Windows/Linux/Mac
/// </summary>
public sealed class SkiaCapturedImage : ICapturedImage
{
    private readonly SKBitmap _bitmap;
    private bool _disposed;

    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;
    public DateTimeOffset Timestamp { get; }
    public int DisplayIndex { get; }

    public SkiaCapturedImage(SKBitmap bitmap, int displayIndex = 0)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        Timestamp = DateTimeOffset.UtcNow;
        DisplayIndex = displayIndex;
    }

    public SkiaCapturedImage(byte[] imageData, int displayIndex = 0)
    {
        _bitmap = SKBitmap.Decode(imageData) ?? throw new ArgumentException("Invalid image data");
        Timestamp = DateTimeOffset.UtcNow;
        DisplayIndex = displayIndex;
    }

    public SKBitmap ToSKBitmap() => _bitmap;

    public async Task SaveAsync(string filePath, ImageFormat format = ImageFormat.Png, int quality = 95)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SkiaCapturedImage));

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var skFormat = format switch
        {
            ImageFormat.Png => SKEncodedImageFormat.Png,
            ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            ImageFormat.Webp => SKEncodedImageFormat.Webp,
            ImageFormat.Bmp => SKEncodedImageFormat.Bmp,
            _ => SKEncodedImageFormat.Png
        };

        using var data = _bitmap.Encode(skFormat, quality);
        await File.WriteAllBytesAsync(filePath, data.ToArray());
    }

    public byte[] ToByteArray(ImageFormat format = ImageFormat.Png, int quality = 95)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SkiaCapturedImage));

        var skFormat = format switch
        {
            ImageFormat.Png => SKEncodedImageFormat.Png,
            ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            ImageFormat.Webp => SKEncodedImageFormat.Webp,
            ImageFormat.Bmp => SKEncodedImageFormat.Bmp,
            _ => SKEncodedImageFormat.Png
        };

        using var data = _bitmap.Encode(skFormat, quality);
        return data.ToArray();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _bitmap.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// SkiaSharp 图像处理器
/// 纯算法实现，完全跨平台
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    public Task<string> ComputeHashAsync(ICapturedImage image)
    {
        using var skiaImage = image.ToSKBitmap();
        
        // 使用感知哈希算法 (pHash)
        // 缩放为 32x32，转换为灰度，计算 DCT，取低频部分
        using var resized = skiaImage.Resize(new SKImageInfo(32, 32), SKFilterQuality.Medium);
        using var gray = new SKBitmap(32, 32, SKColorType.Gray8, SKAlphaType.Opaque);
        
        // 转换为灰度
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                var color = resized.GetPixel(x, y);
                var grayValue = (byte)((color.Red + color.Green + color.Blue) / 3);
                gray.SetPixel(x, y, new SKColor(grayValue, grayValue, grayValue));
            }
        }

        // 简单的平均哈希（简化版 pHash）
        long sum = 0;
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                sum += gray.GetPixel(x, y).Red;

        var avg = sum / 1024;
        var hash = new System.Text.StringBuilder(256);
        
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                hash.Append(gray.GetPixel(x, y).Red > avg ? '1' : '0');

        return Task.FromResult(hash.ToString());
    }

    public async Task<double> ComputeSimilarityAsync(ICapturedImage image1, ICapturedImage image2)
    {
        var hash1 = await ComputeHashAsync(image1);
        var hash2 = await ComputeHashAsync(image2);

        // 计算汉明距离
        int distance = 0;
        for (int i = 0; i < hash1.Length && i < hash2.Length; i++)
        {
            if (hash1[i] != hash2[i]) distance++;
        }

        // 转换为相似度 (0-1)
        return 1.0 - (double)distance / hash1.Length;
    }

    public Task<ICapturedImage> BlurRegionAsync(ICapturedImage image, Rectangle region, int radius)
    {
        using var source = image.ToSKBitmap();
        var result = new SKBitmap(source.Width, source.Height);
        
        // 复制原图
        source.CopyTo(result);

        // 创建模糊区域
        using var paint = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateBlur(radius, radius)
        };

        using var canvas = new SKCanvas(result);
        
        // 保存画布状态
        canvas.Save();
        
        // 设置裁剪区域
        canvas.ClipRect(new SKRect(region.X, region.Y, region.Right, region.Bottom));
        
        // 绘制模糊效果
        canvas.DrawBitmap(source, 0, 0, paint);
        
        // 恢复画布
        canvas.Restore();

        return Task.FromResult<ICapturedImage>(new SkiaCapturedImage(result, image.DisplayIndex));
    }

    public Task<ICapturedImage> ResizeAsync(ICapturedImage image, int width, int height)
    {
        using var source = image.ToSKBitmap();
        var resized = source.Resize(new SKImageInfo(width, height), SKFilterQuality.High);
        
        return Task.FromResult<ICapturedImage>(new SkiaCapturedImage(resized, image.DisplayIndex));
    }

    public Task<double> ComputeEntropyAsync(ICapturedImage image)
    {
        using var skiaImage = image.ToSKBitmap();
        
        // 计算灰度直方图
        int[] histogram = new int[256];
        int totalPixels = skiaImage.Width * skiaImage.Height;

        // 采样以提高性能
        int sampleStep = Math.Max(1, totalPixels / 10000);
        int sampledPixels = 0;

        for (int y = 0; y < skiaImage.Height; y += sampleStep)
        {
            for (int x = 0; x < skiaImage.Width; x += sampleStep)
            {
                var color = skiaImage.GetPixel(x, y);
                int gray = (color.Red + color.Green + color.Blue) / 3;
                histogram[gray]++;
                sampledPixels++;
            }
        }

        // 计算熵
        double entropy = 0;
        for (int i = 0; i < 256; i++)
        {
            if (histogram[i] > 0)
            {
                double probability = (double)histogram[i] / sampledPixels;
                entropy -= probability * Math.Log2(probability);
            }
        }

        return Task.FromResult(entropy);
    }
}

/// <summary>
/// 显示器信息实现
/// </summary>
public sealed class DisplayInfo : IDisplayInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = "";
    public bool IsPrimary { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public float DpiScale { get; set; }

    public Rectangle Bounds => new Rectangle(X, Y, Width, Height);
}