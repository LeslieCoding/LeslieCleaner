using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Diagnostics;

/// <summary>Maps platform exceptions to stable cleanup failure categories without parsing localized messages.</summary>
public static class OperationFailureClassifier
{
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;

    /// <summary>Classifies a cleanup exception for user guidance and retry policy.</summary>
    public static OperationFailureKind Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is QuarantineException quarantine && quarantine.FailureKind != OperationFailureKind.Unknown)
        {
            return quarantine.FailureKind;
        }

        if (exception is BackupException backup && backup.FailureKind != OperationFailureKind.BackupFailed)
        {
            return backup.FailureKind;
        }

        if (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return OperationFailureKind.RequiresElevation;
        }

        if (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return OperationFailureKind.NotFound;
        }

        if (exception is IOException ioException)
        {
            int nativeError = ioException.HResult & 0xFFFF;
            if (nativeError is ErrorSharingViolation or ErrorLockViolation)
            {
                return OperationFailureKind.InUse;
            }
        }

        if (exception.InnerException is not null)
        {
            OperationFailureKind inner = Classify(exception.InnerException);
            if (inner != OperationFailureKind.Unknown)
            {
                return inner;
            }
        }

        return exception is BackupException
            ? OperationFailureKind.BackupFailed
            : OperationFailureKind.Unknown;
    }
}
