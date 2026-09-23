using System.IO.MemoryMappedFiles;
using System.Text;

namespace LeslieCleaner.Infrastructure.Indexing;

/// <summary>
/// 基于 Memory-Mapped File 的字符串外存存储。用于百万级路径清单的
/// 内存外排序/去重：字符串以 [int32 字节长度 + UTF8 载荷] 顺序追加到映射文件，
/// 排序阶段仅将 (offset, length) 索引载入内存，比较直接在映射视图上按字节进行。
/// 容量不足时按 2 倍扩容（新建映射并拷贝）。
/// </summary>
public sealed class MmfStringStore : IDisposable
{
    private const long InitialCapacity = 16L * 1024 * 1024;
    private const int MaxEntryBytes = 4 * 1024;

    private readonly string _backingFile;
    private readonly FileStream _backingStream;
    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _accessor;
    private long _capacity;
    private long _writePosition;
    private readonly List<(long Offset, int ByteLength)> _entries = [];
    private int _disposed;

    /// <summary>创建存储（临时支撑文件位于系统临时目录，Dispose 时删除）。</summary>
    public MmfStringStore()
    {
        _backingFile = Path.Combine(Path.GetTempPath(), $"leslie-mmf-{Guid.NewGuid():N}.bin");
        _capacity = InitialCapacity;
        _backingStream = new FileStream(_backingFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        (_mmf, _accessor) = CreateMapping(_capacity);
    }

    /// <summary>已追加的条目数。</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// 追加一个字符串。超长字符串（UTF8 超过 4KB）将被截断到限制内。
    /// </summary>
    /// <param name="value">待追加内容。</param>
    public void Add(ReadOnlySpan<char> value)
    {
        ThrowIfDisposed();
        int byteCount = Encoding.UTF8.GetByteCount(value);
        if (byteCount > MaxEntryBytes)
        {
            // 先按字符截断再迭代收敛，保证 UTF8 字节数不超限（GetBytes 目标缓冲不足会抛异常）
            int charCount = Math.Min(value.Length, MaxEntryBytes);
            while (Encoding.UTF8.GetByteCount(value[..charCount]) > MaxEntryBytes)
            {
                charCount -= 16;
            }

            value = value[..charCount];
            byteCount = Encoding.UTF8.GetByteCount(value);
        }

        EnsureCapacity(_writePosition + sizeof(int) + byteCount);

        long offset = _writePosition;
        _accessor.Write(offset, byteCount);
        Span<byte> buffer = byteCount <= 512 ? stackalloc byte[byteCount] : new byte[byteCount];
        Encoding.UTF8.GetBytes(value, buffer);
        _accessor.WriteArray(offset + sizeof(int), buffer.ToArray(), 0, byteCount);
        _entries.Add((offset, byteCount));
        _writePosition = offset + sizeof(int) + byteCount;
    }

    /// <summary>
    /// 对全部条目按序号字典序（UTF8 字节序）排序并去重，返回去重后的字符串列表。
    /// 比较直接在映射视图上进行，不将字符串物化进托管堆。
    /// </summary>
    public IReadOnlyList<string> SortedDistinct()
    {
        ThrowIfDisposed();
        (long Offset, int ByteLength)[] index = [.. _entries];
        Array.Sort(index, CompareEntries);

        var result = new List<string>(index.Length);
        (long Offset, int ByteLength)? previous = null;
        foreach ((long offset, int byteLength) in index)
        {
            if (previous is { } prev && CompareEntries((offset, byteLength), prev) == 0)
            {
                continue;
            }

            result.Add(ReadString(offset, byteLength));
            previous = (offset, byteLength);
        }

        return result;
    }

    /// <summary>释放映射与支撑流并删除临时支撑文件。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _accessor.Dispose();
        _mmf.Dispose();
        _backingStream.Dispose();
        try
        {
            if (File.Exists(_backingFile))
            {
                File.Delete(_backingFile);
            }
        }
        catch (IOException)
        {
            // 临时文件清理失败交由 OS 回收
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private int CompareEntries((long Offset, int ByteLength) a, (long Offset, int ByteLength) b)
    {
        int common = Math.Min(a.ByteLength, b.ByteLength);
        for (int i = 0; i < common; i++)
        {
            int diff = _accessor.ReadByte(a.Offset + sizeof(int) + i) - _accessor.ReadByte(b.Offset + sizeof(int) + i);
            if (diff != 0)
            {
                return diff;
            }
        }

        return a.ByteLength.CompareTo(b.ByteLength);
    }

    private string ReadString(long offset, int byteLength)
    {
        byte[] buffer = new byte[byteLength];
        _accessor.ReadArray(offset + sizeof(int), buffer, 0, byteLength);
        return Encoding.UTF8.GetString(buffer);
    }

    private void EnsureCapacity(long required)
    {
        if (required <= _capacity)
        {
            return;
        }

        long newCapacity = Math.Max(_capacity * 2, required + InitialCapacity);
        // 同一支撑流上创建新映射（同句柄多映射合法；另开句柄会与 FileShare.None 冲突）
        _backingStream.SetLength(newCapacity);
        (MemoryMappedFile newMmf, MemoryMappedViewAccessor newAccessor) = CreateMapping(newCapacity);
        if (_writePosition > 0)
        {
            using MemoryMappedViewStream source = _mmf.CreateViewStream(0, _writePosition, MemoryMappedFileAccess.Read);
            using MemoryMappedViewStream target = newMmf.CreateViewStream(0, _writePosition, MemoryMappedFileAccess.Write);
            source.CopyTo(target);
        }

        _accessor.Dispose();
        _mmf.Dispose();
        _mmf = newMmf;
        _accessor = newAccessor;
        _capacity = newCapacity;
    }

    private (MemoryMappedFile, MemoryMappedViewAccessor) CreateMapping(long capacity)
    {
        if (_backingStream.Length < capacity)
        {
            _backingStream.SetLength(capacity);
        }

        MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(
            _backingStream, mapName: null, capacity, MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None, leaveOpen: true);
        return (mmf, mmf.CreateViewAccessor(0, capacity, MemoryMappedFileAccess.ReadWrite));
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(MmfStringStore));
        }
    }
}
