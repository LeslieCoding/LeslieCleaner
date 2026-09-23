# USB 调试连接自检

`scripts/Test-UsbDebug.ps1` 用于新板接入后的只读诊断。它会识别：

- Android ADB：`device`、`unauthorized`、`offline`；
- Fastboot 设备；
- Windows 串口；
- Windows 当前 USB/PnP 设备；
- ADB/Fastboot 工具或驱动缺失。

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-UsbDebug.ps1
```

机器上已有 Android SDK、但未加入 `PATH` 时：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-UsbDebug.ps1 -AndroidSdkPath D:\Android\Sdk
```

脚本也会自动寻找项目内的便携工具：

- `tools\android-platform-tools\platform-tools\adb.exe`
- `tools\platform-tools\adb.exe`

Platform-Tools 应从 Android 官方发布页取得，并由使用者确认 Android SDK 许可；项目不会静默接受许可或从第三方镜像下载二进制文件。

自动化测试可以使用 `-AsJson`。脚本仅在状态为 `AdbReady` 时返回成功退出码；其他状态返回非零码，并在 `NextAction` 中说明下一步。

默认文本输出只展示可能用于调试的 ADB、Fastboot、Android 或串口设备；需要枚举全部 USB 外设时追加 `-IncludeAllUsb`。

## 安全边界

本脚本不会刷写固件、解锁 bootloader、安装 APK、修改板卡设置或接受 RSA 授权。Android 的“USB 调试”开关和首次 RSA 授权必须在板卡端完成；主机只能检测其状态。进入 Fastboot 时也只做设备枚举，不执行任何写命令。
