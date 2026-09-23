using LeslieCleaner.Core.Models;
using LeslieCleaner.Modules.Uninstaller.Residual;

namespace LeslieCleaner.Modules.Uninstaller.Workflow;

/// <summary>Connects the safe uninstall workflow to the reversible residual engine.</summary>
public sealed class ResidualCleanupRunner : IResidualCleanupRunner
{
    private readonly ResidualScannerEngine _engine;

    /// <summary>Creates an adapter for the existing residual engine.</summary>
    public ResidualCleanupRunner(ResidualScannerEngine engine) => _engine = engine;

    /// <inheritdoc />
    public Task<IReadOnlyList<ResidualItem>> ScanAsync(ResidualQuery query, CancellationToken cancellationToken = default) =>
        _engine.ScanAsync(query, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<CleanResult> CleanAsync(IReadOnlyList<ResidualItem> items, CancellationToken cancellationToken = default) =>
        _engine.CleanResidualsAsync(items, cancellationToken: cancellationToken);
}
