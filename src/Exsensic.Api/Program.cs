using Exsensic.Api.Errors;
using Exsensic.Api.Hosting;
using Exsensic.Api.Security;
using Exsensic.Data;
using Exsensic.Core.Bookings;

var builder = WebApplication.CreateBuilder(args);

// Hosting (Josh): telemetry, proxy headers, health checks, Kestrel settings.
builder.Services.AddExsensicHosting(builder.Configuration);

// Data: the EF Core DbContext on SQL Server (ConnectionStrings:Default).
builder.Services.AddExsensicData(builder.Configuration);

// Authentication: Identity, JWT bearer tokens, role policies and rate limits.
builder.Services.AddExsensicAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddExsensicRateLimiting();

// Error handling (Daniel): every error becomes ProblemDetails with "code" and "traceId".
builder.Services.AddExsensicErrorHandling();

// Bookings (Daniel): status-change observers (history and notifications) and their dispatcher.
builder.Services.AddExsensicBookings();

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
