package com.lesliecleaner.android

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import java.io.File
import java.security.MessageDigest

data class JunkCandidate(
    val title: String,
    val detail: String,
    val bytes: Long,
    val action: JunkAction,
    val confidence: JunkConfidence
)

enum class JunkAction { APP_CACHE, SYSTEM_STORAGE_SETTINGS, MANUAL_REVIEW }
enum class JunkConfidence { HIGH, MEDIUM, UNAVAILABLE }

data class JunkAnalysis(
    val candidates: List<JunkCandidate>,
    val scannedBytes: Long,
    val note: String
) {
    val totalReviewableBytes: Long get() = candidates.filter { it.action == JunkAction.APP_CACHE }.sumOf { it.bytes }
}

class JunkCandidateReader(private val context: Context) {
    fun read(): JunkAnalysis {
        val cacheBytes = boundedSize(context.cacheDir, 20_000)
        val candidates = buildList {
            if (cacheBytes > 0L) add(JunkCandidate("Leslie Cleaner 缓存", "仅统计本应用缓存，清理前需要确认", cacheBytes, JunkAction.APP_CACHE, JunkConfidence.HIGH))
            add(JunkCandidate("系统存储深度清理", "由系统或手机管家审核大文件、残留和缓存", 0L, JunkAction.SYSTEM_STORAGE_SETTINGS, JunkConfidence.UNAVAILABLE))
        }
        return JunkAnalysis(candidates, cacheBytes, "Android 不允许普通应用无提示地清空其他应用数据；请通过系统页面确认")
    }

    companion object {
        fun boundedSize(root: File, maxEntries: Int): Long {
            if (!root.exists()) return 0L
            var entries = 0
            var total = 0L
            root.walkTopDown().forEach { file ->
                if (entries++ >= maxEntries) return@forEach
                total += if (file.isFile) file.length() else 0L
            }
            return total
        }
    }
}

data class SecuritySignal(val title: String, val detail: String, val severity: String)
data class SecuritySnapshot(
    val signals: List<SecuritySignal>,
    val playProtectAvailable: Boolean,
    val ruleState: RuleUpdateState
)

class SecuritySignalReader(private val context: Context) {
    fun read(inventory: List<InstalledAppInfo>): SecuritySnapshot {
        val signals = inventory.flatMap { app ->
            buildList {
                if (app.isDebuggable) add(SecuritySignal(app.label, "调试标记可见，建议确认是否为开发版本", "提示"))
                if (!app.isEnabled) add(SecuritySignal(app.label, "应用当前处于停用状态", "信息"))
                if (app.targetSdkVersion?.let { it in 1..25 } == true) add(SecuritySignal(app.label, "目标 Android 版本较旧，建议检查更新", "提示"))
            }
        }.take(20)
        return SecuritySnapshot(signals, canResolve(Intent(Settings.ACTION_SECURITY_SETTINGS)), RuleUpdateStore.current())
    }

    private fun canResolve(intent: Intent): Boolean = intent.resolveActivity(context.packageManager) != null
}

data class StartupSignal(val packageName: String, val label: String, val bootReceiverVisible: Boolean)
data class StartupSnapshot(val signals: List<StartupSignal>, val visibleCount: Int, val note: String)

class StartupSignalReader(private val context: Context) {
    fun read(inventory: List<InstalledAppInfo>): StartupSnapshot {
        val signals = inventory.mapNotNull { app ->
            try {
                val intent = Intent(Intent.ACTION_BOOT_COMPLETED).setPackage(app.identity.value)
                val count = if (Build.VERSION.SDK_INT >= 33) context.packageManager.queryBroadcastReceivers(intent, 0).size else @Suppress("DEPRECATION") context.packageManager.queryBroadcastReceivers(intent, 0).size
                StartupSignal(app.identity.value, app.label, count > 0)
            } catch (_: Exception) { null }
        }.filter { it.bootReceiverVisible }
        return StartupSnapshot(signals.take(30), signals.size, "这里只展示系统可见的开机广播接收器；自启动开关由一加/系统设置决定")
    }
}

data class RuleUpdateState(val version: String, val label: String, val cloudConfigured: Boolean)

object RuleUpdateStore {
    private const val BUNDLED_VERSION = "3.0.0"
    fun current() = RuleUpdateState(BUNDLED_VERSION, "本地离线安全规则", cloudConfigured = false)

    fun sha256(payload: String): String = MessageDigest.getInstance("SHA-256").digest(payload.toByteArray()).joinToString("") { "%02x".format(it) }
    fun verify(payload: String, expectedSha256: String): Boolean = sha256(payload).equals(expectedSha256.trim(), ignoreCase = true)
}

fun systemStorageIntent(): Intent = Intent(Settings.ACTION_INTERNAL_STORAGE_SETTINGS)
fun securitySettingsIntent(): Intent = Intent(Settings.ACTION_SECURITY_SETTINGS)
fun appDetailsIntent(packageName: String): Intent = Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:$packageName"))
