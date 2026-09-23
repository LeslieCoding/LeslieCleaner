package com.lesliecleaner.android

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class MaintenanceModuleTest {
    @Test fun sha256VerificationAcceptsOnlyMatchingPayload() {
        val payload = "leslie-rules-3.0.0"
        assertTrue(RuleUpdateStore.verify(payload, RuleUpdateStore.sha256(payload)))
        assertTrue(!RuleUpdateStore.verify(payload, RuleUpdateStore.sha256("other")))
    }

    @Test fun boundedSizeCountsFilesAndStopsAtLimit() {
        val root = createTempDir(prefix = "leslie-junk-")
        try {
            File(root, "a.tmp").writeBytes(ByteArray(3))
            File(root, "b.tmp").writeBytes(ByteArray(5))
            assertEquals(8L, JunkCandidateReader.boundedSize(root, 10))
        } finally { root.deleteRecursively() }
    }
}
