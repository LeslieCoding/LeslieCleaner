using Serilog.Events;

namespace LeslieCleaner.Infrastructure.Logging;

/// <summary>Validated file-logging retention and rotation settings.</summary>
public sealed record LoggingOptions
{
    /// <summary>Minimum production log level.</summary>
    public LogEventLevel MinimumLevel { get; init; } = LogEventLevel.Information;

    /// <summary>Number of daily/size-rolled files retained; null means unlimited.</summary>
    public int? RetainedFileCountLimit { get; init; } = 31;

    /// <summary>Maximum bytes per log segment before size rotation.</summary>
    public long FileSizeLimitBytes { get; init; } = 100L * 1024 * 1024;

    /// <summary>Validates settings before a logger is created.</summary>
    public void Validate()
    {
        if (RetainedFileCountLimit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(RetainedFileCountLimit));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(FileSizeLimitBytes);
    }
}
