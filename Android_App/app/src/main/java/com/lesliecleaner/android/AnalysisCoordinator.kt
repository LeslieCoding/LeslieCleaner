package com.lesliecleaner.android

import android.content.Context
import android.os.SystemClock
import java.util.concurrent.atomic.AtomicBoolean
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.withContext

data class AnalysisCompleted(
    val inventory: List<InstalledAppInfo>,
    val coverage: Coverage,
    val summary: InventorySummary,
    val health: DeviceHealthSnapshot,
    val delta: InventoryDelta,
    val junk: JunkAnalysis,
    val security: SecuritySnapshot,
    val startup: StartupSnapshot,
    val durationMs: Long
)

sealed interface AnalysisRunResult {
    data class Completed(val value: AnalysisCompleted) : AnalysisRunResult
    data class Failed(val kind: FailureKind, val detail: String, val durationMs: Long) : AnalysisRunResult
    data object Busy : AnalysisRunResult
}

class AnalysisCoordinator(
    context: Context,
    private val snapshotStore: InventorySnapshotStore
) {
    private val appContext = context.applicationContext
    private val running = AtomicBoolean(false)

    suspend fun run(): AnalysisRunResult {
        if (!running.compareAndSet(false, true)) return AnalysisRunResult.Busy
        val startedAt = SystemClock.elapsedRealtime()
        return try {
            withContext(Dispatchers.IO) {
                ensureActive()
                val inventoryResult = PackageInventoryReader(appContext).read()
                ensureActive()
                when (inventoryResult) {
                    is InventoryResult.Failed -> AnalysisRunResult.Failed(
                        inventoryResult.kind,
                        inventoryResult.detail,
                        SystemClock.elapsedRealtime() - startedAt
                    )
                    is InventoryResult.Verified -> AnalysisRunResult.Completed(
                        AnalysisCompleted(
                            inventory = inventoryResult.items,
                            coverage = inventoryResult.coverage,
                            summary = inventoryResult.summary,
                            health = DeviceHealthReader(appContext).read(),
                            delta = snapshotStore.compareAndStore(inventoryResult.items),
                            junk = JunkCandidateReader(appContext).read(),
                            security = SecuritySignalReader(appContext).read(inventoryResult.items),
                            startup = StartupSignalReader(appContext).read(inventoryResult.items),
                            durationMs = SystemClock.elapsedRealtime() - startedAt
                        )
                    )
                }
            }
        } finally {
            running.set(false)
        }
    }
}
