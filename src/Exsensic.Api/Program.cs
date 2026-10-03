using Exsensic.Api.Errors;
using Exsensic.Api.Hosting;
using Exsensic.Api.Security;
using Exsensic.Core.Catalog;
using Exsensic.Data;

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

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Hosting (Josh): apply migrations and seed before the API starts taking requests.
await app.InitialiseExsensicDatabaseAsync();

// Hosting (Josh): must run first so later middleware sees the real scheme and client IP.
app.UseExsensicHosting();

// Error handling (Daniel): straight after hosting, so it catches exceptions from everything below.
app.UseExsensicErrorHandling();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();
