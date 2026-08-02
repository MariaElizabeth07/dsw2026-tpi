using System;
using System.Collections.Generic;
using System.Text;
using Dsw2026Tpi.CrossCutting.Exceptions;

namespace Dsw2026Tpi.CrossCutting.Helpers;

public static class PaginationValidator
{
    public static void Validate(int pageSize, int pageIndex)
    {
        var exception = new ValidationException();

        if (pageSize <= 0)
        {
            exception.WithDetail(nameof(pageSize), "El pageSize debe ser mayor a 0.");
        }

        if (pageIndex < 0)
        {
            exception.WithDetail(nameof(pageIndex), "El pageIndex debe ser mayor o igual a 0.");
        }

        if (exception.Error.Details.Count != 0)
        {
            throw exception;
        }
    }
}