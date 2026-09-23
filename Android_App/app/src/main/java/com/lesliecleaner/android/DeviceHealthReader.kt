package com.lesliecleaner.android

import android.app.ActivityManager
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.BatteryManager
import android.os.Environment
import android.os.StatFs
import java.lang.System.currentTimeMillis

class DeviceHealthReader(private val context: Context) {
    fun read(): DeviceHealthSnapshot {
        val warnings = mutableListOf<String>()
        val storage = readStorage(warnings)
        val memory = readMemory(warnings)
        val battery = readBattery(warnings)
        return DeviceHealthSnapshot(storage, memory, battery, currentTimeMillis(), warnings.toList())
    }

    private fun readStorage(warnings: MutableList<String>): HealthMetric<StorageSnapshot> = try {
        val stats = StatFs(Environment.getDataDirectory().path)
        val total = stats.totalBytes
        val available = stats.availableBytes
        if (total <= 0L || available < 0L || available > total) {
            warnings += "存储容量数据不在可验证范围"
            HealthMetric.Unavailable("invalid_storage")
        } else {
            HealthMetric.Available(StorageSnapshot(total, available))
        }
    } catch (error: RuntimeException) {
        warnings += "存储信息读取失败"
        HealthMetric.Unavailable(error.javaClass.simpleName)
    }

    private fun readMemory(warnings: MutableList<String>): HealthMetric<MemorySnapshot> = try {
        val manager = context.getSystemService(ActivityManager::class.java)
        if (manager == null) {
            warnings += "内存服务不可用"
            HealthMetric.Unavailable("activity_manager_unavailable")
        } else {
            val info = ActivityManager.MemoryInfo()
            manager.getMemoryInfo(info)
            if (info.totalMem <= 0L || info.availMem < 0L || info.availMem > info.totalMem) {
                warnings += "内存数据不在可验证范围"
                HealthMetric.Unavailable("invalid_memory")
            } else {
                HealthMetric.Available(MemorySnapshot(info.totalMem, info.availMem, info.lowMemory))
            }
        }
    } catch (error: RuntimeException) {
        warnings += "内存信息读取失败"
        HealthMetric.Unavailable(error.javaClass.simpleName)
    }

    @Suppress("DEPRECATION")
    private fun readBattery(warnings: MutableList<String>): HealthMetric<BatterySnapshot> = try {
        val intent = context.registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
        if (intent == null) {
            warnings += "电池服务未返回状态"
            HealthMetric.Unavailable("battery_intent_unavailable")
        } else {
            val level = intent.getIntExtra(BatteryManager.EXTRA_LEVEL, -1)
            val scale = intent.getIntExtra(BatteryManager.EXTRA_SCALE, -1)
            val status = intent.getIntExtra(BatteryManager.EXTRA_STATUS, -1)
            val percent = if (level >= 0 && scale > 0) (level * 100 / scale).coerceIn(0, 100) else null
            val charging = when (status) {
                BatteryManager.BATTERY_STATUS_CHARGING,
                BatteryManager.BATTERY_STATUS_FULL -> true
                BatteryManager.BATTERY_STATUS_DISCHARGING,
                BatteryManager.BATTERY_STATUS_NOT_CHARGING -> false
                else -> null
            }
            if (percent == null && charging == null) {
                warnings += "电池状态字段不可用"
                HealthMetric.Unavailable("invalid_battery")
            } else {
                HealthMetric.Available(BatterySnapshot(percent, charging))
            }
        }
    } catch (error: RuntimeException) {
        warnings += "电池信息读取失败"
        HealthMetric.Unavailable(error.javaClass.simpleName)
    }
}
