using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CompanyInfo.Api.Tests.Architecture;

/// <summary>
/// Architecture tests to ensure proper layering and loose coupling.
/// Tests the following rules:
/// 1. All namespaces start with the root namespace prefix
/// 2. Application, and Application.Features layers exist
/// 3. Features inside Applications should not depend on each other
/// 4. Shared should not depend on Application
/// 5. Folder path must start with namespace (ensures physical structure matches logical structure)
///    - Example: Application/Features/Filters/In/InFilter.cs can have namespace CompanyInfo.Api.Application.Features.Filters.In or CompanyInfo.Api.Application.Features.Filters
/// </summary>
public class ArchitectureTests
{
    private const string RootNamespace = "CompanyInfo.Api";

    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(typeof(Program).Assembly)
        .Build();

    // Use ResideInNamespaceMatching (regex) to include sub-namespaces.
    // ResideInNamespace does exact match only and would miss sub-namespaces.
    private static readonly IObjectProvider<IType> SharedLayer = Types()
        .That()
        .ResideInNamespaceMatching($@"{Regex.Escape(RootNamespace)}\.Shared($|\..*)")
        .As("Shared Layer");

    private static readonly IObjectProvider<IType> ApplicationLayer = Types()
        .That()
        .ResideInNamespaceMatching($@"{Regex.Escape(RootNamespace)}\.Application($|\..*)")
        .As("Application Layer");

    [Fact(
        DisplayName = "All namespaces should start with root namespace prefix (except Program.cs)"
    )]
    public void AllNamespaces_ShouldStartWithRootNamespacePrefix()
    {
        // Empty namespace is only allowed in Program.cs
        var violations = Architecture
            .Namespaces.Where(ns =>
                !ns.FullName.StartsWith(RootNamespace)
                && !(string.IsNullOrEmpty(ns.FullName) && ns.Types.Any(t => t.Name == "Program"))
            )
            .Select(ns => ns.FullName)
            .ToList();

        // We only care about project namespaces, not third-party ones.
        // Filter to namespaces that contain types from the main assembly.
        var projectViolations = violations
            .Where(ns =>
                Architecture.Types.Any(t =>
                    t.Namespace.FullName == ns && t.Assembly.FullName.Contains(RootNamespace)
                )
            )
            .ToList();

        Assert.True(
            projectViolations.Count == 0,
            $"The following namespaces do not start with '{RootNamespace}':\n"
                + string.Join("\n", projectViolations.Select(ns => $"  {ns}"))
        );
    }

    [Fact(DisplayName = "Application layer should exist")]
    public void ApplicationLayer_ShouldExist()
    {
        var applicationTypes = Architecture
            .Types.Where(t => t.Namespace.FullName.StartsWith($"{RootNamespace}.Application"))
            .ToList();

        Assert.True(
            applicationTypes.Count > 0,
            $"Expected types in '{RootNamespace}.Application' namespace but found none"
        );
    }

    [Fact(DisplayName = "Application.Features layer should exist")]
    public void ApplicationFeaturesLayer_ShouldExist()
    {
        var featureTypes = Architecture
            .Types.Where(t =>
                t.Namespace.FullName.StartsWith($"{RootNamespace}.Application.Features")
            )
            .ToList();

        Assert.True(
            featureTypes.Count > 0,
            $"Expected types in '{RootNamespace}.Application.Features' namespace but found none"
        );
    }

    [Fact(DisplayName = "Features sibling folders should not depend on each other")]
    public void FeaturesSiblingFolders_ShouldNotDependOnEachOther()
    {
        // At every level of the Features hierarchy, sibling folders must be independent.
        // A child can depend on its own ancestors, but NOT on siblings or their descendants.
        // Example:
        // - Features
        //      - Basic
        //        - Dossiers (cannot depend on Garages, Users, etc.)
        //        - Garages  (cannot depend on Dossiers, Users, etc.)

        var featuresRoot = $"{RootNamespace}.Application.Features";

        // Collect all namespaces under Features and build a parent→children map at every level
        var allFeatureNamespaces = Architecture
            .Namespaces.Where(ns => ns.FullName.StartsWith(featuresRoot + "."))
            .Select(ns => ns.FullName)
            .ToList();

        var parentToChildren = new Dictionary<string, HashSet<string>>();

        foreach (var ns in allFeatureNamespaces)
        {
            var segments = ns[(featuresRoot.Length + 1)..].Split('.');
            var current = featuresRoot;
            foreach (var segment in segments)
            {
                if (!parentToChildren.ContainsKey(current))
                    parentToChildren[current] = new HashSet<string>();
                parentToChildren[current].Add(segment);
                current = current + "." + segment;
            }
        }

        // For each parent with 2+ children, assert siblings are independent
        foreach (var (parent, children) in parentToChildren)
        {
            var childList = children.ToList();
            if (childList.Count <= 1)
                continue;

            foreach (var child in childList)
            {
                foreach (var sibling in childList.Where(c => c != child))
                {
                    var childFullNs = parent + "." + child;
                    var siblingFullNs = parent + "." + sibling;

                    var childTypes = Types()
                        .That()
                        .ResideInNamespaceMatching($@"{Regex.Escape(childFullNs)}($|\..*)")
                        .As($"'{child}' (under {parent.Split('.').Last()})");

                    var siblingTypes = Types()
                        .That()
                        .ResideInNamespaceMatching($@"{Regex.Escape(siblingFullNs)}($|\..*)")
                        .As($"'{sibling}' (under {parent.Split('.').Last()})");

                    var rule = Types()
                        .That()
                        .Are(childTypes)
                        .Should()
                        .NotDependOnAny(siblingTypes)
                        .Because(
                            $"'{child}' should not depend on sibling '{sibling}' under '{parent.Split('.').Last()}'"
                        )
                        .WithoutRequiringPositiveResults();

                    rule.Check(Architecture);
                }
            }
        }
    }

    [Fact(DisplayName = "Shared should not depend on Application")]
    public void Shared_ShouldNotDependOnApplication()
    {
        var rule = Types()
            .That()
            .Are(SharedLayer)
            .Should()
            .NotDependOnAny(ApplicationLayer)
            .Because("Shared layer should not depend on Application layer")
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact(DisplayName = "Folder path must start with namespace")]
    public void FolderPath_ShouldStartWithNamespace()
    {
        // Ensure that the physical folder structure matches the namespace structure.
        // The namespace (minus root prefix) must be a prefix of the file's directory path.
        // Example: Application/Features/Filters/In/InFilter.cs can have namespace
        // "CompanyInfo.Api.Application.Features.Filters" or
        // "CompanyInfo.Api.Application.Features.Filters.In"

        var srcDir = AppContext.BaseDirectory;
        // find src directory by going up from current test assembly location, then down to src/
        while (!Directory.Exists(Path.Combine(srcDir, "src")))
        {
            srcDir =
                Directory.GetParent(srcDir)?.FullName
                ?? throw new Exception("Could not find src directory");
        }
        srcDir = Path.Combine(srcDir, "src");

        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(srcDir, file);

            // Skip generated / build output folders
            if (
                relativePath.StartsWith("obj", StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith("bin", StringComparison.OrdinalIgnoreCase)
            )
                continue;

            var content = File.ReadAllText(file);
            var nsMatch = Regex.Match(content, @"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline);
            if (!nsMatch.Success)
                continue;

            var ns = nsMatch.Groups[1].Value;
            if (!ns.StartsWith(RootNamespace + "."))
                continue;

            // Convert namespace suffix to a directory path fragment
            var nsPath = ns[(RootNamespace.Length + 1)..].Replace('.', Path.DirectorySeparatorChar);

            // The file's directory (relative to src/) must start with that path
            var fileDir = Path.GetDirectoryName(relativePath) ?? "";

            if (!fileDir.StartsWith(nsPath, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(
                    $"  {relativePath} → namespace {ns} (expected dir prefix: {nsPath})"
                );
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Files whose folder path does not start with their namespace:\n"
                + string.Join("\n", violations)
        );
    }
}
