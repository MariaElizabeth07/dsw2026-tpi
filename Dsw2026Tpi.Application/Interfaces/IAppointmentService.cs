using System;
using System.Collections.Generic;
using System.Text;

using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IAppointmentService
{
    Task<AppointmentModel.Response> Create(AppointmentModel.Request request, string authenticatedEmail);
    Task<IReadOnlyCollection<AppointmentModel.Response>> GetByPatient(long dni, string authenticatedEmail);
    Task Cancel(Guid id, string authenticatedEmail);
}
