using Exsensic.Api.Errors;
using Exsensic.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Hosting (Josh): telemetry, proxy headers, health checks, Kestrel settings.
builder.Services.AddExsensicHosting(builder.Configuration);

// Error handling (Daniel): every error becomes ProblemDetails with "code" and "traceId".
builder.Services.AddExsensicErrorHandling();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Hosting (Josh): must run first so later middleware sees the real scheme and client IP.
app.UseExsensicHosting();

// Error handling (Daniel): straight after hosting, so it catches exceptions from everything below.
app.UseExsensicErrorHandling();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
