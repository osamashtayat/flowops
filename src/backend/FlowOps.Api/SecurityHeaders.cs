namespace FlowOps.Api;

/// <summary>AUTHORIZATION.md §1.5: a strict Content-Security-Policy outside Development (the Vite dev server needs inline scripts).</summary>
public static class SecurityHeaders
{
    public const string ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseFlowOpsSecurityHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Environment.IsDevelopment())
        {
            return app;
        }

        return app.Use((context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;
            return next(context);
        });
    }
}
