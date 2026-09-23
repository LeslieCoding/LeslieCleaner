package com.lesliecleaner.android

import android.content.Context
import java.io.File
import java.io.FileOutputStream

class JsonlAuditLogger(context: Context) {
    companion object { const val MAX_RECORDS = 200 }
    private val file = File(context.filesDir, "audit-events.jsonl")

    @Synchronized
    fun append(record: ScanAuditRecord) {
        FileOutputStream(file, true).use { stream ->
            stream.write((record.toJsonLine() + "\n").toByteArray(Charsets.UTF_8))
            stream.fd.sync()
        }
        trimToLimit()
    }

    @Synchronized
    fun readRecent(limit: Int = 20): List<String> {
        if (!file.exists()) return emptyList()
        return file.readLines(Charsets.UTF_8).takeLast(limit.coerceIn(1, MAX_RECORDS))
    }

    @Synchronized
    fun exportRedacted(limit: Int = MAX_RECORDS): String = readRecent(limit).joinToString("\n", postfix = "\n")

    @Synchronized
    fun writeRedactedExport(cacheDir: File): File {
        val output = File(cacheDir, "lesliecleaner-audit-export.jsonl")
        FileOutputStream(output, false).use { stream ->
            stream.write(exportRedacted().toByteArray(Charsets.UTF_8))
            stream.fd.sync()
        }
        return output
    }

    @Synchronized
    fun clear() {
        if (file.exists()) file.delete()
    }

    private fun trimToLimit() {
        val lines = file.readLines(Charsets.UTF_8)
        if (lines.size <= MAX_RECORDS) return
        FileOutputStream(file, false).use { stream ->
            stream.write(lines.takeLast(MAX_RECORDS).joinToString("\n", postfix = "\n").toByteArray(Charsets.UTF_8))
            stream.fd.sync()
        }
    }
}
