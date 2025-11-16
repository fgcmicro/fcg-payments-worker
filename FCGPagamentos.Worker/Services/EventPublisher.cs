using FCGPagamentos.Worker.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Services;

public class EventPublisher : IEventPublisher
{
    private readonly ILogger<EventPublisher> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    public EventPublisher(ILogger<EventPublisher> logger, IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _publishEndpoint = publishEndpoint;
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
            // Publicar usando MassTransit - o nome da fila é configurado via SetEntityName no ServiceCollectionExtensions
            await _publishEndpoint.Publish(completedEvent, cancellationToken);
            
            _logger.LogInformation("✓ Evento GamePurchaseCompleted publicado com sucesso via MassTransit. PaymentId: {PaymentId}", 
                completedEvent.PaymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar evento GamePurchaseCompleted via MassTransit. PaymentId: {PaymentId}", 
                completedEvent.PaymentId);
            throw;
        }
        
        _logger.LogInformation("=== FIM PUBLICAÇÃO GAME PURCHASE COMPLETED ===");
    }

    // Método genérico para publicar em qualquer fila usando MassTransit
    public async Task PublishToQueueAsync<T>(string queueName, T eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Publicando evento na fila SQS {QueueName} via MassTransit", queueName);

            // MassTransit publica diretamente o objeto - serialização é automática
            // Para SQS, o nome da fila é determinado pelo tipo da mensagem ou pode ser configurado via SetEntityName
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData), "Event data cannot be null");
            }
            await _publishEndpoint.Publish(eventData, cancellationToken);
            
            _logger.LogInformation("✓ Evento publicado com sucesso na fila SQS {QueueName} via MassTransit", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar evento na fila SQS {QueueName} via MassTransit", queueName);
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
