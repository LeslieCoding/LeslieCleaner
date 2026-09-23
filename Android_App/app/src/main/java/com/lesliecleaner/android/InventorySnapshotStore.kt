package com.lesliecleaner.android

import android.content.Context
import java.io.File
import java.security.MessageDigest

data class InventoryDelta(val added: Int, val removed: Int, val changed: Int, val hasPrevious: Boolean)

class InventorySnapshotStore(context: Context) {
    private val file = File(context.filesDir, "inventory-snapshot.dat")
    private val formatHeader = "leslie-snapshot-v2"

    @Synchronized
    fun compareAndStore(items: List<InstalledAppInfo>): InventoryDelta {
        val current = items.associate { fingerprint(it) to observedVersion(it) }
        val previous = if (file.exists()) file.readLines(Charsets.UTF_8).dropWhile { it == formatHeader }.mapNotNull { line ->
            val parts = line.split("|", limit = 2)
            if (parts.size == 2) parts[0] to parts[1] else null
        }.toMap() else emptyMap()
        val added = current.keys.count { it !in previous }
        val removed = previous.keys.count { it !in current }
        val changed = current.keys.intersect(previous.keys).count { current[it] != previous[it] }
        val body = current.entries.sortedBy { it.key }.joinToString("\n") { "${it.key}|${it.value}" }
        file.writeText(if (body.isEmpty()) formatHeader else "$formatHeader\n$body", Charsets.UTF_8)
        return InventoryDelta(added, removed, changed, previous.isNotEmpty())
    }

    private fun fingerprint(item: InstalledAppInfo): String {
        val raw = item.identity.stableKey.toByteArray(Charsets.UTF_8)
        return MessageDigest.getInstance("SHA-256").digest(raw).joinToString("") { "%02x".format(it) }
    }

    private fun observedVersion(item: InstalledAppInfo): String = listOf(
        item.versionName.orEmpty(),
        item.targetSdkVersion?.toString().orEmpty(),
        item.lastUpdateTime?.toString().orEmpty(),
        item.isEnabled.toString(),
        item.isDebuggable.toString()
    ).joinToString("|")
}
