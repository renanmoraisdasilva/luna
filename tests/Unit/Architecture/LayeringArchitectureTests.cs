using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Luna.UnitTests.Architecture;

/// <summary>
/// Locks in the backend dependency graph. Every rule here passes today; the point is that a renamed
/// namespace, a stray <c>using</c>, or a new project reference now fails the build instead of silently
/// weakening a boundary that is currently held only by discipline.
///
/// The rules encode the Luna structure rules 1-9 (AGENTS.md) as executable checks: domain projects are
/// self-contained, Application does not depend on the Api host or on EF, Infrastructure does not cross
/// into another service's persistence, and controllers do not reach for a <c>DbContext</c>.
/// </summary>
public sealed class LayeringArchitectureTests
{
    /// <summary>
    /// The five services that follow the five-project template. Identity is deliberately absent: it is a
    /// single project with Application/Controllers/Domain/Infrastructure folders, documented as an
    /// exception in AGENTS.md, so it has no per-layer assemblies to inspect.
    /// </summary>
    private static readonly string[] LayeredServices =
    [
        "Catalog",
        "Inventory",
        "Orders",
        "Payments",
        "Shipping"
    ];

    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Api", "Contracts"];

    /// <summary>
    /// Assembly name for a layer. Note this is <c>{Service}.{Layer}</c>, not <c>Luna.{Service}.{Layer}</c>:
    /// the root namespace is <c>Luna</c> but the assembly name drops it.
    /// </summary>
    private static string AssemblyNameFor(string service, string layer) => $"{service}.{layer}";

    /// <summary>Every assembly in the Luna solution that this file knows how to reason about.</summary>
    private static string[] AllLunaAssemblyNames() =>
        [.. LayeredServices.SelectMany(service => Layers.Select(layer => AssemblyNameFor(service, layer))),
          "Identity",
          "Luna.Authentication",
          "Luna.Contracts",
          "Luna.Observability"];

    public static TheoryData<string> LayeredServicesData()
    {
        var data = new TheoryData<string>();
        foreach (var service in LayeredServices)
        {
            data.Add(service);
        }

        return data;
    }

    public static TheoryData<string, string> LayerProjectsData()
    {
        var data = new TheoryData<string, string>();
        foreach (var service in LayeredServices)
        {
            foreach (var layer in Layers)
            {
                data.Add(service, layer);
            }
        }

        return data;
    }

    /// <summary>
    /// Fails if a whole layer is deleted. Without this, a removed project would leave every rule below
    /// vacuously satisfied because the assembly it inspects no longer exists.
    /// </summary>
    [Theory]
    [MemberData(nameof(LayerProjectsData))]
    public void Every_layer_project_exists(string service, string layer)
    {
        var assemblyName = AssemblyNameFor(service, layer);

        LoadAssembly(assemblyName).Should().NotBeNull(
            $"{assemblyName} is part of the five-project service template for {service} and must exist");
    }

    /// <summary>Rule 3 in AGENTS.md: a domain project contains business entities and rules, and nothing else.</summary>
    [Theory]
    [MemberData(nameof(LayeredServicesData))]
    public void Domain_projects_reference_no_other_luna_project(string service)
    {
        var domainAssemblyName = AssemblyNameFor(service, "Domain");
        var lunaAssemblies = AllLunaAssemblyNames().ToHashSet(StringComparer.Ordinal);
        var forbidden = LoadAssembly(domainAssemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(lunaAssemblies.Contains)
            .Where(name => name != domainAssemblyName)
            .ToArray();

        forbidden.Should().BeEmpty(
            $"{domainAssemblyName} must stand alone; it currently references {string.Join(", ", forbidden)}");
    }

    /// <summary>
    /// Rule 2, second half: nothing in a domain namespace may reach for EF Core, HTTP, ASP.NET Core, or
    /// an Infrastructure namespace. The domain has to stay testable without a database or a host.
    /// </summary>
    [Theory]
    [MemberData(nameof(LayeredServicesData))]
    public void Domain_types_do_not_reference_infrastructure_concerns(string service)
    {
        var result = Types.InAssembly(LoadAssembly(AssemblyNameFor(service, "Domain")))
            .That().ResideInNamespace($"Luna.{service}.Domain")
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "System.Net.Http",
                "Microsoft.AspNetCore",
                "Luna.Contracts",
                $"Luna.{service}.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Luna.{service}.Domain must not depend on persistence, HTTP, or the web framework:{Environment.NewLine}{DescribeFailures(result)}");
    }

    /// <summary>
    /// Dependency direction inside a service. Application coordinates use cases over Domain and Contracts;
    /// it may not reach upward into the Api host or downward into EF.
    /// </summary>
    [Theory]
    [MemberData(nameof(LayeredServicesData))]
    public void Application_does_not_depend_on_the_api_host_or_infrastructure(string service)
    {
        var applicationAssemblyName = AssemblyNameFor(service, "Application");
        var forbidden = LoadAssembly(applicationAssemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.EndsWith(".Api", StringComparison.Ordinal) ||
                           name == AssemblyNameFor(service, "Infrastructure"))
            .ToArray();

        forbidden.Should().BeEmpty(
            $"{applicationAssemblyName} must orchestrate use cases, not host HTTP or persistence; it currently references {string.Join(", ", forbidden)}");
    }

    /// <summary>
    /// Rule 8 in AGENTS.md, plus bounded-context ownership: Infrastructure owns persistence and external
    /// integration for its own service. It may consume another service's Contracts, never its database.
    /// </summary>
    [Theory]
    [MemberData(nameof(LayeredServicesData))]
    public void Infrastructure_does_not_reference_another_services_infrastructure(string service)
    {
        var infrastructureAssemblyName = AssemblyNameFor(service, "Infrastructure");
        var foreign = LoadAssembly(infrastructureAssemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.EndsWith(".Infrastructure", StringComparison.Ordinal) &&
                           name != infrastructureAssemblyName)
            .ToArray();

        foreign.Should().BeEmpty(
            $"{infrastructureAssemblyName} must not depend on another service's persistence; it currently references {string.Join(", ", foreign)}");
    }

    /// <summary>
    /// Rules 7 and 9 in AGENTS.md: controllers are thin and Application handlers own orchestration. A
    /// controller that names a <c>DbContext</c> or an Infrastructure repository has skipped the
    /// Application layer entirely, which is the defect Wave 3.7 exists to remove from
    /// <c>AccountController</c> and <c>OrdersController</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(LayeredServicesData))]
    public void Controllers_do_not_reference_a_dbcontext_or_infrastructure_persistence(string service)
    {
        var apiAssemblyName = AssemblyNameFor(service, "Api");
        var controllerTypes = Types.InAssembly(LoadAssembly(apiAssemblyName))
            .That().HaveNameEndingWith("Controller")
            .GetTypes()
            .ToArray();

        controllerTypes.Should().NotBeEmpty(
            $"{apiAssemblyName} is expected to expose controllers; an empty set would make this rule vacuous");

        var result = Types.InAssembly(LoadAssembly(apiAssemblyName))
            .That().HaveNameEndingWith("Controller")
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                $"Luna.{service}.Infrastructure.Repositories",
                $"Luna.{service}.Infrastructure.Database")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Luna.{service} controllers must go through Application rather than persistence:{Environment.NewLine}{DescribeFailures(result)}");
    }

    private static string DescribeFailures(TestResult result) =>
        result.FailingTypeNames is null || result.FailingTypeNames.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, result.FailingTypeNames);

    /// <summary>
    /// Resolves a project assembly by name. Probing the test output directory is the reliable path here:
    /// the layered projects are referenced only so these rules can inspect them, so nothing in the test
    /// code path forces the runtime to load them eagerly.
    /// </summary>
    private static Assembly LoadAssembly(string assemblyName)
    {
        var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => candidate.GetName().Name == assemblyName);

        if (alreadyLoaded is not null)
        {
            return alreadyLoaded;
        }

        var candidatePath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");
        if (File.Exists(candidatePath))
        {
            return Assembly.LoadFrom(candidatePath);
        }

        return Assembly.Load(assemblyName);
    }
}
