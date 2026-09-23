package com.lesliecleaner.android

data class ScanAuditRecord(
    val event: String,
    val result: String,
    val itemCount: Int,
    val coverage: String,
    val failureKind: String? = null,
    val durationMs: Long? = null,
    val metadata: String? = null
) {
    fun toJsonLine(): String {
        val failure = failureKind?.let { "\"failureKind\":\"${escape(it)}\"," } ?: ""
        val duration = durationMs?.let { "\"durationMs\":$it," } ?: ""
        val extra = metadata?.let { "\"metadata\":\"${escape(it)}\"," } ?: ""
        return "{\"event\":\"${escape(event)}\",\"result\":\"${escape(result)}\",$failure$duration$extra" +
            "\"itemCount\":$itemCount,\"coverage\":\"${escape(coverage)}\"}"
    }

    private fun escape(value: String): String = value
        .replace("\\", "\\\\")
        .replace("\"", "\\\"")
        .replace("\n", "\\n")
        .replace("\r", "\\r")
}
