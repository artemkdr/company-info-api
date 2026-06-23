namespace CompanyInfo.Api.Shared.Attributes;

/// <summary>
/// Base attribute for convention-based service auto-registration.
/// Decorate Application-layer classes (services, repositories, filter strategies)
/// with a concrete lifetime attribute to auto-register them in the DI container.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class RegisterServiceAttribute : Attribute
{
    /// <summary>
    /// The explicit interface type to register as.
    /// When <c>null</c>, the class is registered as all its implemented interfaces
    /// (useful for filter strategies that implement <c>IFilterStrategy&lt;TEntity&gt;</c>).
    /// </summary>
    public Type? ServiceType { get; }

    /// <summary>
    /// The DI lifetime for this service registration.
    /// </summary>
    public virtual ServiceLifetime Lifetime { get; init; }

    /// <summary>
    /// Constructor for the attribute.
    /// </summary>
    /// <param name="lifetime">The DI lifetime for this service registration.</param>
    /// <param name="serviceType">
    /// The explicit interface type to register as, or <c>null</c> to register as all implemented interfaces.
    /// </param>
    public RegisterServiceAttribute(
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        Type? serviceType = null
    )
    {
        ServiceType = serviceType;
        Lifetime = lifetime;
    }
}
