using Exsensic.Data;
using Exsensic.Data.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Exsensic.Api.Hosting;

/// <summary>
/// Everything the API needs to run behind Azure App Service: telemetry, trusted proxy
/// headers, HTTPS enforcement and health endpoints. Program.cs calls these two methods
/// with one line each, so hosting code stays in one place (docs/CONTRACTS.md §8).
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Registers the hosting services: Application Insights, forwarded headers, health checks,
    /// and a Kestrel setting that hides the server name.
    /// </summary>
    public static IServiceCollection AddExsensicHosting(this IServiceCollection services, IConfiguration configuration)
    {
        // Application Insights only runs when a connection string is configured. App Service sets
        // APPLICATIONINSIGHTS_CONNECTION_STRING when monitoring is enabled; locally it stays off.
        if (!string.IsNullOrWhiteSpace(configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            services.AddApplicationInsightsTelemetry();
        }

        // App Service terminates HTTPS at its front end and forwards the request over HTTP with
        // X-Forwarded-For (client IP) and X-Forwarded-Proto (https). Trusting these headers lets
        // HTTPS redirection and rate limiting see the real scheme and client IP.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // The front end's address is not fixed, so the default "loopback only" list would ignore
            // the headers. ForwardLimit stays at 1: only the last hop, which App Service appends
            // itself, is used, so a client cannot spoof its IP by sending its own X-Forwarded-For.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // Behind App Service the app itself only listens on HTTP, so the redirect middleware cannot
        // discover the HTTPS port; tell it the public one. (Only used outside Development.)
        services.AddHttpsRedirection(options => options.HttpsPort = 443);

        // Liveness has no checks (the process answers). Readiness (tag "ready") also proves the
        // API can reach its database, so /health/ready turns 503 if Azure SQL is unreachable.
        services.AddHealthChecks()
            .AddDbContextCheck<ExsensicDbContext>("database", tags: ["ready"]);

        // Do not advertise "Server: Kestrel" to attackers.
        services.Configure<KestrelServerOptions>(options => options.AddServerHeader = false);

        return services;
    }

    /// <summary>
    /// Adds the hosting middleware at the start of the pipeline and maps the health endpoints.
    /// Call this first, straight after builder.Build(), so every later middleware sees the real scheme and IP.
    /// </summary>
    public static WebApplication UseExsensicHosting(this WebApplication app)
    {
        app.UseForwardedHeaders();

        if (!app.Environment.IsDevelopment())
        {
            // Tell browsers to use HTTPS only for this site (default 30 days).
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        // Health endpoints sit outside /api/v1 and allow anonymous access, because App Service
        // health checks and the pipeline smoke test call them without signing in (contract §6).
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
            .AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Prepares the database at start-up: applies committed EF Core migrations, ensures the roles
    /// and the first admin, and adds demo data in Development and Staging, or wherever
    /// Database:SeedDemoData is true (production, so the marker has services and accounts) (Dean's DatabaseInitialiser,
    /// docs/CONTRACTS.md §4). Runs when Database:MigrateOnStartup is true (set in Azure) or in
    /// Development. If it fails the API stops, so a broken schema never serves traffic.
    /// </summary>
    public static async Task InitialiseExsensicDatabaseAsync(this WebApplication app)
    {
        var migrateOnStartup = app.Configuration.GetValue<bool>("Database:MigrateOnStartup");
        if (!migrateOnStartup && !app.Environment.IsDevelopment())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
        {
            // Lets a teammate run the API before setting up user-secrets; Azure always has the setting.
            app.Logger.LogWarning("ConnectionStrings:Default is not set, so the database was not initialised.");
            return;
        }

        // Production is the live demo the marker uses, so it can opt in with Database:SeedDemoData=true.
        var seedDemoData = app.Configuration.GetValue<bool?>("Database:SeedDemoData")
            ?? (app.Environment.IsDevelopment() || app.Environment.IsStaging());
        app.Logger.LogInformation("Initialising the database (demo data: {SeedDemoData}).", seedDemoData);
        await DatabaseInitialiser.InitialiseAsync(app.Services, seedDemoData, app.Lifetime.ApplicationStopping);
    }
}
