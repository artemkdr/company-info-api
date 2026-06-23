using System.Reflection;
using CompanyInfo.Api.Shared.Attributes;

namespace CompanyInfo.Api.Shared.Extensions;

/// <summary>
/// Extension methods for automatic service registration via <see cref="RegisterServiceAttribute"/>.
/// </summary>
public static class ServiceRegistrationExtensions
{
    /// <summary>
    /// Scans the specified assembly for classes decorated with <see cref="RegisterServiceAttribute"/>
    /// and registers them in the DI container with the configured lifetime.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <param name="assembly">The assembly to scan for decorated classes.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddServicesFromAssembly(
        this IServiceCollection services,
        Assembly assembly
    )
    {
        var typesWithAttribute = assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Select(t => new
            {
                Type = t,
                Attribute = t.GetCustomAttribute<RegisterServiceAttribute>(),
            })
            .Where(x => x.Attribute != null);

        foreach (var entry in typesWithAttribute)
        {
            var implementationType = entry.Type;
            var attribute = entry.Attribute!;

            if (attribute.ServiceType != null)
            {
                // Register as the explicitly specified interface
                Register(services, attribute.ServiceType, implementationType, attribute.Lifetime);
            }
            else
            {
                // Register as all implemented interfaces
                var interfaces = implementationType.GetInterfaces();
                if (interfaces.Length > 0)
                {
                    foreach (var serviceType in interfaces)
                    {
                        Register(services, serviceType, implementationType, attribute.Lifetime);
                    }
                }
                else
                {
                    // No interfaces — register as self
                    Register(services, implementationType, implementationType, attribute.Lifetime);
                }
            }
        }

        return services;
    }

    private static void Register(
        IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifetime lifetime
    )
    {
        var descriptor = new ServiceDescriptor(serviceType, implementationType, lifetime);
        services.Add(descriptor);
    }
}
