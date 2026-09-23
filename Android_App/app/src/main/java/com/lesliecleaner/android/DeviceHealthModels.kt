package com.lesliecleaner.android

sealed interface HealthMetric<out T> {
    data class Available<T>(val value: T) : HealthMetric<T>
    data class Unavailable(val reason: String) : HealthMetric<Nothing>
}

data class StorageSnapshot(val totalBytes: Long, val availableBytes: Long) {
    val usedBytes: Long get() = (totalBytes - availableBytes).coerceAtLeast(0L)
    val usedPercent: Int
        get() = if (totalBytes > 0L) ((usedBytes * 100L) / totalBytes).toInt().coerceIn(0, 100) else 0
}

data class MemorySnapshot(val totalBytes: Long, val availableBytes: Long, val lowMemory: Boolean)

data class BatterySnapshot(val percent: Int?, val isCharging: Boolean?)

data class DeviceHealthSnapshot(
    val storage: HealthMetric<StorageSnapshot>,
    val memory: HealthMetric<MemorySnapshot>,
    val battery: HealthMetric<BatterySnapshot>,
    val collectedAtEpochMs: Long,
    val warnings: List<String>
)

fun formatBytes(bytes: Long): String {
    if (bytes < 0L) return "未提供"
    val units = listOf("B", "KB", "MB", "GB", "TB")
    var value = bytes.toDouble()
    var index = 0
    while (value >= 1024.0 && index < units.lastIndex) {
        value /= 1024.0
        index++
    }
    return if (index == 0) "${bytes} ${units[index]}" else "%.1f %s".format(java.util.Locale.US, value, units[index])
}

fun healthMetricLabel(metric: HealthMetric<*>): String = when (metric) {
    is HealthMetric.Available<*> -> "可用"
    is HealthMetric.Unavailable -> "未提供"
}
