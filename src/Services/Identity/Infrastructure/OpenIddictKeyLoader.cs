using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Luna.Identity.Infrastructure;

public static class OpenIddictKeyLoader
{
    private static ReadOnlySpan<byte> Pkcs12Magic => [0x30, 0x82];

    public static X509Certificate2 Load(string path, string? passphrase, string purpose)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"No path was configured for the {purpose} key.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"The {purpose} key was not found at '{path}'. It must be a readable PKCS#12 or PEM certificate.");
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException($"The {purpose} key file at '{path}' is empty.");
        }

        try
        {
            return IsPkcs12(bytes)
                ? new X509Certificate2(bytes, passphrase, X509KeyStorageFlags.EphemeralKeySet)
                : X509Certificate2.CreateFromPemFile(path, passphrase);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The {purpose} key at '{path}' could not be loaded. It must be a PKCS#12 or PEM certificate with a " +
                "usable private key, and OpenIddict:Keys:Passphrase must match when the file is protected.",
                exception);
        }
    }

    private static bool IsPkcs12(ReadOnlySpan<byte> bytes) =>
        bytes.Length > 2 && bytes[..2].SequenceEqual(Pkcs12Magic);
}
