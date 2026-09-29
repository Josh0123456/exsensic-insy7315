using System.Xml.Linq;

namespace Exsensic.UnitTests.Architecture;

/// <summary>
/// Enforces the layer rules in docs/CONTRACTS.md §1 by reading each src project file.
/// If someone adds a forbidden reference (for example Web → Core), CI fails with a
/// message naming the project and the reference, before the change can be merged.
/// </summary>
public class ProjectReferenceTests
{
    /// <summary>
    /// The only project references each src project may have, copied from docs/CONTRACTS.md §1.
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        ["Exsensic.Web"] = ["Exsensic.Contracts"],
        ["Exsensic.Api"] = ["Exsensic.Core", "Exsensic.Data", "Exsensic.Contracts"],
        ["Exsensic.Core"] = ["Exsensic.Contracts"],
        ["Exsensic.Data"] = ["Exsensic.Core", "Exsensic.Contracts"],
        ["Exsensic.Contracts"] = [],
    };

    /// <summary>
    /// Supplies one test case per src project so each shows as its own result in the test report.
    /// </summary>
    public static TheoryData<string> SrcProjects() => new(AllowedReferences.Keys);

    /// <summary>
    /// Each src project references exactly the projects the contract allows: no more, no fewer.
    /// </summary>
    [Theory]
    [MemberData(nameof(SrcProjects))]
    public void ProjectReferences_ComparedToContract_MatchExactly(string projectName)
    {
        var actual = ReadProjectReferences(projectName);
        var expected = AllowedReferences[projectName].Order().ToArray();

        Assert.True(
            expected.SequenceEqual(actual),
            $"{projectName} must reference [{string.Join(", ", expected)}] " +
            $"but references [{string.Join(", ", actual)}]. See docs/CONTRACTS.md §1.");
    }

    /// <summary>
    /// The src folder holds exactly the five contract projects, so a new project cannot
    /// be added without also adding its layer rule to this test.
    /// </summary>
    [Fact]
    public void SrcFolder_ComparedToContract_ContainsOnlyContractProjects()
    {
        var actual = Directory
            .GetFiles(Path.Combine(SolutionRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Order()
            .ToArray();
        var expected = AllowedReferences.Keys.Order().ToArray();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Core holds the business rules and must not depend on ASP.NET Core, so the rules
    /// can be tested without a web server (docs/CONTRACTS.md §1).
    /// </summary>
    [Fact]
    public void CoreProject_Dependencies_DoNotIncludeAspNetCore()
    {
        var project = LoadProject("Exsensic.Core");

        var sdk = project.Root?.Attribute("Sdk")?.Value ?? string.Empty;
        var aspNetReferences = project
            .Descendants()
            .Where(e => e.Name.LocalName is "PackageReference" or "FrameworkReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.False(sdk.Contains("Web", StringComparison.OrdinalIgnoreCase), $"Exsensic.Core must not use the {sdk} SDK.");
        Assert.Empty(aspNetReferences);
    }

    /// <summary>
    /// The solution root, found by walking up from the test output folder until Exsensic.sln appears.
    /// Works the same locally and in CI because it does not rely on a fixed path.
    /// </summary>
    private static string SolutionRoot { get; } = FindSolutionRoot();

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Exsensic.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find Exsensic.sln above " + AppContext.BaseDirectory);
    }

    private static XDocument LoadProject(string projectName) =>
        XDocument.Load(Path.Combine(SolutionRoot, "src", projectName, projectName + ".csproj"));

    /// <summary>
    /// Returns the names of the projects referenced by a project file, sorted, for example "Exsensic.Contracts".
    /// </summary>
    private static string[] ReadProjectReferences(string projectName) =>
        LoadProject(projectName)
            .Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', '/')))
            .Order()
            .ToArray();
}
