namespace backend.Services.Routing;

public sealed class RoutingAdmissionExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (RoutingAdmissionRejectedException exception)
            when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter =
                exception.RetryAfterSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            await context.Response.WriteAsJsonAsync(new
            {
                error = "ROUTING_BUSY",
                message = exception.Message,
                retryAfterSeconds = exception.RetryAfterSeconds
            });
        }
    }
}
