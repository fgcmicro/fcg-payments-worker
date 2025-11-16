using FCGPagamentos.Worker.Models;
using FCGPagamentos.Worker.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Workers;

// Consumer para processar mensagens da fila payments-to-process
public class ProcessPaymentConsumer : IConsumer<PaymentRequestedMessage>
{
    private readonly IPaymentService _paymentService;
    private readonly IObservabilityService _observabilityService;
    private readonly ILogger<ProcessPaymentConsumer> _logger;

    public ProcessPaymentConsumer(
        IPaymentService paymentService,
        IObservabilityService observabilityService,
        ILogger<ProcessPaymentConsumer> logger)
    {
        _paymentService = paymentService;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentRequestedMessage> context)
    {
        var paymentMessage = context.Message;
        
        _logger.LogInformation("Processando mensagem PaymentRequested: PaymentId={PaymentId}, CorrelationId={CorrelationId}, UserId={UserId}, GameId={GameId}",
            paymentMessage.PaymentId, paymentMessage.CorrelationId, paymentMessage.UserId, paymentMessage.GameId);

        try
        {
            // Configurar correlation ID para traces distribuídos
            _observabilityService.SetCorrelationId(paymentMessage.CorrelationId);

            // Processar pagamento
            var success = await _paymentService.ProcessPaymentAsync(paymentMessage, context.CancellationToken);

            if (success)
            {
                _logger.LogInformation("Pagamento {PaymentId} processado com sucesso", paymentMessage.PaymentId);
            }
            else
            {
                _logger.LogWarning("Falha no processamento do pagamento {PaymentId}", paymentMessage.PaymentId);
                throw new InvalidOperationException($"Falha no processamento do pagamento {paymentMessage.PaymentId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem PaymentRequested. PaymentId: {PaymentId}, CorrelationId: {CorrelationId}",
                paymentMessage.PaymentId, paymentMessage.CorrelationId);
            throw; // Re-throw para que MassTransit possa fazer retry
        }
    }
}

