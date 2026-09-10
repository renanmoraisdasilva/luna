using System.Security.Claims;
using FluentAssertions;
using Luna.Authentication;
using Luna.Contracts.Authentication;
using Xunit;

namespace Luna.UnitTests.Authentication;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void Reads_the_subject_claim()
    {
        var id = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(LunaAuthentication.SubjectClaim, id.ToString())]));

        principal.TryGetSubjectId(out var subjectId).Should().BeTrue();
        subjectId.Should().Be(id);
    }

    [Fact]
    public void Falls_back_to_name_identifier_and_rejects_invalid_values()
    {
        var id = Guid.NewGuid();
        var fallback = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())]));
        var invalid = new ClaimsPrincipal(new ClaimsIdentity([new Claim(LunaAuthentication.SubjectClaim, "not-a-guid")]));

        fallback.TryGetSubjectId(out var fallbackId).Should().BeTrue();
        fallbackId.Should().Be(id);
        invalid.TryGetSubjectId(out _).Should().BeFalse();
    }
}
