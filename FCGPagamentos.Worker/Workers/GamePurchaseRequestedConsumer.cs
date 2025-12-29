using FCGPagamentos.Worker.Models;
using FCGPagamentos.Worker.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Workers;

// Consumer para processar mensagens da fila game-purchase-requested
public class GamePurchaseRequestedConsumer : IConsumer<GamePurchaseRequestedEvent>
{
    private readonly IPaymentService _paymentService;
    private readonly IObservabilityService _observabilityService;
    private readonly ILogger<GamePurchaseRequestedConsumer> _logger;

    public GamePurchaseRequestedConsumer(
        IPaymentService paymentService,
        IObservabilityService observabilityService,
        ILogger<GamePurchaseRequestedConsumer> logger)
    {
        _paymentService = paymentService;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GamePurchaseRequestedEvent> context)
    {
        var startTime = DateTime.UtcNow;
        var purchaseEvent = context.Message;
        
        _logger.LogInformation("[GAME-PURCHASE-REQUESTED] INÍCIO - Recebida mensagem da fila. PaymentId={PaymentId}, CorrelationId={CorrelationId}, UserId={UserId}, GameId={GameId}, Amount={Amount}, Currency={Currency}, PaymentMethod={PaymentMethod}, MessageId={MessageId}, Timestamp={Timestamp}",
            purchaseEvent.PaymentId, 
            purchaseEvent.CorrelationId, 
            purchaseEvent.UserId, 
            purchaseEvent.GameId,
            purchaseEvent.Amount,
            purchaseEvent.Currency,
            purchaseEvent.PaymentMethod,
            context.MessageId,
            startTime);

        try
        {
            // Configurar correlation ID para traces distribuídos
            _logger.LogDebug("[GAME-PURCHASE-REQUESTED] Configurando CorrelationId para observabilidade. CorrelationId={CorrelationId}", 
                purchaseEvent.CorrelationId);
            _observabilityService.SetCorrelationId(purchaseEvent.CorrelationId);

            // Criar pagamento
            _logger.LogInformation("[GAME-PURCHASE-REQUESTED] Iniciando criação do pagamento. PaymentId={PaymentId}, CorrelationId={CorrelationId}",
                purchaseEvent.PaymentId, purchaseEvent.CorrelationId);
            
            var success = await _paymentService.CreatePaymentAsync(purchaseEvent, context.CancellationToken);

            var endTime = DateTime.UtcNow;
            var duration = (endTime - startTime).TotalMilliseconds;

            if (success)
            {
                _logger.LogInformation("[GAME-PURCHASE-REQUESTED] SUCESSO - Pagamento criado com sucesso. PaymentId={PaymentId}, CorrelationId={CorrelationId}, DurationMs={DurationMs}, Timestamp={Timestamp}",
                    purchaseEvent.PaymentId, purchaseEvent.CorrelationId, duration, endTime);
            }
            else
            {
                _logger.LogWarning("[GAME-PURCHASE-REQUESTED] FALHA - Falha na criação do pagamento. PaymentId={PaymentId}, CorrelationId={CorrelationId}, DurationMs={DurationMs}, Timestamp={Timestamp}",
                    purchaseEvent.PaymentId, purchaseEvent.CorrelationId, duration, endTime);
                throw new InvalidOperationException($"Falha na criação do pagamento {purchaseEvent.PaymentId}");
            }
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            var duration = (endTime - startTime).TotalMilliseconds;
            
            _logger.LogError(ex, "[GAME-PURCHASE-REQUESTED] ERRO - Exceção ao processar evento. PaymentId={PaymentId}, CorrelationId={CorrelationId}, UserId={UserId}, GameId={GameId}, DurationMs={DurationMs}, Timestamp={Timestamp}, ExceptionType={ExceptionType}, ExceptionMessage={ExceptionMessage}",
                purchaseEvent.PaymentId, 
                purchaseEvent.CorrelationId,
                purchaseEvent.UserId,
                purchaseEvent.GameId,
                duration,
                endTime,
                ex.GetType().Name,
                ex.Message);
            throw; // Re-throw para que MassTransit possa fazer retry
        }
    }
}

