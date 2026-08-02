using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Dsw2026Tpi.Domain.Interfaces;
using Dsw2026Tpi.Data.Options;

namespace Dsw2026Tpi.Data;

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