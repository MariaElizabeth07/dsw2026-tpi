namespace Dsw2026Tpi.CrossCutting.Helpers;

public static class DateTimeHelper
{
    private static readonly Dictionary<string, DayOfWeek> SupportedDays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LUNES"] = DayOfWeek.Monday,
        ["MARTES"] = DayOfWeek.Tuesday,
        ["MIERCOLES"] = DayOfWeek.Wednesday,
        ["MIÉRCOLES"] = DayOfWeek.Wednesday,
        ["JUEVES"] = DayOfWeek.Thursday,
        ["VIERNES"] = DayOfWeek.Friday,
        ["SABADO"] = DayOfWeek.Saturday,
        ["SÁBADO"] = DayOfWeek.Saturday,
        ["DOMINGO"] = DayOfWeek.Sunday,
        ["MONDAY"] = DayOfWeek.Monday,
        ["TUESDAY"] = DayOfWeek.Tuesday,
        ["WEDNESDAY"] = DayOfWeek.Wednesday,
        ["THURSDAY"] = DayOfWeek.Thursday,
        ["FRIDAY"] = DayOfWeek.Friday,
        ["SATURDAY"] = DayOfWeek.Saturday,
        ["SUNDAY"] = DayOfWeek.Sunday,
    };

    public static MonthRange GetCurrentMonthRangeFromToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStartDate = new DateOnly(today.Year, today.Month, 1);
        var monthEndDate = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        return new MonthRange(today.Year, today.Month, monthStartDate, today, monthEndDate);
    }

    public static IReadOnlyCollection<DateOnly> GetDatesForDay(DayOfWeek dayOfWeek, DateOnly startDate, DateOnly endDate)
    {
        var dates = new List<DateOnly>();
        var current = startDate;

        while (current.DayOfWeek != dayOfWeek && current <= endDate)
        {
            current = current.AddDays(1);
        }

        while (current <= endDate)
        {
            dates.Add(current);
            current = current.AddDays(7);
        }

        return dates;
    }

    public static IReadOnlyCollection<TimeInterval> BuildThirtyMinuteIntervals(TimeOnly startTime, TimeOnly endTime)
    {
        var intervals = new List<TimeInterval>();
        var cursor = startTime;

        while (cursor < endTime)
        {
            var next = cursor.AddMinutes(30);
            if (next > endTime)
            {
                break;
            }

            intervals.Add(new TimeInterval(cursor, next));
            cursor = next;
        }

        return intervals;
    }

    public static bool TryParseSupportedDay(string input, out DayOfWeek dayOfWeek)
    {
        dayOfWeek = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return SupportedDays.TryGetValue(input.Trim(), out dayOfWeek);
    }
}

public record MonthRange(int Year, int Month, DateOnly MonthStartDate, DateOnly StartDate, DateOnly EndDate);

public record TimeInterval(TimeOnly StartTime, TimeOnly EndTime);
