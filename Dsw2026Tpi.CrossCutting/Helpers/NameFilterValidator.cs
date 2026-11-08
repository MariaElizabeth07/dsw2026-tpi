using Dsw2026Tpi.CrossCutting.Exceptions;

namespace Dsw2026Tpi.CrossCutting.Helpers;

public static class NameFilterValidator
{
    public static void Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (name.Trim().Length is < 3 or > 100)
        {
            throw new ValidationException()
                .WithDetail(nameof(name), "El filtro name debe tener entre 3 y 100 caracteres.");
        }
    }
}
