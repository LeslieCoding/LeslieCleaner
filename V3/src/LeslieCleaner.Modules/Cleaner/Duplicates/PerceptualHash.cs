using System.Drawing;

namespace LeslieCleaner.Modules.Cleaner.Duplicates;

/// <summary>
/// 感知哈希（aHash/dHash 64 位）。经 System.Drawing 解码 JPEG/PNG/BMP，
/// 缩放到 8x8（aHash）与 9x8（dHash）灰度位图计算；汉明距离 ≤ 5 判相似。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class PerceptualHash
{
    /// <summary>相似判定汉明距离阈值。</summary>
    public const int SimilarityThreshold = 5;

    /// <summary>
    /// 计算 aHash（8x8 均值哈希）。
    /// </summary>
    /// <param name="imagePath">图片路径（JPEG/PNG/BMP）。</param>
    public static ulong ComputeAverageHash(string imagePath)
    {
        int[] gray = LoadGrayscale(imagePath, 8, 8);
        int sum = 0;
        foreach (int value in gray)
        {
            sum += value;
        }

        int average = sum / gray.Length;
        ulong hash = 0;
        for (int i = 0; i < gray.Length; i++)
        {
            if (gray[i] >= average)
            {
                hash |= 1UL << i;
            }
        }

        return hash;
    }

    /// <summary>
    /// 计算 dHash（9x8 水平梯度哈希）。
    /// </summary>
    /// <param name="imagePath">图片路径（JPEG/PNG/BMP）。</param>
    public static ulong ComputeDifferenceHash(string imagePath)
    {
        int[] gray = LoadGrayscale(imagePath, 9, 8);
        ulong hash = 0;
        int bit = 0;
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                if (gray[(row * 9) + column] > gray[(row * 9) + column + 1])
                {
                    hash |= 1UL << bit;
                }

                bit++;
            }
        }

        return hash;
    }

    /// <summary>
    /// 两个 64 位哈希的汉明距离。
    /// </summary>
    /// <param name="a">哈希 a。</param>
    /// <param name="b">哈希 b。</param>
    public static int HammingDistance(ulong a, ulong b) => System.Numerics.BitOperations.PopCount(a ^ b);

    /// <summary>判断是否支持的图片扩展名。</summary>
    /// <param name="path">文件路径。</param>
    public static bool IsSupportedImage(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static int[] LoadGrayscale(string imagePath, int width, int height)
    {
        using var source = new Bitmap(imagePath);
        using var scaled = new Bitmap(source, width, height);
        var gray = new int[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = scaled.GetPixel(x, y);
                gray[(y * width) + x] = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
            }
        }

        return gray;
    }
}
