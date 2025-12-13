using FCGPagamentos.Application.DTOs;
using FCGPagamentos.Worker.Models;
using FCGPagamentos.Worker.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using PaymentRequestedMessageApi = FCGPagamentos.Application.DTOs.PaymentRequestedMessage;
using PaymentRequestedMessageWorker = FCGPagamentos.Worker.Models.PaymentRequestedMessage;

namespace FCGPagamentos.Worker.Workers;

// Consumer para processar mensagens da fila payments-to-process
// Aceita o formato da API (FCGPagamentos.Application.DTOs:PaymentRequestedMessage)
public class ProcessPaymentConsumer : IConsumer<PaymentRequestedMessageApi>
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

    public async Task Consume(ConsumeContext<PaymentRequestedMessageApi> context)
    {
        try
        {
            _logger.LogInformation("Mensagem recebida - Amount (string): '{Amount}', PaymentId: {PaymentId}", 
                context.Message.Amount, context.Message.PaymentId);
            
            // Converter o DTO da API para o modelo do worker
            var paymentMessage = context.Message.ToPaymentRequestedMessage(); // Retorna PaymentRequestedMessageWorker
            
            _logger.LogInformation("Mensagem convertida - Amount (decimal): {Amount}, PaymentId: {PaymentId}", 
                paymentMessage.Amount, paymentMessage.PaymentId);
            
            _logger.LogInformation("Processando mensagem PaymentRequested: PaymentId={PaymentId}, CorrelationId={CorrelationId}, UserId={UserId}, GameId={GameId}",
                paymentMessage.PaymentId, paymentMessage.CorrelationId, paymentMessage.UserId, paymentMessage.GameId);

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
            var paymentId = context.Message?.PaymentId ?? "unknown";
            var correlationId = context.Message?.CorrelationId ?? "unknown";
            
            _logger.LogError(ex, "Erro ao processar mensagem PaymentRequested. PaymentId: {PaymentId}, CorrelationId: {CorrelationId}",
                paymentId, correlationId);
            throw; // Re-throw para que MassTransit possa fazer retry
        }
    }
}

