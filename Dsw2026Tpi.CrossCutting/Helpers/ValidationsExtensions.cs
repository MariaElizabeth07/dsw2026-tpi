using System.Text.RegularExpressions;

namespace Dsw2026Tpi.CrossCutting.Helpers;

public static class ValidationsExtensions
{
    public const string EmailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$";
    public static bool IsEmailValid(this string? email)
    {
        return !string.IsNullOrWhiteSpace(email) &&
            Regex.IsMatch(email, EmailPattern);
    }

    public static bool IsPatientLoginDNIValid(this long dni)
    {
        return dni is >= 1_000_000 and <= 99_999_999;
    }

    public static bool IsAppointmentDNIValid(this long dni)
    {
        return dni is >= 1_000_000 and <= 9_999_999_999;
    }
}
