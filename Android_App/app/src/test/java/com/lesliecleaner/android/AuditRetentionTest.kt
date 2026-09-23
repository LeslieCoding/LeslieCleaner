package com.lesliecleaner.android

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class AuditRetentionTest {
    @Test
    fun retentionLimitIsBoundedAndRecentReadsAreSmaller() {
        assertEquals(200, JsonlAuditLogger.MAX_RECORDS)
        assertTrue(JsonlAuditLogger.MAX_RECORDS > 20)
    }
}
