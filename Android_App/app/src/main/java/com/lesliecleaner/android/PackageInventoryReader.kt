package com.lesliecleaner.android

import android.content.Context
import android.content.pm.PackageManager
import android.os.Build

data class InstalledAppInfo(
    val identity: TargetIdentity.PackageName,
    val label: String,
    val versionName: String?,
    val targetSdkVersion: Int?,
    val lastUpdateTime: Long?,
    val isSystemApp: Boolean,
    val isEnabled: Boolean,
    val isDebuggable: Boolean
)

enum class InventoryAttention { Debuggable, Disabled, None }

fun attentionFor(app: InstalledAppInfo): InventoryAttention = when {
    app.isDebuggable -> InventoryAttention.Debuggable
    !app.isEnabled -> InventoryAttention.Disabled
    else -> InventoryAttention.None
}

data class InventorySummary(
    val total: Int,
    val userApps: Int,
    val systemApps: Int,
    val enabledApps: Int,
    val disabledApps: Int,
    val debuggableApps: Int
)

sealed interface InventoryResult {
    data class Verified(val items: List<InstalledAppInfo>, val coverage: Coverage, val summary: InventorySummary) : InventoryResult
    data class Failed(val kind: FailureKind, val detail: String) : InventoryResult
}

enum class FailureKind { PermissionDenied, ProviderUnavailable, Unexpected }
enum class Coverage { ProviderVisibleOnly }

class PackageInventoryReader(private val context: Context) {
    fun read(): InventoryResult = try {
        val manager = context.packageManager
        val applications = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            manager.getInstalledApplications(PackageManager.ApplicationInfoFlags.of(0))
        } else {
            @Suppress("DEPRECATION")
            manager.getInstalledApplications(0)
        }
        val items = applications
            .asSequence()
            .mapNotNull { info ->
                try {
                    val packageName = info.packageName
                    val versionName = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                        manager.getPackageInfo(packageName, PackageManager.PackageInfoFlags.of(0)).versionName
                    } else {
                        @Suppress("DEPRECATION")
                        manager.getPackageInfo(packageName, 0).versionName
                    }
                    InstalledAppInfo(
                        identity = TargetIdentity.PackageName(packageName),
                        label = manager.getApplicationLabel(info).toString(),
                        versionName = versionName,
                        targetSdkVersion = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                            manager.getPackageInfo(packageName, PackageManager.PackageInfoFlags.of(0)).applicationInfo?.targetSdkVersion
                        } else {
                            @Suppress("DEPRECATION")
                            manager.getPackageInfo(packageName, 0).applicationInfo?.targetSdkVersion
                        },
                        lastUpdateTime = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                            manager.getPackageInfo(packageName, PackageManager.PackageInfoFlags.of(0)).lastUpdateTime
                        } else {
                            @Suppress("DEPRECATION")
                            manager.getPackageInfo(packageName, 0).lastUpdateTime
                        },
                        isSystemApp = (info.flags and android.content.pm.ApplicationInfo.FLAG_SYSTEM) != 0,
                        isEnabled = info.enabled,
                        isDebuggable = (info.flags and android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE) != 0
                    )
                } catch (_: PackageManager.NameNotFoundException) {
                    null
                } catch (_: IllegalArgumentException) {
                    null
                } catch (_: SecurityException) {
                    null
                }
            }
            .distinctBy { it.identity.stableKey }
            .sortedBy { it.identity.value }
            .toList()
        val summary = InventorySummary(
            total = items.size,
            userApps = items.count { !it.isSystemApp },
            systemApps = items.count { it.isSystemApp },
            enabledApps = items.count { it.isEnabled },
            disabledApps = items.count { !it.isEnabled },
            debuggableApps = items.count { it.isDebuggable }
        )
        InventoryResult.Verified(items, Coverage.ProviderVisibleOnly, summary)
    } catch (e: SecurityException) {
        InventoryResult.Failed(FailureKind.PermissionDenied, e.javaClass.simpleName)
    } catch (e: PackageManager.NameNotFoundException) {
        InventoryResult.Failed(FailureKind.ProviderUnavailable, e.javaClass.simpleName)
    } catch (e: RuntimeException) {
        InventoryResult.Failed(FailureKind.Unexpected, e.javaClass.simpleName)
    }
}
