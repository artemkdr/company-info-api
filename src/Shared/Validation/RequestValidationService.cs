using FluentValidation;

namespace CompanyInfo.Api.Shared.Validation;

/// <summary>
/// Validates request models using registered FluentValidation validators.
/// </summary>
public interface IRequestValidationService
{
    /// <summary>
    /// Validates the request and throws <see cref="ValidationException"/> on failure.
    /// </summary>
    /// <typeparam name="TRequest">The request model type.</typeparam>
    /// <param name="request">The request to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ValidateAndThrowAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default
    )
        where TRequest : class;
}

/// <summary>
/// Default implementation of <see cref="IRequestValidationService"/>.
/// </summary>
public class RequestValidationService : IRequestValidationService
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestValidationService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve validators.</param>
    public RequestValidationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async Task ValidateAndThrowAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default
    )
        where TRequest : class
    {
        if (request is null)
        {
            throw new ValidationException("Request is required.");
        }

        var validator = _serviceProvider.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            throw new InvalidOperationException(
                $"No validator is registered for request type {typeof(TRequest).Name}."
            );
        }

        await validator.ValidateAndThrowAsync(request, cancellationToken);
    }
}
