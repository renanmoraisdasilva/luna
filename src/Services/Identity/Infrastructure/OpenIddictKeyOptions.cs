namespace Luna.Identity.Infrastructure;

/// <summary>
/// Configuration for the OpenIddict signing and encryption keys.
/// </summary>
/// <remarks>
/// The authorization server must sign tokens with a key that survives restarts. A development certificate is
/// ephemeral: it is generated on demand and not persisted, so every restart produces a different key,
/// invalidating every outstanding access token and every other service's cached JWKS. Production therefore
/// requires an explicitly configured key and refuses to start without one.
/// </remarks>
public sealed class OpenIddictKeyOptions
{
    public const string SectionName = "OpenIddict:Keys";

    /// <summary>
    /// Absolute path to a PKCS#12 (.pfx) or PEM-encoded private key used for signing tokens.
    /// </summary>
    public string? SigningKeyPath { get; set; }

    /// <summary>
    /// Absolute path to a PKCS#12 (.pfx) or PEM-encoded private key used for encrypting tokens.
    /// </summary>
    public string? EncryptionKeyPath { get; set; }

    /// <summary>
    /// Passphrase protecting the key files, when they are encrypted.
    /// </summary>
    public string? Passphrase { get; set; }
}
