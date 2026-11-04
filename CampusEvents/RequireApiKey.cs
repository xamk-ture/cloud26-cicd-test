namespace CampusEvents;

/// <summary>
/// Endpoint filter: admin operations require the X-Api-Key header.
/// The expected key comes from configuration — in Azure via a Key Vault
/// reference, locally from appsettings.Development.json.
/// </summary>
public class RequireApiKey : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["AdminApiKey"];

        if (string.IsNullOrEmpty(expected))
        {
            // Fail CLOSED: no configured key means nobody gets in —
            // never "no key configured, so let everyone in".
            return Results.Problem(
                "AdminApiKey is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var provided = context.HttpContext.Request.Headers["X-Api-Key"].ToString();

        if (provided != expected)
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}
