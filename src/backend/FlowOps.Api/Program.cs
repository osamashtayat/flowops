using FlowOps.Api;
using FlowOps.Api.Auth;
using FlowOps.Api.Errors;
using FlowOps.Api.OpenApi;
using FlowOps.Application;
using FlowOps.Application.Common;
using FlowOps.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

var builder = WebApplication.CreateBuilder(args);

// Structured logs: the built-in JSON console formatter (no Serilog in Phase 1, ARCHITECTURE.md §6).
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

// Validation errors use JSON (camelCase) field paths, e.g. estimatedCost.amount (API_CONTRACT.md §2).
builder.Services
    .AddControllers(options =>
    {
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        options.Filters.Add<ValidateAntiforgeryFilter>();
    })
    .AddJsonOptions(options => JsonSetup.Configure(options.JsonSerializerOptions));
// The OpenAPI generator reads the HTTP (minimal API) options, so both must agree with MVC.
builder.Services.ConfigureHttpJsonOptions(options => JsonSetup.Configure(options.SerializerOptions));
builder.Services.AddFlowOpsProblemDetails();
builder.Services.AddFlowOpsOpenApi();
builder.Services.AddFlowOpsAuthorization();
builder.Services.AddFlowOpsLoginRateLimiting(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddApplication();
builder.Services.AddOptions<CompanyOptions>()
    .Bind(builder.Configuration.GetSection(CompanyOptions.SectionName))
    .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.CompanyTimeZone, out _), "FlowOps:CompanyTimeZone is not a known time zone.")
    .ValidateOnStart();
// Composition root only: no controller uses Infrastructure types (ARCHITECTURE.md §3).
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseFlowOpsSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<PasswordChangeGate>();
app.UseAuthorization();

app.MapControllers();

app.Run();
