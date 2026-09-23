package com.lesliecleaner.android

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class AuditExportTest {
    @Test
    fun exportContractContainsOnlyRedactedFields() {
        val export = ScanAuditRecord("audit_export", "requested", 3, "redacted").toJsonLine()
        assertTrue(export.contains("audit_export"))
        assertTrue(export.contains("redacted"))
        assertFalse(export.contains("content://"))
        assertFalse(export.contains("/storage/"))
    }
}
