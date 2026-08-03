using System.Globalization;
using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Enums;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly IPersistence _persistence;
    private readonly IHolidayProvider _holidayProvider;

    public AvailabilityService(IPersistence persistence, IHolidayProvider holidayProvider)
    {
        _persistence = persistence;
        _holidayProvider = holidayProvider;
    }

    public async Task<IReadOnlyCollection<AvailabilityModel.Response>> Create(AvailabilityModel.Request request)
    {
        var context = DateTimeHelper.GetCurrentMonthRangeFromToday();
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
        var context = DateTimeHelper.GetCurrentMonthRangeFromToday();
        var normalizedDays = NormalizeRequest(request);
        ValidateOverlaps([], normalizedDays);

        var doctor = await GetDoctor(request.DoctorId);
        var existingRules = (await _persistence.GetFiltered<AvailabilityRule>(
            rule => rule.DoctorId == doctor.Id && rule.Year == context.Year && rule.Month == context.Month))
            ?.ToList() ?? [];

        var existingSlots = (await _persistence.GetFiltered<AvailabilitySlot>(
            slot => slot.DoctorId == doctor.Id &&
                slot.SlotDate >= context.StartDate &&
                slot.SlotDate <= context.EndDate))
            ?.ToList() ?? [];

        var slotsToDelete = existingSlots
            .Where(slot => slot.Status != SlotStatus.Booked)
            .ToList();

        foreach (var slot in slotsToDelete)
        {
            slot.Delete();
        }
        if (slotsToDelete.Count != 0)
        {
            await _persistence.UpdateRange(slotsToDelete);
        }

        foreach (var rule in existingRules)
        {
            rule.Delete();
        }
        if (existingRules.Count != 0)
        {
            await _persistence.UpdateRange(existingRules);
        }

        var createdRules = new List<AvailabilityRule>();
        foreach (var day in normalizedDays)
        {
            var rule = new AvailabilityRule(doctor, context.Month, context.Year, day.DayOfWeek, day.StartTime, day.EndTime);
            createdRules.Add(rule);
        }
        if (createdRules.Count != 0)
        {
            await _persistence.AddRange(createdRules);
        }

        foreach (var day in normalizedDays)
        {
            var rule = createdRules.First(createdRule =>
                createdRule.DayOfWeek == day.DayOfWeek &&
                createdRule.StartTime == day.StartTime &&
                createdRule.EndTime == day.EndTime);

            await EnsureSlotsExist(doctor, rule, day, context);
        }

        return MapResponses(createdRules);
    }

    public async Task<IReadOnlyCollection<AvailabilityModel.Response>> GetByDoctor(Guid doctorId)
    {
        _ = await GetDoctor(doctorId);
        var context = DateTimeHelper.GetCurrentMonthRangeFromToday();

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

    private async Task EnsureSlotsExist(Doctor doctor, AvailabilityRule rule, NormalizedDayRequest day, MonthRange context)
    {
        var monthSlots = (await _persistence.GetFiltered<AvailabilitySlot>(
            slot => slot.DoctorId == doctor.Id &&
                slot.SlotDate >= context.StartDate &&
                slot.SlotDate <= context.EndDate))
            ?.ToList() ?? [];
        var newSlots = new List<AvailabilitySlot>();

        var dates = DateTimeHelper.GetDatesForDay(day.DayOfWeek, context.StartDate, context.EndDate)
            .Where(date => !_holidayProvider.IsHoliday(date))
            .ToList();
        var intervals = DateTimeHelper.BuildThirtyMinuteIntervals(day.StartTime, day.EndTime);

        foreach (var date in dates)
        {
            foreach (var interval in intervals)
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
                newSlots.Add(slot);
                monthSlots.Add(slot);
            }
        }

        if (newSlots.Count != 0)
        {
            await _persistence.AddRange(newSlots);
        }
    }

    private static IReadOnlyCollection<AvailabilityModel.Response> MapResponses(IEnumerable<AvailabilityRule> rules)
    {
        return rules
            .OrderBy(rule => ToSortOrder(rule.DayOfWeek))
            .ThenBy(rule => rule.StartTime)
            .Select(rule => new AvailabilityModel.Response(
                rule.Id,
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

        if (request.Days.Count == 0)
        {
            exception.WithDetail(nameof(request.Days), "days debe contener al menos un día.");
            throw exception;
        }

        var normalizedDays = new List<NormalizedDayRequest>();
        var index = 0;
        foreach (var day in request.Days)
        {
            var fieldPrefix = $"{nameof(request.Days)}[{index}]";

            if (!DateTimeHelper.TryParseSupportedDay(day.Day, out var dayOfWeek))
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

            if (startTimeOk && endTimeOk && DateTimeHelper.TryParseSupportedDay(day.Day, out dayOfWeek))
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
            throw new ConflictException(
                nameof(ErrorCodes.AVAILABILITY_OVERLAP),
                ErrorCodes.AVAILABILITY_OVERLAP);
        }
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

    private sealed record NormalizedDayRequest(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
}
