using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;

namespace Dsw2026Tpi.Api.Services;

public class AdminSeedService(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<AdminSeedService> logger) : IAdminSeedService
{
    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue<bool>("AdminSeed:Enabled");
        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];

        if (!enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("AdminSeed requiere Email y Password.");
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            if (existingUser.Deleted)
            {
                logger.LogWarning("El administrador inicial existe pero está eliminado.");
                return;
            }

            if (!await userManager.IsInRoleAsync(existingUser, Roles.Administrator))
            {
                var addRoleResult = await userManager.AddToRoleAsync(existingUser, Roles.Administrator);
                if (!addRoleResult.Succeeded)
                {
                    throw new InvalidOperationException(ErrorCodes.REGISTER_USER_CONFLICT);
                }
            }

            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(ErrorCodes.REGISTER_USER_CONFLICT);
        }

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Administrator);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(ErrorCodes.REGISTER_USER_CONFLICT);
        }

        logger.LogInformation("Administrador inicial creado correctamente.");
    }
}
