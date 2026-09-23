using LeslieCleaner.Modules.Startup.Classification;

namespace LeslieCleaner.UI.ViewModels;

public sealed partial class StartupItemRow
{
    /// <summary>Human-readable signature state that distinguishes deferred verification from an invalid signature.</summary>
    public string SignatureVerificationText => Detail.Signature.VerificationState == SignatureVerificationState.NotChecked
        ? "待验证"
        : Detail.Signature.IsValid
            ? StartupClassifier.IsMicrosoftSigner(Detail.Signature.SignerSubject)
                ? "Microsoft 已验证"
                : "已签名"
            : "未签名或无效";
}
