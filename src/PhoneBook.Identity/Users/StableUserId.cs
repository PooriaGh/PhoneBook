using System.Security.Cryptography;
using System.Text;

namespace PhoneBook.Identity.Users;

/// <summary>
/// Name-based RFC 4122 version-5 GUIDs, so a configured user keeps the same <c>sub</c> across restarts
/// (feature 002, data-model §5).
/// </summary>
public static class StableUserId
{
    /// <summary>Fixed namespace for PhoneBook user ids.</summary>
    private static readonly Guid Namespace = new("6f3c1f0e-2b8a-4d52-9d6e-7a1c0b4e9f21");

    public static Guid For(string userName)
    {
        ArgumentNullException.ThrowIfNull(userName);

        var namespaceBytes = Namespace.ToByteArray(bigEndian: true);
        var nameBytes = Encoding.UTF8.GetBytes(userName.ToUpperInvariant());
#pragma warning disable CA5350 // SHA-1 is mandated by RFC 4122 for version-5 GUIDs; it is not used for security here.
        var hash = SHA1.HashData([.. namespaceBytes, .. nameBytes]);
#pragma warning restore CA5350

        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(bytes, bigEndian: true);
    }
}
