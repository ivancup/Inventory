using Inventory.Domain.ApiModels;
using System.Security.Claims;

namespace Inventory.Api.Security;

public static class ActorIdentityFactory
{
    public static ActorIdentity FromPrincipal(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue("sub") ?? string.Empty;
        var name = principal.FindFirstValue("name")
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.Identity?.Name
            ?? subject;
        var email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email);
        return new ActorIdentity(subject, name, email);
    }
}
