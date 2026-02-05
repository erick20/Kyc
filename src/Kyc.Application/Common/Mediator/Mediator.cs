using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Application.Common.Mediator;

/// <summary>
/// Default mediator implementation that resolves handlers and executes pipeline behaviors.
/// </summary>
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var responseType = typeof(TResponse);

        // Get the handler type: IRequestHandler<TRequest, TResponse>
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);

        // Resolve the handler
        var handler = _serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for {requestType.Name}");

        // Get all pipeline behaviors for this request/response type
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var behaviors = _serviceProvider.GetServices(behaviorType).Cast<object>().ToList();

        // Build the pipeline
        // Start with the innermost handler
        RequestHandlerDelegate<TResponse> handlerDelegate = () =>
        {
            var handleMethod = handlerType.GetMethod("Handle")!;
            var task = (Task<TResponse>)handleMethod.Invoke(handler, new object[] { request, cancellationToken })!;
            return task;
        };

        // Wrap with behaviors in reverse order (so first registered runs first)
        foreach (var behavior in behaviors.AsEnumerable().Reverse())
        {
            var currentDelegate = handlerDelegate;
            var handleMethod = behaviorType.GetMethod("Handle")!;

            handlerDelegate = () =>
            {
                var task = (Task<TResponse>)handleMethod.Invoke(
                    behavior,
                    new object[] { request, currentDelegate, cancellationToken })!;
                return task;
            };
        }

        // Execute the pipeline
        return await handlerDelegate();
    }
}
