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
        var purchaseEvent = context.Message;
        
        _logger.LogInformation("Processando evento GamePurchaseRequested: PaymentId={PaymentId}, CorrelationId={CorrelationId}, UserId={UserId}, GameId={GameId}",
            purchaseEvent.PaymentId, purchaseEvent.CorrelationId, purchaseEvent.UserId, purchaseEvent.GameId);

        try
        {
            // Configurar correlation ID para traces distribuídos
            _observabilityService.SetCorrelationId(purchaseEvent.CorrelationId);

            // Criar pagamento
            var success = await _paymentService.CreatePaymentAsync(purchaseEvent, context.CancellationToken);

            if (success)
            {
                _logger.LogInformation("Pagamento {PaymentId} criado com sucesso", purchaseEvent.PaymentId);
            }
            else
            {
                _logger.LogWarning("Falha na criação do pagamento {PaymentId}", purchaseEvent.PaymentId);
                throw new InvalidOperationException($"Falha na criação do pagamento {purchaseEvent.PaymentId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento GamePurchaseRequested. PaymentId: {PaymentId}, CorrelationId: {CorrelationId}",
                purchaseEvent.PaymentId, purchaseEvent.CorrelationId);
            throw; // Re-throw para que MassTransit possa fazer retry
        }
    }
}

