using Dsw2026Tpi.Data.Identity;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(ApplicationUser user, string? role);
}
