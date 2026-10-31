using Exsensic.Api.Errors;
using Exsensic.Api.Hosting;
using Exsensic.Api.OpenApi;
using Exsensic.Api.Security;
using Exsensic.Core.Catalog;
using Exsensic.Data;
using Exsensic.Api.Bookings;

var builder = WebApplication.CreateBuilder(args);

// Hosting (Josh): telemetry, proxy headers, health checks, Kestrel settings.
builder.Services.AddExsensicHosting(builder.Configuration);

// Data: the EF Core DbContext on SQL Server (ConnectionStrings:Default).
builder.Services.AddExsensicData(builder.Configuration);

// Authentication: Identity, JWT bearer tokens, role policies and rate limits.
builder.Services.AddExsensicAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddExsensicRateLimiting();

// Catalogue: services and slot availability (Core business logic).
builder.Services.AddScoped<ServiceCatalogService>();

// Error handling (Daniel): every error becomes ProblemDetails with "code" and "traceId".
builder.Services.AddExsensicErrorHandling();

// Bookings (Daniel): booking services, observers (history and notifications) and the user directory.
builder.Services.AddExsensicBookingApi(builder.Configuration);

builder.Services.AddControllers();
// OpenAPI (Daniel): the document is generated from the real controllers and published as docs/openapi.json.
builder.Services.AddExsensicOpenApi();

var app = builder.Build();

// Hosting (Josh): apply migrations and seed before the API starts taking requests.
await app.InitialiseExsensicDatabaseAsync();

// Hosting (Josh): must run first so later middleware sees the real scheme and client IP.
app.UseExsensicHosting();

// Error handling (Daniel): straight after hosting, so it catches exceptions from everything below.
app.UseExsensicErrorHandling();

// The live /openapi/v1.json endpoint exists in Development only. Everywhere else the spec is the committed
// docs/openapi.json (docs/openapi.md), so no deployed API serves it anonymously.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();
