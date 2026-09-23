using System.Security.Cryptography;

namespace LeslieCleaner.Modules.Shredder;

/// <summary>
/// 覆写模式表。提供 DoD 5220.22-M 3 遍、DoD ECE 7 遍与 Gutmann 35 遍的
/// 真实字节模式序列；每遍为固定字节、三字节轮换模式或加密随机数。
/// </summary>
public static class ShredPatternProvider
{
    /// <summary>一遍覆写模式。</summary>
    /// <param name="Kind">模式类别。</param>
    /// <param name="Pattern">固定字节序列（轮换填充）；随机遍为 <see langword="null"/>。</param>
    public readonly record struct PassPattern(PassPatternKind Kind, byte[]? Pattern);

    /// <summary>模式类别。</summary>
    public enum PassPatternKind
    {
        /// <summary>固定字节序列按长度轮换填充。</summary>
        Fixed,

        /// <summary>加密随机（RandomNumberGenerator）。</summary>
        Random,
    }

    /// <summary>Gutmann 27 个固定三遍轮换模式（第 5~31 遍，首尾各 4 遍随机）。</summary>
    public static IReadOnlyList<byte[]> GutmannFixedPatterns { get; } =
    [
        [0x55, 0x55, 0x55], [0xAA, 0xAA, 0xAA], [0x92, 0x49, 0x24],
        [0x49, 0x24, 0x92], [0x24, 0x92, 0x49], [0x00, 0x00, 0x00],
        [0x11, 0x11, 0x11], [0x22, 0x22, 0x22], [0x33, 0x33, 0x33],
        [0x44, 0x44, 0x44], [0x55, 0x55, 0x55], [0x66, 0x66, 0x66],
        [0x77, 0x77, 0x77], [0x88, 0x88, 0x88], [0x99, 0x99, 0x99],
        [0xAA, 0xAA, 0xAA], [0xBB, 0xBB, 0xBB], [0xCC, 0xCC, 0xCC],
        [0xDD, 0xDD, 0xDD], [0xEE, 0xEE, 0xEE], [0xFF, 0xFF, 0xFF],
        [0x92, 0x49, 0x24], [0x49, 0x24, 0x92], [0x24, 0x92, 0x49],
        [0x6D, 0xB6, 0xDB], [0xB6, 0xDB, 0x6D], [0xDB, 0x6D, 0xB6],
    ];

    /// <summary>
    /// 获取指定方法的完整遍序列。
    /// </summary>
    /// <param name="method">粉碎方法。</param>
    public static IReadOnlyList<PassPattern> GetPasses(ShredMethod method) => method switch
    {
        ShredMethod.DoD3Pass =>
        [
            new PassPattern(PassPatternKind.Fixed, [0x00]),
            new PassPattern(PassPatternKind.Fixed, [0xFF]),
            new PassPattern(PassPatternKind.Random, null),
        ],
        ShredMethod.DoD7Pass =>
        [
            new PassPattern(PassPatternKind.Fixed, [0xF6]),
            new PassPattern(PassPatternKind.Fixed, [0x00]),
            new PassPattern(PassPatternKind.Fixed, [0xFF]),
            new PassPattern(PassPatternKind.Random, null),
            new PassPattern(PassPatternKind.Fixed, [0x00]),
            new PassPattern(PassPatternKind.Fixed, [0xFF]),
            new PassPattern(PassPatternKind.Random, null),
        ],
        ShredMethod.Gutmann35Pass => BuildGutmannPasses(),
        ShredMethod.SsdSafe => [new PassPattern(PassPatternKind.Random, null)],
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };

    /// <summary>
    /// 按模式填充缓冲区（固定序列轮换 / 加密随机）。
    /// </summary>
    /// <param name="pass">遍模式。</param>
    /// <param name="buffer">目标缓冲区。</param>
    public static void FillBuffer(PassPattern pass, Span<byte> buffer)
    {
        if (pass.Kind == PassPatternKind.Random || pass.Pattern is null)
        {
            RandomNumberGenerator.Fill(buffer);
            return;
        }

        byte[] pattern = pass.Pattern;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = pattern[i % pattern.Length];
        }
    }

    private static IReadOnlyList<PassPattern> BuildGutmannPasses()
    {
        var passes = new List<PassPattern>(35);
        for (int i = 0; i < 4; i++)
        {
            passes.Add(new PassPattern(PassPatternKind.Random, null));
        }

        foreach (byte[] pattern in GutmannFixedPatterns)
        {
            passes.Add(new PassPattern(PassPatternKind.Fixed, pattern));
        }

        for (int i = 0; i < 4; i++)
        {
            passes.Add(new PassPattern(PassPatternKind.Random, null));
        }

        return passes;
    }
}
