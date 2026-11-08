using Dsw2026Tpi.Api.Configurations;
using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2026Tpi.Tests.Unit;

public class SecurityPolicyTests
{
    [Fact]
    public async Task CatalogReadPolicy_CuandoSeRegistra_EntoncesPermiteAdministradoresYPacientes()
    {
        var services = new ServiceCollection();
        services.AddAppAuthentication(BuildConfiguration());

        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync(Policies.CatalogReadPolicy);

        Assert.NotNull(policy);

        var rolesRequirement = Assert.Single(
            policy!.Requirements.OfType<RolesAuthorizationRequirement>());

        Assert.Contains(Roles.Administrator, rolesRequirement.AllowedRoles);
        Assert.Contains(Roles.Patient, rolesRequirement.AllowedRoles);
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = new string('k', 64),
                ["Jwt:Issuer"] = "Dsw2026Tpi.Api",
                ["Jwt:Audience"] = "Dsw2026Tpi.ApiUsers"
            })
            .Build();
    }
}
