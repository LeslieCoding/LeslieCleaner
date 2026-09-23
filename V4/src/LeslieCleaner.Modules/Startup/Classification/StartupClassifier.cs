namespace LeslieCleaner.Modules.Startup.Classification;

/// <summary>
/// 启动项来源分类器（纯函数，禁止仅按注册表路径分类）。
/// 综合 Publisher/Company、Signer 主题、数字签名有效性、路径位置、
/// 已知厂商名单多因素判定，产出分类 + 判定依据 + 置信度。
/// </summary>
public static class StartupClassifier
{
    /// <summary>硬件驱动/OEM 厂商名单（小写匹配前缀）。</summary>
    public static IReadOnlyList<string> DriverVendors { get; } =
    [
        "intel", "amd", "nvidia", "realtek", "synaptics", "logitech",
        "dell", "hp", "hp inc", "lenovo", "asus", "msi", "micro-star",
        "corsair", "razer", "steelseries", "hyperx", "roccat", "gigabyte",
        "wacom", "huion", "xp-pen", "displaylink", "via", "conexant",
    ];

    /// <summary>
    /// 执行分类。
    /// </summary>
    /// <param name="name">启动项名称。</param>
    /// <param name="publisher">发布商（注册表 Publisher 或版本信息 CompanyName）。</param>
    /// <param name="signer">签名者主题；无签名/未验证为 <see langword="null"/>。</param>
    /// <param name="signatureValid">签名是否有效。</param>
    /// <param name="filePath">可执行文件路径；无文件（计划任务等）为 <see langword="null"/>。</param>
    public static StartupClassification Classify(
        string name,
        string? publisher,
        string? signer,
        bool signatureValid,
        string? filePath)
    {
        ArgumentNullException.ThrowIfNull(name);
        var reasons = new List<string>(4);
        string vendorText = $"{publisher} {signer}".ToLowerInvariant();

        // 规则 1：Microsoft Windows —— Company=Microsoft Corporation 或 Signer=Microsoft*
        // 或路径在 Windows/WindowsApps/Microsoft Shared 且签名有效
        if (IsMicrosoft(publisher, signer))
        {
            reasons.Add($"厂商为 Microsoft（{publisher ?? signer}）");
            return new StartupClassification(StartupSourceKind.MicrosoftWindows, reasons, 0.98);
        }

        if (signatureValid && IsMicrosoftSigner(signer))
        {
            reasons.Add($"数字签名者为 {signer} 且签名有效");
            return new StartupClassification(StartupSourceKind.MicrosoftWindows, reasons, 0.98);
        }

        if (signatureValid && IsMicrosoftPath(filePath))
        {
            reasons.Add("位于 Windows/WindowsApps/Microsoft Shared 目录且签名有效");
            return new StartupClassification(StartupSourceKind.MicrosoftWindows, reasons, 0.9);
        }

        // 规则 2：Hardware Drivers —— 已知驱动/OEM 厂商名单（按 Publisher/Signer 匹配）
        foreach (string vendor in DriverVendors)
        {
            if (vendorText.Contains(vendor, StringComparison.Ordinal))
            {
                reasons.Add($"命中驱动/OEM 厂商名单：{vendor}");
                return new StartupClassification(StartupSourceKind.HardwareDriver, reasons, 0.9);
            }
        }

        // 规则 3：Unknown —— Publisher 空 + 签名无效 + 无 Signer（或路径异常）
        if (string.IsNullOrWhiteSpace(publisher) && !signatureValid && string.IsNullOrWhiteSpace(signer))
        {
            reasons.Add("发布商为空、无有效数字签名、无签名者信息");
            return new StartupClassification(StartupSourceKind.StartupUnknown, reasons, 0.85);
        }

        // 规则 4：User Applications —— 其余全部（含 Program Files/AppData 第三方路径）
        if (filePath is not null && IsUserPath(filePath))
        {
            reasons.Add("位于用户软件目录（Program Files/AppData 等）且非微软/驱动厂商");
        }
        else
        {
            reasons.Add("非微软、非驱动厂商的第三方来源");
        }

        return new StartupClassification(StartupSourceKind.UserApplication, reasons, 0.75);
    }

    /// <summary>判断是否微软厂商。</summary>
    public static bool IsMicrosoft(string? publisher, string? signer)
    {
        if (publisher is not null &&
            publisher.Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsMicrosoftSigner(signer);
    }

    /// <summary>判断签名者是否微软（兼容 X.509 DN 形式 "CN=Microsoft Windows, O=..."）。</summary>
    public static bool IsMicrosoftSigner(string? signer) =>
        !string.IsNullOrWhiteSpace(signer) &&
        (signer.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
         signer.StartsWith("CN=Microsoft", StringComparison.OrdinalIgnoreCase));

    /// <summary>判断是否微软系统路径（Windows/WindowsApps/Common Files\Microsoft Shared）。</summary>
    /// <param name="filePath">文件路径。</param>
    public static bool IsMicrosoftPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string? programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
        return
            (!string.IsNullOrEmpty(windows) && filePath.StartsWith(windows, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(programFiles) &&
                (filePath.StartsWith(Path.Combine(programFiles, "WindowsApps"), StringComparison.OrdinalIgnoreCase) ||
                 filePath.StartsWith(Path.Combine(programFiles, "Common Files", "Microsoft Shared"), StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>判断是否用户软件常规路径。</summary>
    /// <param name="filePath">文件路径。</param>
    public static bool IsUserPath(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        string? programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
        string? programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        return
            (!string.IsNullOrEmpty(programFiles) && filePath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(programFilesX86) && filePath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase)) ||
            filePath.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase) ||
            filePath.StartsWith(appData, StringComparison.OrdinalIgnoreCase) ||
            filePath.StartsWith(programData, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>分类对应的禁用保护级别（纯函数）。</summary>
    /// <param name="kind">来源分类。</param>
    public static DisableProtection GetProtection(StartupSourceKind kind) => kind switch
    {
        StartupSourceKind.MicrosoftWindows => DisableProtection.ConfirmSystemCritical,
        StartupSourceKind.HardwareDriver => DisableProtection.ConfirmHardware,
        _ => DisableProtection.Allowed,
    };
}
