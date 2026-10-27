using Microsoft.AspNetCore.DataProtection;

namespace Orbit.Core.Security;

/// <summary>Encrypts/decrypts provider API tokens (and later SSH keys) at rest.</summary>
public interface ITokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedPayload);
}

public sealed class TokenProtector : ITokenProtector
{
    private readonly IDataProtector _protector;

    public TokenProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Orbit.Credentials.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedPayload) => _protector.Unprotect(protectedPayload);
}
