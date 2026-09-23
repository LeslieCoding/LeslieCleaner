using LeslieCleaner.Core.Models;

namespace LeslieCleaner.Core.Abstractions;

/// <summary>本地敏感数据加密和密钥轮换契约。</summary>
public interface IEncryptionService
{
    /// <summary>使用当前 AES-256 密钥加密数据，并将密钥标识、随机 nonce 和认证标签写入密文。</summary>
    byte[] Encrypt(byte[] data);

    /// <summary>验证认证标签并解密数据；密文被篡改或密钥不可用时抛出加密异常。</summary>
    byte[] Decrypt(byte[] encryptedData);

    /// <summary>生成新的活动密钥；调用方应使用 <see cref="Decrypt(byte[])"/> 和 <see cref="Encrypt(byte[])"/> 重加密持久化数据后再淘汰旧密钥。</summary>
    Task<KeyRotationResult> RotateKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>淘汰非活动密钥；调用方必须先完成所有关联数据的重加密。</summary>
    Task RetireKeyAsync(string keyId, CancellationToken cancellationToken = default);
}
