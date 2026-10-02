using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Luna.Identity.Infrastructure;

/// <summary>
/// Loads the X.509 material the authorization server signs and encrypts tokens with.
/// </summary>
/// <remarks>
/// PEM is supported alongside PKCS#12 because that is what <c>dotnet dev-certs</c> and most secret tooling
/// produce. PKCS#12 is detected from its DER header rather than its file extension, so a misnamed file still
/// loads correctly.
/// </remarks>
public static class OpenIddictKeyLoader
{
    /// <summary>
    /// DER SEQUENCE header. A PKCS#12 file always begins with a SEQUENCE whose two-byte length follows.
    /// </summary>
    private static ReadOnlySpan<byte> Pkcs12Magic => [0x30, 0x82];

    /// <summary>
    /// Loads a signing or encryption certificate from a PKCS#12 or PEM file.
    /// </summary>
    /// <param name="path">Absolute path to the key file.</param>
    /// <param name="passphrase">Passphrase for the file, when it is protected.</param>
    /// <param name="purpose">Name of the key being loaded, used in error messages.</param>
    /// <exception cref="InvalidOperationException">
    /// The file is absent, empty, or cannot be read as a usable certificate.
    /// </exception>
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
