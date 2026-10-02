namespace Luna.Identity.Infrastructure;

public sealed class OpenIddictKeyOptions
{
    public const string SectionName = "OpenIddict:Keys";

    public string? SigningKeyPath { get; set; }

    public string? EncryptionKeyPath { get; set; }

    public string? Passphrase { get; set; }
}
