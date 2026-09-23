using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using LeslieCleaner.Core.Abstractions;

namespace LeslieCleaner.Modules.Startup.Classification;

/// <summary>
/// Authenticode 数字签名验证（真实验签）：
/// WinVerifyTrust（WINTRUST_ACTION_GENERIC_VERIFY_V2）判定有效性，
/// X509Certificate2.CreateFromSignedFile 提取签名者主题。
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class SignatureVerifier
{
    private static readonly Guid WintrustActionGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private readonly ILogService _logService;

    /// <summary>以日志服务创建验签器。</summary>
    /// <param name="logService">日志服务。</param>
    public SignatureVerifier(ILogService logService) => _logService = logService;

    /// <summary>
    /// 验证文件数字签名。
    /// </summary>
    /// <param name="filePath">可执行文件完整路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<SignatureInfo> VerifyAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Task.Run(() => VerifyCore(filePath), cancellationToken);
    }

    private SignatureInfo VerifyCore(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new SignatureInfo(false, null);
        }

        // 有效性：WinVerifyTrust（0=有效；系统文件常见 catalog 签名可能返回 TRUST_E_NOSIGNATURE，
        // 此时分类层回退路径规则判定）
        bool isValid = VerifyTrust(filePath) == 0;

        // 签名者主题：CreateFromSignedFile（catalog 签名/无签名文件会抛异常，按缺失处理）
        string? signerSubject = null;
        try
        {
            using var certificate = X509Certificate2.CreateFromSignedFile(filePath);
            signerSubject = certificate.Subject;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FileNotFoundException or UnauthorizedAccessException)
        {
            if (isValid)
            {
                _logService.LogException(ex, "SignatureVerifier.ExtractSigner",
                    new Dictionary<string, object?> { ["File"] = filePath });
            }
        }

        return new SignatureInfo(isValid, signerSubject);
    }

    private static int VerifyTrust(string filePath)
    {
        var fileInfo = new WintrustFileInfo(filePath);
        IntPtr pFile = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
        Marshal.StructureToPtr(fileInfo, pFile, false);
        var data = new WintrustData { FileStateData = pFile };
        IntPtr pData = Marshal.AllocHGlobal(Marshal.SizeOf(data));
        Marshal.StructureToPtr(data, pData, false);
        try
        {
            Guid actionId = WintrustActionGenericVerifyV2;
            int result = WinVerifyTrust(IntPtr.Zero, actionId, pData);

            // WTD_STATEACTION_CLOSE：释放验证阶段创建的状态句柄（读回后重发关闭）
            if (Marshal.PtrToStructure(pData, typeof(WintrustData)) is WintrustData closeData)
            {
                closeData.StateAction = 2;
                Marshal.StructureToPtr(closeData, pData, false);
                _ = WinVerifyTrust(IntPtr.Zero, actionId, pData);
            }

            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(pData);
            Marshal.FreeHGlobal(pFile);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class WintrustFileInfo
    {
        public uint StructSize = (uint)Marshal.SizeOf<WintrustFileInfo>();
        public string FilePath;
        public IntPtr FileHandle = IntPtr.Zero;
        public IntPtr KnownSubject = IntPtr.Zero;

        public WintrustFileInfo(string filePath) => FilePath = filePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class WintrustData
    {
        public uint StructSize = (uint)Marshal.SizeOf<WintrustData>();
        public IntPtr PolicyCallbackData = IntPtr.Zero;
        public IntPtr SipClientData = IntPtr.Zero;
        public uint UiChoice = 2; // WTD_UI_NONE
        public uint RevocationChecks = 0; // WTD_REVOKE_NONE（离线环境跳过吊销检查）
        public uint UnionChoice = 1; // WTD_CHOICE_FILE
        public IntPtr FileStateData;
        public uint StateAction = 0; // WTD_STATEACTION_VERIFY
        public IntPtr TrustProviderContext = IntPtr.Zero; // hWVTStateData
        public IntPtr UrlReference = IntPtr.Zero; // pwszURLReference
        public uint ProvFlags = 0; // dwProvFlags
        public uint UiContext = 0; // dwUIContext
        public IntPtr SignatureSettings = IntPtr.Zero;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
    private static extern int WinVerifyTrust(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.LPStruct)] Guid actionId,
        IntPtr trustData);
}
