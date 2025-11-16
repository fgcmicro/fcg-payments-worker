using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FCGPagamentos.Worker.HealthChecks;

public class PaymentWorkerHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Health check simples - sempre retorna saudável
        // Pode ser expandido para verificar conexões com SQS, API, etc.
        return Task.FromResult(HealthCheckResult.Healthy("Payment Worker está funcionando corretamente"));
    }
}

