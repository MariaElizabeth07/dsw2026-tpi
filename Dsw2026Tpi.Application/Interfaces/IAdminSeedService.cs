namespace Dsw2026Tpi.Application.Interfaces;

public interface IAdminSeedService
{
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);
}
