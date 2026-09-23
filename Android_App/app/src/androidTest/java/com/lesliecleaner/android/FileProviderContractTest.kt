package com.lesliecleaner.android

import android.content.Context
import androidx.core.content.FileProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class FileProviderContractTest {
    private val context: Context
        get() = InstrumentationRegistry.getInstrumentation().targetContext

    @Test
    fun exportIsCacheScopedAndExcludesRawTargets() {
        val logger = JsonlAuditLogger(context)
        logger.append(ScanAuditRecord("instrumented_test", "verified", 1, "redacted"))
        val export = logger.writeRedactedExport(context.cacheDir)
        val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", export)

        assertEquals("content", uri.scheme)
        assertTrue(export.canonicalPath.startsWith(context.cacheDir.canonicalPath))
        assertFalse(export.readText().contains("content://"))
        assertFalse(export.readText().contains("/storage/"))
    }
}
