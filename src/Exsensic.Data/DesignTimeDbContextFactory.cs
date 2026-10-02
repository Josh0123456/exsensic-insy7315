using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Exsensic.Data;

/// Design-time factory so the EF Core tools can create the context without starting the API.
/// Reads the connection string from the startup project's config.

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ExsensicDbContext>
{
    public ExsensicDbContext CreateDbContext(string[] args)
    {
        var apiProjectPath = FindApiProjectPath();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(apiProjectPath, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(apiProjectPath, "appsettings.Development.json"), optional: true)
            .AddUserSecrets<DesignTimeDbContextFactory>()
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ExsensicDbContext>();
        var connectionString = configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Default is missing from config.");

        optionsBuilder.UseSqlServer(connectionString);

        return new ExsensicDbContext(optionsBuilder.Options);
    }

    private static string FindApiProjectPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Exsensic.Api");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Couldn't find src/Exsensic.Api. Make sure the repo layout matches the contract.");
    }
}
