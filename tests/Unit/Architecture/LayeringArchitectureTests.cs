using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Luna.UnitTests.Architecture;

public sealed class LayeringArchitectureTests
{
    private static readonly string[] LayeredServices =
    [
        "Catalog",
        "Inventory",
        "Orders",
        "Payments",
        "Shipping"
    ];

    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Api", "Contracts"];

    private static string AssemblyNameFor(string service, string layer) => $"{service}.{layer}";

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

    [Theory]
    [MemberData(nameof(LayerProjectsData))]
    public void Every_layer_project_exists(string service, string layer)
    {
        var assemblyName = AssemblyNameFor(service, layer);

        LoadAssembly(assemblyName).Should().NotBeNull(
            $"{assemblyName} is part of the five-project service template for {service} and must exist");
    }

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
