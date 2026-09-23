namespace LeslieCleaner.Modules.Privacy;

/// <summary>隐私数据类别。</summary>
public enum PrivacyCategory
{
    /// <summary>最近文件（RecentDocs + Recent 文件夹）。</summary>
    RecentFiles,

    /// <summary>剪贴板历史缓存。</summary>
    ClipboardHistory,

    /// <summary>搜索历史（WordWheelQuery）。</summary>
    SearchHistory,

    /// <summary>运行历史（RunMRU）。</summary>
    RunHistory,

    /// <summary>Office 文件历史（File MRU）。</summary>
    OfficeHistory,

    /// <summary>浏览器隐私数据（经 IBrowserPrivacyCleaner）。</summary>
    BrowserPrivacy,

    /// <summary>地理位置访问记录。</summary>
    LocationCache,

    /// <summary>语音输入缓存。</summary>
    SpeechCache,
}

/// <summary>一条隐私扫描命中项。</summary>
/// <param name="Category">类别。</param>
/// <param name="Description">描述。</param>
/// <param name="Target">清理目标（文件路径 / 注册表键 / "键|值" 编码）。</param>
/// <param name="IsRegistryTarget">是否注册表目标（删除前强制备份）。</param>
/// <param name="SizeBytes">占用字节（注册表项为 0）。</param>
public sealed record PrivacyItem(
    PrivacyCategory Category,
    string Description,
    string Target,
    bool IsRegistryTarget,
    long SizeBytes);
