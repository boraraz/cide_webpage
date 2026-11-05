using Microsoft.AspNetCore.Antiforgery;

namespace Commerce.Api.Auth;

internal sealed class RequireAntiforgery : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>()
                .ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                title: "The request could not be validated.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}
