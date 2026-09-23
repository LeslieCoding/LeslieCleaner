package com.lesliecleaner.android

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class InventorySnapshotStoreTest {
    @Test
    fun attentionExplanationUsesOnlyObservedFacts() {
        val debuggable = InstalledAppInfo(
            identity = TargetIdentity.PackageName("example.debug"),
            label = "Debug",
            versionName = "1",
            targetSdkVersion = 35,
            lastUpdateTime = 1L,
            isSystemApp = false,
            isEnabled = true,
            isDebuggable = true
        )
        val disabled = debuggable.copy(isDebuggable = false, isEnabled = false)
        val routine = disabled.copy(isEnabled = true)

        org.junit.Assert.assertEquals(InventoryAttention.Debuggable, attentionFor(debuggable))
        org.junit.Assert.assertEquals(InventoryAttention.Disabled, attentionFor(disabled))
        org.junit.Assert.assertEquals(InventoryAttention.None, attentionFor(routine))
    }

    @Test
    fun deltaModelDistinguishesFirstScan() {
        val delta = InventoryDelta(2, 0, 0, false)
        assertTrue(delta.added == 2)
        assertFalse(delta.hasPrevious)
    }
}
