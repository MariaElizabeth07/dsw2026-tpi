using System.Globalization;
using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Enums;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services;

public class AvailabilityService : IAvailabilityService
{
    private const string AvailabilityHasBookedSlotsErrorCode = "AVAILABILITY_HAS_BOOKED_SLOTS";
    private const string AvailabilityHasBookedSlotsMessage = "No se puede sobrescribir la disponibilidad porque existen turnos reservados en el mes actual.";
    private const string AvailabilityOverlapErrorCode = "AVAILABILITY_OVERLAP";
    private const string AvailabilityOverlapMessage = "No se permiten horarios solapados para el mismo médico.";
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

    private readonly IPersistence _persistence;

    public AvailabilityService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<IReadOnlyCollection<AvailabilityModel.Response>> Create(AvailabilityModel.Request request)
    {
        var context = BuildMonthContext();
        var normalizedDays = NormalizeRequest(request);
        var doctor = await GetDoctor(request.DoctorId);

        var existingRules = (await _persistence.GetFiltered<AvailabilityRule>(
            rule => rule.DoctorId == doctor.Id && rule.Year == context.Year && rule.Month == context.Month))
            ?.ToList() ?? [];

        ValidateOverlaps(existingRules, normalizedDays);

        foreach (var day in normalizedDays)
        {
            var matchingRule = existingRules.FirstOrDefault(rule =>
                rule.DayOfWeek == day.DayOfWeek &&
                rule.StartTime == day.StartTime &&
                rule.EndTime == day.EndTime);

            if (matchingRule is null)
            {
                matchingRule = new AvailabilityRule(doctor, context.Month, context.Year, day.DayOfWeek, day.StartTime, day.EndTime);
                await _persistence.Add(matchingRule);
                existingRules.Add(matchingRule);
            }

            await EnsureSlotsExist(doctor, matchingRule, day, context);
        }

        return MapResponses(existingRules);
    }

    public async Task<IReadOnlyCollection<AvailabilityModel.Response>> Update(AvailabilityModel.Request request)
    {
        var context = BuildMonthContext();
        var normalizedDays = NormalizeRequest(request);
        ValidateOverlaps([], normalizedDays);

        var doctor = await GetDoctor(request.DoctorId);
        var existingRules = (await _persistence.GetFiltered<AvailabilityRule>(
            rule => rule.DoctorId == doctor.Id && rule.Year == context.Year && rule.Month == context.Month))
            ?.ToList() ?? [];

        var existingSlots = (await _persistence.GetFiltered<AvailabilitySlot>(
            slot => slot.DoctorId == doctor.Id &&
                slot.SlotDate >= context.MonthStartDate &&
                slot.SlotDate <= context.EndDate))
            ?.ToList() ?? [];

        if (existingSlots.Any(slot => slot.Status == SlotStatus.Booked))
        {
            throw new ConflictException(AvailabilityHasBookedSlotsErrorCode, AvailabilityHasBookedSlotsMessage);
        }

        foreach (var slot in existingSlots)
        {
            slot.Delete();
            await _persistence.Update(slot);
        }

        foreach (var rule in existingRules)
        {
            rule.Delete();
            await _persistence.Update(rule);
        }

        var createdRules = new List<AvailabilityRule>();
        foreach (var day in normalizedDays)
        {
            var rule = new AvailabilityRule(doctor, context.Month, context.Year, day.DayOfWeek, day.StartTime, day.EndTime);
            await _persistence.Add(rule);
            createdRules.Add(rule);
            await EnsureSlotsExist(doctor, rule, day, context);
        }

        return MapResponses(createdRules);
    }

    public async Task<IReadOnlyCollection<AvailabilityModel.Response>> GetByDoctor(Guid doctorId)
    {
        _ = await GetDoctor(doctorId);
        var context = BuildMonthContext();

        var rules = (await _persistence.GetFiltered<AvailabilityRule>(
            rule => rule.DoctorId == doctorId && rule.Year == context.Year && rule.Month == context.Month))
            ?.ToList() ?? [];

        return MapResponses(rules);
    }

    private async Task<Doctor> GetDoctor(Guid doctorId)
    {
        if (doctorId == Guid.Empty)
        {
            throw new ValidationException()
                .WithDetail(nameof(AvailabilityModel.Request.DoctorId), "El doctorId es obligatorio.");
        }

        return await _persistence.GetById<Doctor>(doctorId)
            ?? throw new EntityNotFoundException(nameof(Doctor));
    }

    private async Task EnsureSlotsExist(Doctor doctor, AvailabilityRule rule, NormalizedDayRequest day, MonthContext context)
    {
        var monthSlots = (await _persistence.GetFiltered<AvailabilitySlot>(
            slot => slot.DoctorId == doctor.Id &&
                slot.SlotDate >= context.StartDate &&
                slot.SlotDate <= context.EndDate))
            ?.ToList() ?? [];

        foreach (var date in GetDatesForDay(day.DayOfWeek, context.StartDate, context.EndDate))
        {
            foreach (var interval in BuildIntervals(day.StartTime, day.EndTime))
            {
                var exists = monthSlots.Any(slot =>
                    slot.SlotDate == date &&
                    slot.StartTime == interval.StartTime &&
                    slot.EndTime == interval.EndTime);

                if (exists)
                {
                    continue;
                }

                var slot = new AvailabilitySlot(doctor, rule, date, interval.StartTime, interval.EndTime);
                await _persistence.Add(slot);
                monthSlots.Add(slot);
            }
        }
    }

    private static IReadOnlyCollection<AvailabilityModel.Response> MapResponses(IEnumerable<AvailabilityRule> rules)
    {
        return rules
            .OrderBy(rule => ToSortOrder(rule.DayOfWeek))
            .ThenBy(rule => rule.StartTime)
            .Select(rule => new AvailabilityModel.Response(
                ToSpanishDay(rule.DayOfWeek),
                rule.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                rule.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static List<NormalizedDayRequest> NormalizeRequest(AvailabilityModel.Request request)
    {
        var exception = new ValidationException();

        if (request.Days is null)
        {
            exception.WithDetail(nameof(request.Days), "days debe ser un arreglo.");
            throw exception;
        }

        var normalizedDays = new List<NormalizedDayRequest>();
        var index = 0;
        foreach (var day in request.Days)
        {
            var fieldPrefix = $"{nameof(request.Days)}[{index}]";

            if (string.IsNullOrWhiteSpace(day.Day) || !TryParseDay(day.Day, out var dayOfWeek))
            {
                exception.WithDetail($"{fieldPrefix}.day", "El día indicado no es válido.");
            }

            var startTimeOk = TimeOnly.TryParseExact(day.StartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime);
            if (!startTimeOk)
            {
                exception.WithDetail($"{fieldPrefix}.startTime", "El horario de inicio debe tener formato HH:mm.");
            }

            var endTimeOk = TimeOnly.TryParseExact(day.EndTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endTime);
            if (!endTimeOk)
            {
                exception.WithDetail($"{fieldPrefix}.endTime", "El horario de fin debe tener formato HH:mm.");
            }

            if (startTimeOk && endTimeOk && startTime >= endTime)
            {
                exception.WithDetail($"{fieldPrefix}.startTime", "startTime debe ser menor a endTime.");
            }

            if (startTimeOk && endTimeOk && TryParseDay(day.Day, out dayOfWeek))
            {
                normalizedDays.Add(new NormalizedDayRequest(dayOfWeek, startTime, endTime));
            }

            index++;
        }

        if (exception.Error.Details.Count != 0)
        {
            throw exception;
        }

        return normalizedDays;
    }

    private static void ValidateOverlaps(IEnumerable<AvailabilityRule> existingRules, IReadOnlyCollection<NormalizedDayRequest> normalizedDays)
    {
        var allRules = existingRules
            .Select(rule => new NormalizedDayRequest(rule.DayOfWeek, rule.StartTime, rule.EndTime))
            .Concat(normalizedDays)
            .Distinct()
            .ToList();

        var overlap = allRules
            .GroupBy(rule => rule.DayOfWeek)
            .SelectMany(group =>
            {
                var ordered = group.OrderBy(item => item.StartTime).ToList();
                for (var i = 1; i < ordered.Count; i++)
                {
                    if (ordered[i].StartTime < ordered[i - 1].EndTime)
                    {
                        return [ordered[i]];
                    }
                }

                return Enumerable.Empty<NormalizedDayRequest>();
            })
            .Any();

        if (overlap)
        {
            throw new ConflictException(AvailabilityOverlapErrorCode, AvailabilityOverlapMessage);
        }
    }

    private static IEnumerable<DateOnly> GetDatesForDay(DayOfWeek dayOfWeek, DateOnly startDate, DateOnly endDate)
    {
        var current = startDate;
        while (current.DayOfWeek != dayOfWeek && current <= endDate)
        {
            current = current.AddDays(1);
        }

        while (current <= endDate)
        {
            yield return current;
            current = current.AddDays(7);
        }
    }

    private static IEnumerable<(TimeOnly StartTime, TimeOnly EndTime)> BuildIntervals(TimeOnly startTime, TimeOnly endTime)
    {
        var cursor = startTime;
        while (cursor < endTime)
        {
            var next = cursor.AddMinutes(30);
            if (next > endTime)
            {
                yield break;
            }

            yield return (cursor, next);
            cursor = next;
        }
    }

    private static bool TryParseDay(string input, out DayOfWeek dayOfWeek)
    {
        return SupportedDays.TryGetValue(input.Trim(), out dayOfWeek);
    }

    private static string ToSpanishDay(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "LUNES",
            DayOfWeek.Tuesday => "MARTES",
            DayOfWeek.Wednesday => "MIÉRCOLES",
            DayOfWeek.Thursday => "JUEVES",
            DayOfWeek.Friday => "VIERNES",
            DayOfWeek.Saturday => "SÁBADO",
            DayOfWeek.Sunday => "DOMINGO",
            _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek), dayOfWeek, null),
        };
    }

    private static int ToSortOrder(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            DayOfWeek.Sunday => 7,
            _ => 8,
        };
    }

    private static MonthContext BuildMonthContext()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var monthStartDate = new DateOnly(today.Year, today.Month, 1);
        var endDate = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        return new MonthContext(today.Year, today.Month, monthStartDate, today, endDate);
    }

    private sealed record NormalizedDayRequest(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    private sealed record MonthContext(int Year, int Month, DateOnly MonthStartDate, DateOnly StartDate, DateOnly EndDate);
}
