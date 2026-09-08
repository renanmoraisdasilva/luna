using System.Security.Claims;
using FluentAssertions;
using Luna.Authentication;
using Luna.Contracts.Authentication;
using Xunit;

namespace Luna.IntegrationTests.Authentication;

public sealed class ClaimsPrincipalTests
{
    [Fact]
    public void Reads_a_valid_subject_claim()
    {
        var expected = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(LunaAuthentication.SubjectClaim, expected.ToString()),
        ]));

        principal.TryGetSubjectId(out var actual).Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Fact]
    public void Falls_back_to_name_identifier_and_rejects_invalid_or_empty_values()
    {
        var expected = Guid.NewGuid();
        var fallback = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, expected.ToString()),
        ]));
        var invalid = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(LunaAuthentication.SubjectClaim, "not-a-guid"),
        ]));
        var empty = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(LunaAuthentication.SubjectClaim, Guid.Empty.ToString()),
        ]));

        fallback.TryGetSubjectId(out var fallbackId).Should().BeTrue();
        fallbackId.Should().Be(expected);
        invalid.TryGetSubjectId(out _).Should().BeFalse();
        empty.TryGetSubjectId(out _).Should().BeFalse();
    }
}
