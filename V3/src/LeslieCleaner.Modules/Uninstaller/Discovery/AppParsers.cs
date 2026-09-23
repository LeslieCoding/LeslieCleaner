using System.Text.Json;

namespace LeslieCleaner.Modules.Uninstaller.Discovery;

/// <summary>
/// 各来源的解析器（纯函数，可独立单元测试）。
/// 覆盖：Uninstall 键值、Steam ACF / libraryfolders.vdf、Epic .item 清单、
/// VS 实例 state.json、winget list 表格文本。
/// </summary>
public static partial class AppParsers
{
    /// <summary>
    /// 从 Uninstall 键值字典解析应用记录。
    /// </summary>
    /// <param name="keyName">键名（MSI 时为 ProductCode GUID）。</param>
    /// <param name="values">值字典（DisplayName/DisplayVersion/Publisher/...）。</param>
    /// <param name="sourcePrefix">来源前缀（hklm64/hklm32/hkcu）。</param>
    public static InstalledApp? ParseUninstallValues(
        string keyName,
        IReadOnlyDictionary<string, object?> values,
        string sourcePrefix)
    {
        ArgumentNullException.ThrowIfNull(keyName);
        ArgumentNullException.ThrowIfNull(values);

        string? displayName = GetString(values, "DisplayName");
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        // 系统组件与补丁不列入应用清单
        if (GetInt(values, "SystemComponent") == 1)
        {
            return null;
        }

        bool isMsi = GetInt(values, "WindowsInstaller") == 1 ||
            (keyName.StartsWith('{') && keyName.EndsWith('}'));
        long sizeBytes = (long)(GetInt(values, "EstimatedSize") ?? 0) * 1024;

        return new InstalledApp(
            $"{sourcePrefix}:{keyName}",
            displayName,
            GetString(values, "DisplayVersion"),
            GetString(values, "Publisher"),
            sizeBytes,
            ParseInstallDate(GetString(values, "InstallDate")),
            GetString(values, "InstallLocation"),
            GetString(values, "UninstallString"),
            isMsi ? AppSourceType.Msi : AppSourceType.Win32,
            isMsi && keyName.StartsWith('{') ? keyName : null,
            GetString(values, "UpgradeCode"));
    }

    /// <summary>
    /// 解析 Steam appmanifest_*.acf 文本。
    /// </summary>
    /// <param name="acfContent">ACF 文件内容。</param>
    /// <param name="libraryRoot">所属库根（…\steamapps）。</param>
    public static InstalledApp? ParseSteamAppManifest(string acfContent, string libraryRoot)
    {
        ArgumentNullException.ThrowIfNull(acfContent);
        string? appId = ExtractVdfValue(acfContent, "appid");
        string? name = ExtractVdfValue(acfContent, "name");
        string? installDir = ExtractVdfValue(acfContent, "installdir");
        string? sizeOnDisk = ExtractVdfValue(acfContent, "SizeOnDisk");
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new InstalledApp(
            $"steam:{appId}", name, null, "Valve",
            long.TryParse(sizeOnDisk, out long size) ? size : 0,
            null,
            string.IsNullOrWhiteSpace(installDir) ? null : Path.Combine(libraryRoot, "common", installDir),
            $"steam://uninstall/{appId}",
            AppSourceType.Steam, null, null);
    }

    /// <summary>
    /// 解析 Steam libraryfolders.vdf，返回库 steamapps 目录列表（含主库与自定义库）。
    /// </summary>
    /// <param name="vdfContent">vdf 文件内容。</param>
    /// <param name="steamRoot">Steam 安装根目录。</param>
    public static IReadOnlyList<string> ParseLibraryFolders(string vdfContent, string steamRoot)
    {
        ArgumentNullException.ThrowIfNull(vdfContent);
        var libraries = new List<string>();
        string mainLibrary = Path.Combine(steamRoot, "steamapps");
        if (Directory.Exists(mainLibrary))
        {
            libraries.Add(mainLibrary);
        }

        foreach (System.Text.RegularExpressions.Match match in LibraryPathRegex().Matches(vdfContent))
        {
            string path = match.Groups[1].Value.Replace("\\\\", "\\");
            string steamapps = Path.Combine(path, "steamapps");
            if (Directory.Exists(steamapps) && !libraries.Contains(steamapps, StringComparer.OrdinalIgnoreCase))
            {
                libraries.Add(steamapps);
            }
        }

        return libraries;
    }

    /// <summary>
    /// 解析 Epic .item 清单 JSON。
    /// </summary>
    /// <param name="json">清单内容。</param>
    /// <param name="manifestPath">清单文件路径（用于 Id）。</param>
    public static InstalledApp? ParseEpicManifest(string json, string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string? name = GetJsonString(root, "DisplayName");
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            long size = root.TryGetProperty("InstallSize", out JsonElement sizeElement) &&
                sizeElement.TryGetInt64(out long parsed) ? parsed : 0;

            return new InstalledApp(
                $"epic:{GetJsonString(root, "AppName") ?? Path.GetFileNameWithoutExtension(manifestPath)}",
                name,
                GetJsonString(root, "AppVersion"),
                "Epic Games",
                size,
                null,
                GetJsonString(root, "InstallLocation"),
                null,
                AppSourceType.Epic, null, null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 解析 Visual Studio 实例 state.json（%ProgramData%\Microsoft\VisualStudio\Packages\_Instances\*）。
    /// </summary>
    /// <param name="json">state.json 内容。</param>
    /// <param name="instanceId">实例目录名。</param>
    public static InstalledApp? ParseVisualStudioInstanceState(string json, string instanceId)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string? name = GetJsonString(root, "displayName") ?? GetJsonString(root, "productName");
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string? version = null;
            if (root.TryGetProperty("catalogInfo", out JsonElement catalog) &&
                catalog.TryGetProperty("buildVersion", out JsonElement build))
            {
                version = build.GetString();
            }

            return new InstalledApp(
                $"vs:{instanceId}", name, version, "Microsoft Corporation", 0, null,
                GetJsonString(root, "installationPath"), null,
                AppSourceType.VisualStudio, null, null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 解析 winget list 表格文本（固定列宽，含中文标题）。
    /// </summary>
    /// <param name="output">winget list 标准输出。</param>
    public static IReadOnlyList<InstalledApp> ParseWingetList(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var apps = new List<InstalledApp>();
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int headerIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Id") && (lines[i].Contains("名称") || lines[i].Contains("Name")))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex < 0 || headerIndex + 2 >= lines.Length)
        {
            return apps;
        }

        // 表头下一行是分隔线（---），数据从 +2 开始；按空白 2+ 分列容错
        for (int i = headerIndex + 2; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = System.Text.RegularExpressions.Regex.Split(line, @"\s{2,}");
            if (parts.Length < 2)
            {
                continue;
            }

            string name = parts[0].Trim();
            string id = parts.Length > 1 ? parts[1].Trim() : name;
            string? version = parts.Length > 2 ? parts[2].Trim() : null;
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith('-'))
            {
                continue;
            }

            apps.Add(new InstalledApp(
                $"winget:{id}", name, version, null, 0, null, null, null,
                AppSourceType.Winget, null, null));
        }

        return apps;
    }

    /// <summary>
    /// 按名称/发布者把 Win32 记录重分类到 Adobe/Office/VS/DockerWsl/DevEnvironment/Store。
    /// </summary>
    /// <param name="app">原始记录。</param>
    public static InstalledApp ClassifyBySignature(InstalledApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        string text = $"{app.Name} {app.Publisher}".ToLowerInvariant();
        AppSourceType source = app.Source;
        if (source is AppSourceType.Win32 or AppSourceType.Msi)
        {
            if (text.Contains("adobe"))
            {
                source = AppSourceType.Adobe;
            }
            else if (text.Contains("microsoft office") || text.Contains("microsoft 365"))
            {
                source = AppSourceType.Office;
            }
            else if (text.Contains("visual studio"))
            {
                source = AppSourceType.VisualStudio;
            }
            else if (text.Contains("docker") || text.Contains("wsl"))
            {
                source = AppSourceType.DockerWsl;
            }
            else if (text.Contains("node.js") || text.Contains("python") ||
                     text.Contains("openjdk") || text.Contains("java"))
            {
                source = AppSourceType.DevEnvironment;
            }
            else if (text.Contains("blizzard") || text.Contains("battle.net"))
            {
                source = AppSourceType.BattleNet;
            }
        }

        return source == app.Source ? app : app with { Source = source };
    }

    /// <summary>解析 InstallDate（yyyyMMdd 或 yyyy/MM/dd）。</summary>
    /// <param name="raw">原始值。</param>
    public static DateOnly? ParseInstallDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string digits = new([.. raw.Where(char.IsDigit)]);
        return digits.Length == 8 &&
            DateOnly.TryParseExact(digits, "yyyyMMdd", out DateOnly parsed)
            ? parsed
            : null;
    }

    private static string? ExtractVdfValue(string content, string key)
    {
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            content, $"\"{key}\"\\s+\"(?<value>[^\"]*)\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? value) ? value as string : null;

    private static int? GetInt(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? value) && value is int intValue ? intValue : null;

    private static string? GetJsonString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    [System.Text.RegularExpressions.GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial System.Text.RegularExpressions.Regex LibraryPathRegex();
}
