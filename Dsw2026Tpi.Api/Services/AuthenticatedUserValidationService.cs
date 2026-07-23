using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Dsw2026Tpi.Api.Services;

public class AuthenticatedUserValidationService(
    UserManager<ApplicationUser> userManager) : IAuthenticatedUserValidationService
{
    public async Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Name);

        ApplicationUser? user = null;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            user = await userManager.FindByIdAsync(userId);
        }

        if (user is null && !string.IsNullOrWhiteSpace(email))
        {
            user = await userManager.FindByEmailAsync(email);
        }

        if (user is null || user.Deleted)
        {
            return false;
        }

        var tokenSecurityStamp = principal.FindFirstValue("security_stamp");
        if (string.IsNullOrWhiteSpace(tokenSecurityStamp))
        {
            return false;
        }

        return string.Equals(tokenSecurityStamp, user.SecurityStamp, StringComparison.Ordinal);
    }
}
