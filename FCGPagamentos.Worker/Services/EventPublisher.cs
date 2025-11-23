using FCGPagamentos.Worker.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Services;

public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public EventPublisher(ILogger<EventPublisher> logger, ISendEndpointProvider sendEndpointProvider)
    {
        _logger = logger;
        _sendEndpointProvider = sendEndpointProvider;
    }

    public async Task PublishPaymentProcessingAsync(Guid paymentId, Guid correlationId, CancellationToken cancellationToken = default)
    {
        await PublishEventAsync("PaymentProcessing", paymentId, correlationId, null, cancellationToken);
    }

    public async Task PublishPaymentApprovedAsync(Guid paymentId, Guid correlationId, string providerResponse, CancellationToken cancellationToken = default)
    {
        await PublishEventAsync("PaymentApproved", paymentId, correlationId, providerResponse, cancellationToken);
    }

    public async Task PublishPaymentDeclinedAsync(Guid paymentId, Guid correlationId, string reason, CancellationToken cancellationToken = default)
    {
        await PublishEventAsync("PaymentDeclined", paymentId, correlationId, reason, cancellationToken);
    }

    public async Task PublishPaymentFailedAsync(Guid paymentId, Guid correlationId, string reason, CancellationToken cancellationToken = default)
    {
        await PublishEventAsync("PaymentFailed", paymentId, correlationId, reason, cancellationToken);
    }

    public async Task PublishGamePurchaseCompletedAsync(GamePurchaseCompletedEvent completedEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== INÍCIO PUBLICAÇÃO GAME PURCHASE COMPLETED ===");
        _logger.LogInformation("Evento recebido: PaymentId={PaymentId}, UserId={UserId}, GameId={GameId}, Amount={Amount}", 
            completedEvent.PaymentId, completedEvent.UserId, completedEvent.GameId, completedEvent.Amount);
        
        try
        {
            var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri("queue:game-purchase-completed"));
            await endpoint.Send(completedEvent, cancellationToken);
            
            _logger.LogInformation("✓ Evento GamePurchaseCompleted enviado com sucesso para fila SQS. PaymentId: {PaymentId}", 
                completedEvent.PaymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar evento GamePurchaseCompleted para fila SQS. PaymentId: {PaymentId}", 
                completedEvent.PaymentId);
            throw;
        }
        
        _logger.LogInformation("=== FIM PUBLICAÇÃO GAME PURCHASE COMPLETED ===");
    }

    public async Task PublishToQueueAsync<T>(string queueName, T eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Enviando evento para fila SQS {QueueName} via MassTransit", queueName);

            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData), "Event data cannot be null");
            }

            var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri($"queue:{queueName}"));
            await endpoint.Send(eventData, cancellationToken);
            
            _logger.LogInformation("✓ Evento enviado com sucesso para fila SQS {QueueName} via MassTransit", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar evento para fila SQS {QueueName} via MassTransit", queueName);
            throw;
        }
    }

    private async Task PublishEventAsync(string eventType, Guid paymentId, Guid correlationId, string? data, CancellationToken cancellationToken)
    {
        try
        {
            var paymentEvent = new PaymentEvent(paymentId, correlationId, eventType, DateTime.UtcNow, data);
            
            _logger.LogInformation("Publicando evento {EventType}: PaymentId={PaymentId}, CorrelationId={CorrelationId}", 
                eventType, paymentId, correlationId);

            // Por enquanto, apenas logamos o evento (não há fila específica para esses eventos)
            _logger.LogInformation("Evento publicado: {Event}", paymentEvent);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar evento {EventType} para PaymentId={PaymentId}", eventType, paymentId);
            throw;
        }
    }
}
