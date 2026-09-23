package com.lesliecleaner.android

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class AuditRecordTest {
    @Test
    fun auditRecordIsStructuredAndContainsNoRawTargetData() {
        val json = ScanAuditRecord("package_scan", "verified", 2, "ProviderVisibleOnly").toJsonLine()
        assertTrue(json.startsWith("{\"event\":\"package_scan\""))
        assertTrue(json.contains("\"itemCount\":2"))
        assertFalse(json.contains("/storage/"))
    }

    @Test
    fun auditRecordEscapesQuotesAndNewlines() {
        val json = ScanAuditRecord("scan\"", "failed\n", 0, "none", "bad\"reason").toJsonLine()
        assertTrue(json.contains("scan\\\""))
        assertTrue(json.contains("failed\\n"))
        assertTrue(json.contains("bad\\\"reason"))
    }

    @Test
    fun scanAuditCanCarryBoundedAnalysisMetadata() {
        val json = ScanAuditRecord(
            event = "package_scan",
            result = "verified",
            itemCount = 253,
            coverage = "ProviderVisibleOnly",
            durationMs = 42,
            metadata = "user=100;system=153;enabled=250;disabled=3;debuggable=2"
        ).toJsonLine()
        assertTrue(json.contains("\"durationMs\":42"))
        assertTrue(json.contains("user=100;system=153"))
    }
}
