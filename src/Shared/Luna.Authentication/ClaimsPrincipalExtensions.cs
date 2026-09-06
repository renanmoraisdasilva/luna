using System.Security.Claims;
using Luna.Contracts.Authentication;

namespace Luna.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetSubjectId(this ClaimsPrincipal principal, out Guid subjectId)
    {
        var value = principal.FindFirstValue(LunaAuthentication.SubjectClaim)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out subjectId) && subjectId != Guid.Empty;
    }
}
