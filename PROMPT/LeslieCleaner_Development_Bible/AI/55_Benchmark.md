# CleanMyPC Next Benchmark Standard

Version: 1.0  
Status: Normative

## Measurements

Record cold start, warm start, page switch, search, rule scan, residual pipeline, registry scan, browser cache scan, backup, restore and deep-scan latency. Record P50/P95/max, item count, bytes, CPU, private memory, working set, allocation rate and failure count.

## Performance objectives

Rule-based scans should complete in seconds for known applications; deep scanning is bounded, cancellable and opt-in. UI actions acknowledge within 100 ms, page switches within 300 ms, and no scan performs synchronous work on the dispatcher thread.

## Acceptance

Benchmarks run on a named machine with a fixed fixture and are compared against the previous baseline. A single fast run cannot establish compliance; regressions over 15% require investigation or an explicit waiver.

## Benchmark dossier

- IDs: `LCN-PERF-001..012`; every record includes machine, OS build, fixture hash, rule/index version, sample size, and cold/warm classification.
- Measure each residual stage independently; deep scan is excluded from normal rule-stage budgets and is separately opt-in.
- Capture P50/P95/max, CPU, working set, private memory, allocations, bytes, items, and errors.
- A regression gate compares like-for-like fixtures; changed fixtures require a new baseline rather than a misleading comparison.
- Any long-running integration benchmark is marked pending if the board or clean Windows image is unavailable.
