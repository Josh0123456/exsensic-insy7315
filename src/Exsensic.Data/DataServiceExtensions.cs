using Exsensic.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exsensic.Data;

/// <summary>
/// Registers the database for the API. Program.cs calls this with one line, so the Api project
/// never needs to know how the DbContext is configured.
/// </summary>
public static class DataServiceExtensions
{
    /// <summary>
    /// Registers <see cref="ExsensicDbContext"/> on SQL Server using ConnectionStrings:Default
    /// (user-secrets locally, an App Service setting in Azure; docs/CONTRACTS.md §9), and exposes it
    /// to Core through <see cref="IAppDbContext"/> so business logic never references this project.
    /// </summary>
    public static IServiceCollection AddExsensicData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ExsensicDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Default"),
                // Azure SQL can drop connections briefly (including when the free database resumes
                // from auto-pause); retrying transient failures avoids random errors.
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<ExsensicDbContext>());
        return services;
    }
}
