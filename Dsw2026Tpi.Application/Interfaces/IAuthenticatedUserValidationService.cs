using System.Security.Claims;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IAuthenticatedUserValidationService
{
    Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
