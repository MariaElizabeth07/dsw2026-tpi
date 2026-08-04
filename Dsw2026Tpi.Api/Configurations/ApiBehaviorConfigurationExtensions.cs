using Dsw2026Tpi.CrossCutting.Models;
using Dsw2026Tpi.CrossCutting.Resources;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2026Tpi.Api.Configurations;

public static class ApiBehaviorConfigurationExtensions
{
    public static IMvcBuilder AddAppApiBehavior(this IMvcBuilder mvcBuilder)
    {
        return mvcBuilder.ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var error = new ErrorResponse(
                    nameof(ErrorCodes.VALIDATION_ERROR),
                    ErrorCodes.VALIDATION_ERROR);

                foreach (var modelError in context.ModelState)
                {
                    foreach (var detail in modelError.Value.Errors)
                    {
                        error.AddDetail(modelError.Key, detail.ErrorMessage);
                    }
                }

                return new BadRequestObjectResult(error);
            };
        });
    }
}
