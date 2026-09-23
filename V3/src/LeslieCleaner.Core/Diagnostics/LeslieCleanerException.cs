namespace LeslieCleaner.Core.Diagnostics;

/// <summary>
/// LeslieCleaner 领域异常基类。
/// 错误策略（docs/01-architecture.md 有完整说明）：领域错误一律抛出异常并记录日志，
/// 不使用 Result&lt;T&gt; 双通道，避免错误被静默吞掉。
/// </summary>
public class LeslieCleanerException : Exception
{
    /// <summary>以描述信息创建异常。</summary>
    /// <param name="message">错误描述。</param>
    public LeslieCleanerException(string message) : base(message)
    {
    }

    /// <summary>以描述信息与内部异常创建异常。</summary>
    /// <param name="message">错误描述。</param>
    /// <param name="innerException">内部异常。</param>
    public LeslieCleanerException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>隔离区操作失败。</summary>
public sealed class QuarantineException : LeslieCleanerException
{
    /// <summary>以描述信息创建异常。</summary>
    /// <param name="message">错误描述。</param>
    public QuarantineException(string message) : base(message)
    {
    }

    /// <summary>以描述信息与内部异常创建异常。</summary>
    /// <param name="message">错误描述。</param>
    /// <param name="innerException">内部异常。</param>
    public QuarantineException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>备份/恢复操作失败。</summary>
public sealed class BackupException : LeslieCleanerException
{
    /// <summary>以描述信息创建异常。</summary>
    /// <param name="message">错误描述。</param>
    public BackupException(string message) : base(message)
    {
    }

    /// <summary>以描述信息与内部异常创建异常。</summary>
    /// <param name="message">错误描述。</param>
    /// <param name="innerException">内部异常。</param>
    public BackupException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>配置读写失败。</summary>
public sealed class ConfigurationException : LeslieCleanerException
{
    /// <summary>以描述信息创建异常。</summary>
    /// <param name="message">错误描述。</param>
    public ConfigurationException(string message) : base(message)
    {
    }

    /// <summary>以描述信息与内部异常创建异常。</summary>
    /// <param name="message">错误描述。</param>
    /// <param name="innerException">内部异常。</param>
    public ConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
