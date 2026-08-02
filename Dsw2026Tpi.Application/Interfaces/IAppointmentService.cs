using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IAppointmentService
{
    Task<AppointmentModel.Response> Create(AppointmentModel.Request request, string authenticatedEmail);
    Task<IReadOnlyCollection<AppointmentModel.Response>> GetByPatient(long dni, string authenticatedEmail);
    Task Cancel(Guid id, string authenticatedEmail);
    Task<Pagination<AppointmentModel.AdminSummary>> GetByDate(DateOnly? date, int pageSize, int pageIndex);
    Task<Pagination<AppointmentModel.AdminSummary>> Search(
        int pageSize,
        int pageIndex,
        Guid? specialtyId,
        Guid? doctorId,
        long? dni,
        DateOnly? date);
    Task Attend(Guid id);
    Task MarkNoShow(Guid id);
}
