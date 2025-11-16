using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Services;

public class ObservabilityService : IObservabilityService
{
    private readonly ILogger<ObservabilityService> _logger;
    private static readonly ActivitySource _activitySource = new("FCGPagamentos.Worker.PaymentProcessing");
    private Guid? _correlationId;

    public ObservabilityService(ILogger<ObservabilityService> logger)
    {
        _logger = logger;
    }

    public Activity? StartPaymentProcessingActivity(Guid paymentId, Guid correlationId)
    {
        var activity = _activitySource.StartActivity("ProcessPayment");
        
        if (activity != null)
        {
            activity.SetTag("payment.id", paymentId.ToString());
            activity.SetTag("payment.correlation_id", correlationId.ToString());
            activity.SetTag("service.name", "FCGPagamentos.Worker");
            activity.SetTag("service.version", "1.0.0");
        }

        _logger.LogInformation("Iniciando processamento de pagamento. PaymentId: {PaymentId}, CorrelationId: {CorrelationId}", 
            paymentId, correlationId);

        return activity;
    }

    public void TrackApiDependency(string operation, string endpoint, TimeSpan duration, bool success)
    {
        _logger.LogInformation(
            "Dependência de API: {Operation} | Endpoint: {Endpoint} | Duração: {Duration}ms | Sucesso: {Success}",
            operation, endpoint, duration.TotalMilliseconds, success);
    }

    public void SetCorrelationId(Guid correlationId)
    {
        _correlationId = correlationId;
        // CloudWatch Logs pode usar correlation ID através de structured logging
        _logger.LogInformation("CorrelationId definido: {CorrelationId}", correlationId);
    }
}
