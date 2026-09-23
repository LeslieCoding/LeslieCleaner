package com.lesliecleaner.android

import android.content.ActivityNotFoundException
import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Share
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.core.content.FileProvider
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

data class ScanSummary(
    val checked: Int,
    val supported: Boolean,
    val breakdown: InventorySummary? = null,
    val health: DeviceHealthSnapshot? = null,
    val delta: InventoryDelta? = null
)

private val Blue = Color(0xFF3867F5)
private val Teal = Color(0xFF00A88F)
private val Ink = Color(0xFF172033)
private val Cloud = Color(0xFFF6F8FC)

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent { LeslieCleanerApp() }
    }
}

@Composable
private fun LeslieCleanerTheme(content: @Composable () -> Unit) {
    val colors = if (isSystemInDarkTheme()) {
        darkColorScheme(primary = Color(0xFF9DB2FF), secondary = Color(0xFF63DBC4))
    } else {
        lightColorScheme(primary = Blue, secondary = Teal, background = Cloud, surface = Color.White)
    }
    MaterialTheme(colorScheme = colors, content = content)
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun LeslieCleanerApp() {
    var summary by remember { mutableStateOf<ScanSummary?>(null) }
    var detail by remember { mutableStateOf("\u53ef\u4ee5\u5f00\u59cb\u5b89\u5168\u7684\u53ea\u8bfb\u68c0\u67e5") }
    var history by remember { mutableStateOf<List<String>>(emptyList()) }
    var showHistory by remember { mutableStateOf(false) }
    var showExport by remember { mutableStateOf(false) }
    var showScanResult by remember { mutableStateOf(false) }
    var showPrivacy by remember { mutableStateOf(false) }
    var showClearHistory by remember { mutableStateOf(false) }
    var showJunk by remember { mutableStateOf(false) }
    var showSecurity by remember { mutableStateOf(false) }
    var showStartup by remember { mutableStateOf(false) }
    var showAcceleration by remember { mutableStateOf(false) }
    var inventory by remember { mutableStateOf<List<InstalledAppInfo>>(emptyList()) }
    var junk by remember { mutableStateOf<JunkAnalysis?>(null) }
    var security by remember { mutableStateOf<SecuritySnapshot?>(null) }
    var startup by remember { mutableStateOf<StartupSnapshot?>(null) }
    var message by remember { mutableStateOf<String?>(null) }
    var selectedTab by remember { mutableStateOf(0) }
    var scanning by remember { mutableStateOf(false) }
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val logger = remember(context) { JsonlAuditLogger(context) }
    val snapshotStore = remember(context) { InventorySnapshotStore(context) }
    val coordinator = remember(context) { AnalysisCoordinator(context, snapshotStore) }
    LaunchedEffect(logger) { history = withContext(Dispatchers.IO) { logger.readRecent(3) } }

    LeslieCleanerTheme {
        Scaffold(
            containerColor = MaterialTheme.colorScheme.background,
            topBar = {
                TopAppBar(title = {
                    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        Box(Modifier.size(34.dp).clip(RoundedCornerShape(11.dp)).background(Blue), contentAlignment = Alignment.Center) {
                            Icon(Icons.Default.CheckCircle, null, tint = Color.White, modifier = Modifier.size(20.dp))
                        }
                        Column {
                            Text("LeslieCleaner", fontWeight = FontWeight.Bold)
                            Text("Android \u7248", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                }, actions = { Icon(Icons.Default.CheckCircle, "\u53ea\u8bfb\u4fdd\u62a4\u6a21\u5f0f", tint = Teal, modifier = Modifier.padding(end = 18.dp)) })
            },
            bottomBar = {
                NavigationBar {
                    NavigationBarItem(selectedTab == 0, { selectedTab = 0 }, { Icon(Icons.Default.Home, "\u9996\u9875") }, label = { Text("\u9996\u9875") })
                    NavigationBarItem(selectedTab == 1, { selectedTab = 1; showHistory = true }, { Icon(Icons.Default.Share, "\u5386\u53f2") }, label = { Text("\u5386\u53f2") })
                    NavigationBarItem(selectedTab == 2, { selectedTab = 2 }, { Icon(Icons.Default.Settings, "\u8bbe\u7f6e") }, label = { Text("\u8bbe\u7f6e") })
                }
            }
        ) { padding ->
            if (selectedTab == 2) {
                Column(Modifier.fillMaxSize().padding(padding).padding(20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text("\u504f\u597d\u8bbe\u7f6e", style = MaterialTheme.typography.headlineMedium, fontWeight = FontWeight.Bold)
                    Text("\u4fdd\u5b58\u4e0e\u6570\u636e\u8fb9\u754c", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                    SettingsRow("\u53ea\u8bfb\u4fdd\u62a4", "\u5df2\u542f\u7528 - \u4e0d\u4f1a\u9759\u9ed8\u5220\u9664\u5185\u5bb9", Teal)
                    SettingsRow("\u5ba1\u8ba1\u5386\u53f2", "\u4ec5\u4fdd\u5b58\u5e94\u7528\u5185\u90e8\u7684\u8131\u654f\u4e8b\u4ef6\uff0c\u6700\u591a 200 \u6761", Blue)
                    OutlinedButton(onClick = { showClearHistory = true }, Modifier.fillMaxWidth()) { Text("\u6e05\u9664\u672c\u5730\u5ba1\u8ba1\u5386\u53f2") }
                    Text("\u6240\u6709\u626b\u63cf\u90fd\u9650\u5b9a\u4e3a provider \u53ef\u89c1\u8303\u56f4\uff0c\u4e0d\u4f1a\u58f0\u79f0\u5b8c\u6574\u8bbe\u5907\u8986\u76d6\u3002", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            } else {
                LazyColumn(Modifier.fillMaxSize().padding(padding), contentPadding = PaddingValues(20.dp, 12.dp, 20.dp, 28.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
                    item { DashboardHeader() }
                    item {
                        ScanHero(summary, detail, scanning, onScan = {
                            if (!scanning) scope.launch {
                                scanning = true
                                try {
                                    withContext(Dispatchers.IO) { logger.append(ScanAuditRecord("package_scan", "started", 0, "pending")) }
                                    when (val result = coordinator.run()) {
                                        is AnalysisRunResult.Completed -> {
                                            val analysis = result.value
                                            inventory = analysis.inventory
                                            junk = analysis.junk
                                            security = analysis.security
                                            startup = analysis.startup
                                            summary = ScanSummary(analysis.inventory.size, true, analysis.summary, analysis.health, analysis.delta)
                                            detail = "\u5df2\u9a8c\u8bc1 provider \u53ef\u89c1\u7684\u5e94\u7528\u6e05\u5355"
                                            withContext(Dispatchers.IO) { logger.append(ScanAuditRecord("package_scan", "verified", analysis.inventory.size, analysis.coverage.name, durationMs = analysis.durationMs, metadata = "user=${analysis.summary.userApps};system=${analysis.summary.systemApps};enabled=${analysis.summary.enabledApps};disabled=${analysis.summary.disabledApps};debuggable=${analysis.summary.debuggableApps};added=${analysis.delta.added};removed=${analysis.delta.removed};changed=${analysis.delta.changed};previous=${analysis.delta.hasPrevious};healthWarnings=${analysis.health.warnings.size}")) }
                                            showScanResult = true
                                        }
                                        is AnalysisRunResult.Failed -> {
                                            inventory = emptyList()
                                            summary = ScanSummary(0, false)
                                            detail = "\u626b\u63cf\u4e0d\u53ef\u7528\uff1a${failureLabel(result.kind)}"
                                            withContext(Dispatchers.IO) { logger.append(ScanAuditRecord("package_scan", "failed", 0, "none", result.kind.name, result.durationMs)) }
                                            showScanResult = true
                                        }
                                        AnalysisRunResult.Busy -> {
                                            detail = "\u5f53\u524d\u5df2\u6709\u4e00\u6b21\u5206\u6790\u6b63\u5728\u8fdb\u884c"
                                            showScanResult = true
                                        }
                                    }
                                } catch (error: Throwable) {
                                    inventory = emptyList()
                                    summary = ScanSummary(0, false)
                                    detail = "\u626b\u63cf\u51fa\u9519\uff1a${error.javaClass.simpleName}"
                                    withContext(Dispatchers.IO) { logger.append(ScanAuditRecord("package_scan", "failed", 0, "none", "Unexpected")) }
                                    showScanResult = true
                                } finally {
                                    history = withContext(Dispatchers.IO) { logger.readRecent(3) }
                                    scanning = false
                                }
                            }
                        }, onReset = { summary = null; detail = "\u53ef\u4ee5\u5f00\u59cb\u5b89\u5168\u7684\u53ea\u8bfb\u68c0\u67e5" })
                    }
                    item { SectionTitle("\u529f\u80fd\u5de5\u5177\u7bb1", "\u57fa\u4e8e\u516c\u5f00 Android \u80fd\u529b\u6784\u5efa") }
                    item { FeatureGrid(
                        onHistory = { scope.launch { history = withContext(Dispatchers.IO) { logger.readRecent(50) }; showHistory = true } },
                        onInventory = { detail = if (inventory.isEmpty()) "\u8bf7\u5148\u70b9\u51fb\u5f00\u59cb\u5b89\u5168\u626b\u63cf" else detail; showScanResult = true },
                        onPrivacy = { showPrivacy = true },
                        onSettings = { selectedTab = 2 },
                        onJunk = { showJunk = true },
                        onSecurity = { showSecurity = true },
                        onStartup = { showStartup = true },
                        onAcceleration = { showAcceleration = true }
                    ) }
                    item { AuditPreview(history, onOpen = { scope.launch { history = withContext(Dispatchers.IO) { logger.readRecent(50) }; showHistory = true } }, onExport = { showExport = true }) }
                    item { Text("LeslieCleaner \u4e0d\u4f7f\u7528 root\u3001\u9690\u85cf API \u6216\u9759\u9ed8\u5220\u9664\u3002\u672a\u6765\u7684\u6bcf\u9879\u64cd\u4f5c\u90fd\u4f1a\u5c55\u793a provider\u3001\u6743\u9650\u3001\u786e\u8ba4\u548c\u9a8c\u8bc1\u72b6\u6001\u3002", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                }
            }
        }
    }
    if (showHistory) HistoryDialog(history, { showHistory = false; selectedTab = 0 }, { showExport = true })
    if (showExport) ExportDialog(context, logger, history, { showExport = false }, { message = it })
    if (showScanResult) ScanResultDialog(inventory, detail, summary?.breakdown, summary?.health, summary?.delta) { showScanResult = false }
    if (showPrivacy) PrivacyDialog { showPrivacy = false }
    if (showJunk) JunkDialog(junk, { openExternal(context, systemStorageIntent()) { message = "系统存储页面暂不可用" } }, { showJunk = false })
    if (showSecurity) SecurityDialog(security, { openExternal(context, securitySettingsIntent()) { message = "系统安全页面暂不可用" } }, { showSecurity = false })
    if (showStartup) StartupDialog(startup, { openExternal(context, Intent(android.provider.Settings.ACTION_SETTINGS)) { message = "系统设置页面暂不可用" } }, { showStartup = false })
    if (showAcceleration) AccelerationDialog(summary?.health, { openExternal(context, systemStorageIntent()) { message = "系统存储页面暂不可用" } }, { showAcceleration = false })
    if (showClearHistory) AlertDialog(
        onDismissRequest = { showClearHistory = false },
        title = { Text("\u6e05\u9664\u672c\u5730\u5386\u53f2\uff1f") },
        text = { Text("\u53ea\u4f1a\u5220\u9664 LeslieCleaner \u5185\u90e8\u7684\u8131\u654f\u5ba1\u8ba1\u8bb0\u5f55\uff0c\u4e0d\u4f1a\u5f71\u54cd\u624b\u673a\u4e0a\u7684\u5176\u4ed6\u5e94\u7528\u6216\u6587\u4ef6\u3002") },
        confirmButton = { TextButton(onClick = { scope.launch { withContext(Dispatchers.IO) { logger.clear() }; history = emptyList(); showClearHistory = false; message = "\u672c\u5730\u5ba1\u8ba1\u5386\u53f2\u5df2\u6e05\u9664" } }) { Text("\u786e\u8ba4\u6e05\u9664") } },
        dismissButton = { TextButton(onClick = { showClearHistory = false }) { Text("\u53d6\u6d88") } }
    )
    message?.let { Text(it, Modifier.padding(20.dp), style = MaterialTheme.typography.bodySmall) }
}

@Composable
private fun DashboardHeader() {
    Column(verticalArrangement = Arrangement.spacedBy(5.dp)) {
        Text("\u6b22\u8fce\u56de\u6765", style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
        Text("\u8ba9\u8bbe\u5907\u4fdd\u6301\u6e05\u6670\uff0c\n\u8ba9\u72b6\u6001\u4e00\u76ee\u4e86\u7136\u3002", style = MaterialTheme.typography.headlineLarge, fontWeight = FontWeight.Bold, color = Ink)
        Text("\u4ee5\u900f\u660e\u3001\u514b\u5236\u7684\u65b9\u5f0f\u7ef4\u62a4 Android \u8bbe\u5907\u3002", color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun ScanHero(summary: ScanSummary?, detail: String, scanning: Boolean, onScan: () -> Unit, onReset: () -> Unit) {
    Card(Modifier.fillMaxWidth(), RoundedCornerShape(26.dp), colors = CardDefaults.cardColors(containerColor = Color.Transparent)) {
        Column(Modifier.background(Brush.linearGradient(listOf(Blue, Color(0xFF6748D9)))).padding(22.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            Row(Modifier.fillMaxWidth(), Arrangement.SpaceBetween, Alignment.Top) {
                Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text("\u5b89\u5168\u68c0\u67e5", style = MaterialTheme.typography.labelMedium, color = Color.White.copy(.78f), fontWeight = FontWeight.Bold)
                    Text("\u67e5\u770b Android\n\u80fd\u591f\u9a8c\u8bc1\u7684\u5185\u5bb9\u3002", style = MaterialTheme.typography.headlineSmall, color = Color.White, fontWeight = FontWeight.Bold)
                }
                Icon(Icons.Default.CheckCircle, null, tint = Color.White, modifier = Modifier.size(44.dp))
            }
            Text(if (summary == null) detail else "$detail - ${summary.checked} \u9879", color = Color.White.copy(.86f))
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.CenterVertically) {
                Button(onClick = onScan, enabled = !scanning, shape = RoundedCornerShape(14.dp)) {
                    Icon(if (scanning) Icons.Default.Refresh else Icons.AutoMirrored.Filled.ArrowForward, null, Modifier.size(18.dp)); Spacer(Modifier.size(8.dp)); Text(if (scanning) "\u68c0\u67e5\u4e2d..." else "\u5f00\u59cb\u5b89\u5168\u626b\u63cf")
                }
                if (summary != null) TextButton(onClick = onReset) { Text("\u91cd\u7f6e", color = Color.White) }
            }
        }
    }
}

@Composable
private fun ScanResultDialog(
    items: List<InstalledAppInfo>,
    detail: String,
    breakdown: InventorySummary?,
    health: DeviceHealthSnapshot?,
    delta: InventoryDelta?,
    onClose: () -> Unit
) {
    var query by remember { mutableStateOf("") }
    var filter by remember { mutableStateOf("all") }
    var selectedApp by remember { mutableStateOf<InstalledAppInfo?>(null) }
    val filteredItems = items.filter { item ->
        val matchesFilter = when (filter) {
            "user" -> !item.isSystemApp
            "system" -> item.isSystemApp
            else -> true
        }
        val text = query.trim()
        matchesFilter && (text.isBlank() || item.label.contains(text, ignoreCase = true) || item.identity.value.contains(text, ignoreCase = true))
    }
    Dialog(onDismissRequest = onClose) {
        Surface(shape = RoundedCornerShape(28.dp), color = MaterialTheme.colorScheme.surface) {
            Column(Modifier.padding(22.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
                Row(Modifier.fillMaxWidth(), Arrangement.SpaceBetween, Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text(if (items.isEmpty()) "\u5e94\u7528\u6e05\u5355" else "\u626b\u63cf\u5b8c\u6210", style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold)
                        Text(if (items.isEmpty()) "\u5c1a\u672a\u751f\u6210\u626b\u63cf\u7ed3\u679c" else "\u53d1\u73b0 ${items.size} \u4e2a\u53ef\u8bfb\u53d6\u7684\u5e94\u7528", color = MaterialTheme.colorScheme.primary)
                    }
                    Icon(Icons.Default.CheckCircle, "\u626b\u63cf\u5b8c\u6210", tint = Teal, modifier = Modifier.size(30.dp))
                }
                Text(detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                breakdown?.let { stats ->
                    Text("\u7528\u6237\u5e94\u7528 ${stats.userApps}  \u7cfb\u7edf\u7ec4\u4ef6 ${stats.systemApps}  \u5df2\u542f\u7528 ${stats.enabledApps}  \u5df2\u7981\u7528 ${stats.disabledApps}  \u53ef\u8c03\u8bd5 ${stats.debuggableApps}", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
                }
                health?.let { snapshot ->
                    Text(healthDisplayLine(snapshot), style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
                    if (snapshot.warnings.isNotEmpty()) {
                        Text("\u90e8\u5206\u5065\u5eb7\u6307\u6807\u672a\u80fd\u9a8c\u8bc1\uff1a${snapshot.warnings.size} \u9879", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
                delta?.let { change ->
                    Text("\u91cd\u590d\u5206\u6790\uff1a\u65b0\u589e ${change.added}  \u79fb\u9664 ${change.removed}  \u53d8\u66f4 ${change.changed}${if (change.hasPrevious) "" else "  \u9996\u6b21\u5feb\u7167"}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                if (items.isNotEmpty()) {
                    OutlinedTextField(
                        value = query,
                        onValueChange = { query = it },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        label = { Text("\u641c\u7d22\u5e94\u7528\u6216\u5305\u540d") }
                    )
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                        TextButton(onClick = { filter = "all" }) { Text(if (filter == "all") "\u5168\u90e8\u2022" else "\u5168\u90e8") }
                        TextButton(onClick = { filter = "user" }) { Text(if (filter == "user") "\u7528\u6237\u5e94\u7528\u2022" else "\u7528\u6237\u5e94\u7528") }
                        TextButton(onClick = { filter = "system" }) { Text(if (filter == "system") "\u7cfb\u7edf\u7ec4\u4ef6\u2022" else "\u7cfb\u7edf\u7ec4\u4ef6") }
                    }
                    Text("\u663e\u793a ${filteredItems.size} / ${items.size} \u9879\uff0c\u70b9\u51fb\u6761\u76ee\u67e5\u770b\u8be6\u60c5", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                if (items.isEmpty()) {
                    Text("\u5f53\u524d\u6ca1\u6709\u53ef\u663e\u793a\u7684\u7ed3\u679c\u3002", color = MaterialTheme.colorScheme.onSurfaceVariant)
                } else {
                    if (filteredItems.isEmpty()) {
                        Text("\u6ca1\u6709\u5339\u914d\u7684\u5e94\u7528", color = MaterialTheme.colorScheme.onSurfaceVariant)
                    } else {
                        LazyColumn(Modifier.height(230.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            items(filteredItems.take(100), key = { it.identity.stableKey }) { item ->
                            Row(
                                Modifier.fillMaxWidth()
                                    .clickable { selectedApp = item }
                                    .semantics { contentDescription = "${item.label.ifBlank { item.identity.value }}，\u70b9\u51fb\u67e5\u770b\u8be6\u60c5" }
                                    .padding(vertical = 3.dp),
                                Arrangement.SpaceBetween
                            ) {
                                Text(item.label.ifBlank { item.identity.value }, modifier = Modifier.weight(1f), maxLines = 1)
                                Text(item.versionName ?: "-", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            }
                            if (filteredItems.size > 100) item { Text("\u4ec5\u5c55\u793a\u524d 100 \u9879\uff0c\u8bf7\u7ee7\u7eed\u641c\u7d22\u7f29\u5c0f\u8303\u56f4", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                        }
                    }
                }
                Button(onClick = onClose, Modifier.fillMaxWidth()) { Text("\u5173\u95ed\u7ed3\u679c") }
            }
        }
    }
    selectedApp?.let { AppDetailDialog(it) { selectedApp = null } }
}

@Composable
private fun AppDetailDialog(app: InstalledAppInfo, onClose: () -> Unit) {
    val attention = when (attentionFor(app)) {
        InventoryAttention.Debuggable -> "\u5173\u6ce8\u63d0\u793a\uff1a\u8fd9\u662f\u53ef\u8c03\u8bd5\u6784\u5efa\uff0c\u4ec5\u8868\u793a\u6784\u5efa\u6807\u8bb0\uff0c\u4e0d\u4ee3\u8868\u5b58\u5728\u6076\u610f\u884c\u4e3a\u3002"
        InventoryAttention.Disabled -> "\u5173\u6ce8\u63d0\u793a\uff1a\u5e94\u7528\u5f53\u524d\u5df2\u505c\u7528\uff0c\u4ec5\u8868\u793a\u8fd0\u884c\u72b6\u6001\uff0c\u4e0d\u4ee3\u8868\u5b58\u5728\u6076\u610f\u884c\u4e3a\u3002"
        InventoryAttention.None -> "\u5173\u6ce8\u63d0\u793a\uff1a\u5f53\u524d\u516c\u5f00\u4fe1\u606f\u672a\u89e6\u53d1\u672c\u5e94\u7528\u7684\u89c2\u5bdf\u89c4\u5219\u3002"
    }
    val updatedAt = app.lastUpdateTime
        ?.takeIf { it >= 946684800000L }
        ?.let { SimpleDateFormat("yyyy-MM-dd HH:mm", Locale.CHINA).format(Date(it)) }
        ?: "未提供"
    AlertDialog(
        onDismissRequest = onClose,
        title = { Text(app.label.ifBlank { app.identity.value }) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("\u5305\u540d\uff1a${app.identity.value}")
                Text("\u7248\u672c\uff1a${app.versionName ?: "-"}")
                Text("\u76ee\u6807 SDK\uff1a${app.targetSdkVersion ?: "-"}")
                Text("\u6700\u8fd1\u66f4\u65b0\uff1a$updatedAt")
                Text("\u7c7b\u578b\uff1a${if (app.isSystemApp) "\u7cfb\u7edf\u7ec4\u4ef6" else "\u7528\u6237\u5e94\u7528"}")
                Text("\u72b6\u6001\uff1a${if (app.isEnabled) "\u5df2\u542f\u7528" else "\u5df2\u7981\u7528"}")
                Text("\u8c03\u8bd5\uff1a${if (app.isDebuggable) "\u53ef\u8c03\u8bd5" else "\u975e\u8c03\u8bd5\u6784\u5efa"}")
                Text(attention, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.primary)
                Text("\u4ee5\u4e0a\u4e3a Android \u516c\u5f00 API \u53ef\u9a8c\u8bc1\u7684\u53ea\u8bfb\u4fe1\u606f\u3002", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        },
        confirmButton = { TextButton(onClick = onClose) { Text("\u5173\u95ed") } }
    )
}

@Composable
private fun SectionTitle(title: String, subtitle: String) {
    Column(verticalArrangement = Arrangement.spacedBy(3.dp)) { Text(title, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold); Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
}

@Composable
private fun FeatureGrid(onHistory: () -> Unit, onInventory: () -> Unit, onPrivacy: () -> Unit, onSettings: () -> Unit, onJunk: () -> Unit, onSecurity: () -> Unit, onStartup: () -> Unit, onAcceleration: () -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Row(Modifier.fillMaxWidth(), Arrangement.spacedBy(12.dp)) { FeatureCard(Modifier.weight(1f), Icons.Default.Refresh, "垃圾与缓存", "预览清理", Teal, onJunk); FeatureCard(Modifier.weight(1f), Icons.Default.CheckCircle, "安全与病毒", "系统协同", Blue, onSecurity) }
        Row(Modifier.fillMaxWidth(), Arrangement.spacedBy(12.dp)) { FeatureCard(Modifier.weight(1f), Icons.Default.Settings, "自启动管理", "查看启动信号", Color(0xFFE28A24), onStartup); FeatureCard(Modifier.weight(1f), Icons.Default.Refresh, "系统加速", "内存与存储", Color(0xFF8B5CF6), onAcceleration) }
        Row(Modifier.fillMaxWidth(), Arrangement.spacedBy(12.dp)) { FeatureCard(Modifier.weight(1f), Icons.Default.Settings, "应用清单", "搜索与详情", Teal, onInventory); FeatureCard(Modifier.weight(1f), Icons.Default.CheckCircle, "隐私信号", "查看状态", Blue, onPrivacy) }
        Row(Modifier.fillMaxWidth(), Arrangement.spacedBy(12.dp)) { FeatureCard(Modifier.weight(1f), Icons.Default.Share, "审计历史", "查看记录", Color(0xFFE28A24), onHistory); FeatureCard(Modifier.weight(1f), Icons.Default.Settings, "偏好设置", "已可用", Color(0xFF8B5CF6), onSettings) }
    }
}

private fun openExternal(context: android.content.Context, intent: Intent, onFailure: () -> Unit) {
    try { context.startActivity(intent) } catch (_: ActivityNotFoundException) { onFailure() }
}

@Composable
private fun SettingsRow(title: String, detail: String, color: Color) {
    Card(shape = RoundedCornerShape(18.dp)) {
        Row(Modifier.fillMaxWidth().padding(16.dp), Arrangement.SpaceBetween, Alignment.CenterVertically) {
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(title, fontWeight = FontWeight.SemiBold)
                Text(detail, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Icon(Icons.Default.CheckCircle, null, tint = color, modifier = Modifier.size(24.dp))
        }
    }
}

@Composable
private fun PrivacyDialog(onClose: () -> Unit) {
    AlertDialog(
        onDismissRequest = onClose,
        title = { Text("\u9690\u79c1\u4fe1\u53f7") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Text("\u5f53\u524d\u7248\u672c\u4ec5\u4f7f\u7528 Android \u516c\u5f00 API \u8bfb\u53d6 provider \u53ef\u89c1\u7684\u5e94\u7528\u6e05\u5355\u3002")
                Text("\u6743\u9650\uff1a\u672a\u7533\u8bf7\u5b58\u50a8\u3001\u8054\u7cfb\u4eba\u6216\u7f51\u7edc\u6743\u9650\u3002")
                Text("Root \u4e0e\u9690\u85cf API\uff1a\u4e0d\u4f7f\u7528\u3002")
                Text("\u672c\u5730\u8bb0\u5f55\uff1a\u4ec5\u4fdd\u5b58\u8131\u654f\u626b\u63cf\u4e8b\u4ef6\uff0c\u6700\u591a 200 \u6761\u3002")
            }
        },
        confirmButton = { TextButton(onClick = onClose) { Text("\u660e\u767d\u4e86") } }
    )
}

@Composable
private fun FeatureCard(modifier: Modifier, icon: ImageVector, title: String, status: String, color: Color, onClick: () -> Unit) {
    Card(onClick = onClick, modifier = modifier.semantics { contentDescription = "$title, $status" }, shape = RoundedCornerShape(20.dp)) { Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) { Icon(icon, null, tint = color, modifier = Modifier.size(24.dp)); Text(title, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold); Text(status, style = MaterialTheme.typography.labelMedium, color = color) } }
}

@Composable
private fun AuditPreview(history: List<String>, onOpen: () -> Unit, onExport: () -> Unit) {
    Card(shape = RoundedCornerShape(22.dp)) { Column(Modifier.padding(18.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Row(Modifier.fillMaxWidth(), Arrangement.SpaceBetween, Alignment.CenterVertically) { Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.CenterVertically) { Icon(Icons.Default.Share, null, tint = MaterialTheme.colorScheme.primary); Column { Text("\u5ba1\u8ba1\u8f68\u8ff9", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold); Text("\u5e94\u7528\u79c1\u6709 - \u5df2\u8131\u654f - \u5df2\u540c\u6b65", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant) } }; Text("${history.size}", style = MaterialTheme.typography.headlineSmall, color = MaterialTheme.colorScheme.primary, fontWeight = FontWeight.Bold) }
        Text(if (history.isEmpty()) "\u5b8c\u6210\u9a8c\u8bc1\u7684\u6d3b\u52a8\u4f1a\u663e\u793a\u5728\u8fd9\u91cc\u3002" else auditDisplayLine(history.last()), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) { OutlinedButton(onClick = onOpen, Modifier.weight(1f)) { Text("\u67e5\u770b\u5386\u53f2") }; OutlinedButton(onClick = onExport, Modifier.weight(1f)) { Icon(Icons.Default.Share, null, Modifier.size(17.dp)); Spacer(Modifier.size(6.dp)); Text("\u5bfc\u51fa") } }
    } }
}

private fun failureLabel(kind: FailureKind): String = when (kind) { FailureKind.PermissionDenied -> "\u6743\u9650\u88ab\u62d2\u7edd"; FailureKind.ProviderUnavailable -> "\u7cfb\u7edf provider \u4e0d\u53ef\u7528"; FailureKind.Unexpected -> "\u672a\u77e5\u9519\u8bef" }

private fun healthDisplayLine(snapshot: DeviceHealthSnapshot): String {
    val storage = (snapshot.storage as? HealthMetric.Available)?.value?.let {
        "\u5b58\u50a8\u5df2\u4f7f\u7528 ${it.usedPercent}%"
    } ?: "\u5b58\u50a8\u672a\u63d0\u4f9b"
    val memory = (snapshot.memory as? HealthMetric.Available)?.value?.let {
        "\u5185\u5b58\u53ef\u7528 ${formatBytes(it.availableBytes)}"
    } ?: "\u5185\u5b58\u672a\u63d0\u4f9b"
    val battery = (snapshot.battery as? HealthMetric.Available)?.value?.percent?.let {
        "\u7535\u91cf ${it}%"
    } ?: "\u7535\u91cf\u672a\u63d0\u4f9b"
    return "$storage  $memory  $battery"
}

private fun auditDisplayLine(raw: String): String = when { raw.contains("package_scan") && raw.contains("verified") -> "\u5e94\u7528\u6e05\u5355\u626b\u63cf - \u5df2\u9a8c\u8bc1 - \u53ea\u8bfb\u7ed3\u679c"; raw.contains("package_scan") -> "\u5e94\u7528\u6e05\u5355\u626b\u63cf - \u672a\u5b8c\u6210 - \u5df2\u8bb0\u5f55\u539f\u56e0"; raw.contains("audit_export") && raw.contains("share_sheet_started") -> "\u5ba1\u8ba1\u5bfc\u51fa - \u5206\u4eab\u9762\u677f\u5df2\u542f\u52a8"; raw.contains("audit_export") && raw.contains("failed") -> "\u5ba1\u8ba1\u5bfc\u51fa - \u672a\u5b8c\u6210"; raw.contains("audit_export") -> "\u5ba1\u8ba1\u5bfc\u51fa - \u5df2\u8bf7\u6c42"; else -> "\u7cfb\u7edf\u4e8b\u4ef6 - \u5df2\u8bb0\u5f55" }

@Composable
private fun HistoryDialog(history: List<String>, onClose: () -> Unit, onExport: () -> Unit) {
    Dialog(onDismissRequest = onClose) { Surface(shape = RoundedCornerShape(28.dp), tonalElevation = 8.dp) { Column(Modifier.padding(22.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Row(Modifier.fillMaxWidth(), Arrangement.SpaceBetween, Alignment.CenterVertically) { Column { Text("\u5ba1\u8ba1\u5386\u53f2", style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold); Text("\u6700\u8fd1 ${history.size} \u6761 - \u6700\u591a\u4fdd\u7559 200 \u6761", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant) }; TextButton(onClick = onClose) { Text("\u5173\u95ed") } }
        Text(if (history.isEmpty()) "\u6682\u65e0\u672c\u5730\u4e8b\u4ef6\u3002" else history.joinToString("\n\n") { auditDisplayLine(it) }, style = MaterialTheme.typography.bodySmall)
        Button(onClick = onExport, Modifier.fillMaxWidth()) { Icon(Icons.Default.Share, null); Spacer(Modifier.size(8.dp)); Text("\u5bfc\u51fa\u8131\u654f\u5386\u53f2") }
    } } }
}

@Composable
private fun ExportDialog(context: android.content.Context, logger: JsonlAuditLogger, history: List<String>, onClose: () -> Unit, onMessage: (String) -> Unit) {
    val scope = rememberCoroutineScope()
    AlertDialog(onDismissRequest = onClose, title = { Text("\u5bfc\u51fa\u5ba1\u8ba1\u5386\u53f2\uff1f") }, text = { Text("\u53ea\u4f1a\u5206\u4eab\u5df2\u8131\u654f\u7684\u626b\u63cf\u4e8b\u4ef6\uff0c\u4e0d\u5305\u542b\u539f\u59cb\u8def\u5f84\u3001 URI \u6216\u4e2a\u4eba\u5185\u5bb9\u3002") }, confirmButton = {
        TextButton(onClick = { onClose(); scope.launch { try {
            val file = withContext(Dispatchers.IO) { logger.append(ScanAuditRecord("audit_export", "requested", history.size, "redacted")); logger.writeRedactedExport(context.cacheDir) }
            val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
            val share = Intent(Intent.ACTION_SEND).apply { type = "text/plain"; putExtra(Intent.EXTRA_STREAM, uri); addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION) }
            context.startActivity(Intent.createChooser(share, "\u5206\u4eab\u5ba1\u8ba1\u5386\u53f2")); withContext(Dispatchers.IO) { logger.append(ScanAuditRecord("audit_export", "share_sheet_started", history.size, "redacted")) }; onMessage("\u5bfc\u51fa\u5df2\u51c6\u5907\u597d")
        } catch (_: ActivityNotFoundException) { withContext(Dispatchers.IO) { logger.append(ScanAuditRecord("audit_export", "failed", history.size, "redacted", "NoShareHandler")) }; onMessage("\u6ca1\u6709\u53ef\u7528\u7684\u5206\u4eab\u5e94\u7528") } catch (error: RuntimeException) { withContext(Dispatchers.IO) { logger.append(ScanAuditRecord("audit_export", "failed", history.size, "redacted", error.javaClass.simpleName)) }; onMessage("\u5bfc\u51fa\u4e0d\u53ef\u7528") } } }) { Text("\u5206\u4eab") }
    }, dismissButton = { TextButton(onClick = onClose) { Text("\u53d6\u6d88") } })
}
