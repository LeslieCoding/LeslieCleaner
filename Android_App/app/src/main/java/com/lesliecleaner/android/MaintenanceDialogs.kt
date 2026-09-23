package com.lesliecleaner.android

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

@Composable
fun JunkDialog(data: JunkAnalysis?, onOpenSystem: () -> Unit, onClose: () -> Unit) {
    AlertDialog(onDismissRequest = onClose, title = { Text("垃圾与缓存管理") }, text = {
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            if (data == null) Text("请先点击首页的开始安全扫描，生成可核验的清理预览。") else {
                Text("可确认清理：${formatBytes(data.totalReviewableBytes)}", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.primary)
                data.candidates.forEach { candidate ->
                    Text("${candidate.title}：${if (candidate.bytes > 0) formatBytes(candidate.bytes) else "需系统审核"}")
                    Text(candidate.detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Text(data.note, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }, confirmButton = { TextButton(onClick = onOpenSystem) { Text("打开系统存储") } }, dismissButton = { TextButton(onClick = onClose) { Text("关闭") } })
}

@Composable
fun SecurityDialog(data: SecuritySnapshot?, onOpenSettings: () -> Unit, onClose: () -> Unit) {
    AlertDialog(onDismissRequest = onClose, title = { Text("安全与病毒检查") }, text = {
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("系统安全权威：Google Play Protect", style = MaterialTheme.typography.titleMedium)
            Text(if (data?.playProtectAvailable == true) "系统安全设置可打开" else "未发现可直接打开的系统安全页面")
            Text("当前规则：${data?.ruleState?.label ?: "等待扫描"} ${data?.ruleState?.version ?: ""}", color = MaterialTheme.colorScheme.primary)
            if (data == null) Text("请先扫描应用清单。") else if (data.signals.isEmpty()) Text("本地可观察信号：暂未发现需要提示的项目。") else {
                Text("本地可观察信号：${data.signals.size} 项")
                LazyColumn { items(data.signals.take(8)) { signal -> Text("${signal.title}：${signal.detail}", style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(vertical = 3.dp)) } }
            }
            Text("LeslieCleaner 不替代系统杀毒，也不会宣称已清除病毒；最终扫描交给 Play Protect。", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }, confirmButton = { TextButton(onClick = onOpenSettings) { Text("打开系统安全") } }, dismissButton = { TextButton(onClick = onClose) { Text("关闭") } })
}

@Composable
fun StartupDialog(data: StartupSnapshot?, onOpenSettings: () -> Unit, onClose: () -> Unit) {
    AlertDialog(onDismissRequest = onClose, title = { Text("自启动与后台启动") }, text = {
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("系统可见开机接收器：${data?.visibleCount ?: "等待扫描"}", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.primary)
            data?.signals?.take(8)?.forEach { Text("${it.label}  ${it.packageName}", style = MaterialTheme.typography.bodySmall) }
            Text(data?.note ?: "请先扫描应用清单。", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text("不同品牌的自启动权限由系统/OEM控制，本应用只提供可见性和设置入口。", style = MaterialTheme.typography.bodySmall)
        }
    }, confirmButton = { TextButton(onClick = onOpenSettings) { Text("打开系统设置") } }, dismissButton = { TextButton(onClick = onClose) { Text("关闭") } })
}

@Composable
fun AccelerationDialog(health: DeviceHealthSnapshot?, onOpenStorage: () -> Unit, onClose: () -> Unit) {
    AlertDialog(onDismissRequest = onClose, title = { Text("系统加速") }, text = {
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text("加速依据设备事实，不强制结束第三方进程。", style = MaterialTheme.typography.titleMedium)
            Text(health?.let { snapshot ->
                val storage = (snapshot.storage as? HealthMetric.Available<StorageSnapshot>)?.value?.let { "存储已使用 ${it.usedPercent}%" } ?: "存储未提供"
                val memory = (snapshot.memory as? HealthMetric.Available<MemorySnapshot>)?.value?.let { "内存可用 ${formatBytes(it.availableBytes)}" } ?: "内存未提供"
                "$storage · $memory"
            } ?: "请先点击开始安全扫描获取内存、电量和存储状态。")
            Text("建议：清理可确认缓存、关闭不需要的后台权限，并使用系统页面完成深度优化。", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }, confirmButton = { TextButton(onClick = onOpenStorage) { Text("打开系统存储") } }, dismissButton = { TextButton(onClick = onClose) { Text("关闭") } })
}
