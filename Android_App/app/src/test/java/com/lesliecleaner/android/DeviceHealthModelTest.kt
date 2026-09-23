package com.lesliecleaner.android

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class DeviceHealthModelTest {
    @Test
    fun storageCalculatesBoundedUsage() {
        val storage = StorageSnapshot(totalBytes = 1_000L, availableBytes = 250L)
        assertEquals(750L, storage.usedBytes)
        assertEquals(75, storage.usedPercent)
    }

    @Test
    fun invalidStorageCannotProduceNegativeUsage() {
        val storage = StorageSnapshot(totalBytes = 100L, availableBytes = 200L)
        assertEquals(0L, storage.usedBytes)
        assertEquals(0, storage.usedPercent)
    }

    @Test
    fun unavailableMetricIsExplicit() {
        val metric: HealthMetric<StorageSnapshot> = HealthMetric.Unavailable("missing")
        assertEquals("未提供", healthMetricLabel(metric))
        assertTrue(metric is HealthMetric.Unavailable)
    }

    @Test
    fun byteFormattingIsStable() {
        assertEquals("1.0 KB", formatBytes(1024L))
        assertEquals("未提供", formatBytes(-1L))
    }
}
