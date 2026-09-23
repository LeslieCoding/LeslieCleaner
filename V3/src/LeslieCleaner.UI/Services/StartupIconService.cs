using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LeslieCleaner.UI.Services;

/// <summary>
/// 文件图标提取服务（SHGetFileInfo 小图标 → BitmapSource，按路径缓存）。
/// </summary>
public static class StartupIconService
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;

    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 提取文件关联小图标；文件不存在或提取失败返回 <see langword="null"/>（UI 用兜底字形）。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    public static ImageSource? GetIcon(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        string path = filePath;
        return Cache.GetOrAdd(path, static key =>
        {
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                var shinfo = new ShFileInfo();
                IntPtr result = SHGetFileInfo(key, 0, ref shinfo, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiSmallIcon);
                if (result == IntPtr.Zero || shinfo.Icon == IntPtr.Zero)
                {
                    return null;
                }

                hIcon = shinfo.Icon;
                BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // 提取失败返回 null，UI 使用兜底字形（静态服务无日志通道）。
                return null;
            }
            finally
            {
                if (hIcon != IntPtr.Zero)
                {
                    _ = DestroyIcon(hIcon);
                }
            }
        });
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, ref ShFileInfo fileInfo, uint size, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
