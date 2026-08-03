using System.Text.Json;
using Dsw2026Tpi.Data.Options;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Data.Providers;

public class HolidayProvider : IHolidayProvider
{
    private static readonly Lazy<HashSet<DateOnly>> Holidays = new(LoadHolidays);

    public bool IsHoliday(DateOnly date)
    {
        return Holidays.Value.Contains(date);
    }

    private static HashSet<DateOnly> LoadHolidays()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Sources", "holidays.json");

        if (!File.Exists(path))
        {
            return [];
        }

        var json = File.ReadAllText(path);
        var entries = JsonSerializer.Deserialize<List<HolidayEntry>>(json, JsonOptions.JsonSerializerOptions) ?? [];

        return entries.Select(entry => entry.Date).ToHashSet();
    }

    private sealed record HolidayEntry(DateOnly Date, string? Description);
}
