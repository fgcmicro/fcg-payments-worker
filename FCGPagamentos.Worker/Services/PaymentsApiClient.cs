using FCGPagamentos.Worker.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace FCGPagamentos.Worker.Services;

public class PaymentsApiClient : IPaymentsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentsApiClient> _logger;
    private readonly IObservabilityService _observabilityService;
    private readonly string _baseUrl;
    private readonly string _internalToken;

    public PaymentsApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PaymentsApiClient> logger,
        IObservabilityService observabilityService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _observabilityService = observabilityService;
        _baseUrl = configuration["PaymentsApi:BaseUrl"] ?? throw new InvalidOperationException("PaymentsApi:BaseUrl not configured");
        _internalToken = configuration["PaymentsApi:InternalToken"] ?? throw new InvalidOperationException("PaymentsApi:InternalToken not configured");
        
        _httpClient.DefaultRequestHeaders.Add("x-internal-token", _internalToken);
    }

    public async Task<Payment?> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var endpoint = "/internal/payments";
        var startTime = DateTime.UtcNow;
        
        _logger.LogInformation("[API-CALL] INÍCIO - Chamada HTTP POST para criar pagamento. PaymentId={PaymentId}, CorrelationId={CorrelationId}, Endpoint={Endpoint}, BaseUrl={BaseUrl}, Timestamp={Timestamp}",
            request.PaymentId, request.CorrelationId, endpoint, _baseUrl, startTime);
        
        try
        {
            var content = JsonContent.Create(request);
            _logger.LogDebug("[API-CALL] Enviando requisição HTTP. PaymentId={PaymentId}, CorrelationId={CorrelationId}, RequestPayload={RequestPayload}",
                request.PaymentId, request.CorrelationId, System.Text.Json.JsonSerializer.Serialize(request));
            
            var response = await _httpClient.PostAsync($"{_baseUrl}{endpoint}", content, cancellationToken);
            stopwatch.Stop();
            
            var responseTime = DateTime.UtcNow;
            var duration = stopwatch.Elapsed.TotalMilliseconds;
            
            _logger.LogInformation("[API-CALL] Resposta recebida. PaymentId={PaymentId}, CorrelationId={CorrelationId}, StatusCode={StatusCode}, DurationMs={DurationMs}, Timestamp={Timestamp}",
                request.PaymentId, request.CorrelationId, (int)response.StatusCode, duration, responseTime);
            
            if (response.IsSuccessStatusCode)
            {
                _observabilityService.TrackApiDependency("POST", endpoint, stopwatch.Elapsed, true);
                var payment = await response.Content.ReadFromJsonAsync<Payment>(cancellationToken: cancellationToken);
                
                _logger.LogInformation("[API-CALL] SUCESSO - Pagamento criado na API. PaymentId={PaymentId}, CorrelationId={CorrelationId}, PaymentStatus={PaymentStatus}, DurationMs={DurationMs}",
                    request.PaymentId, request.CorrelationId, payment?.Status, duration);
                
                return payment;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _observabilityService.TrackApiDependency("POST", endpoint, stopwatch.Elapsed, false);
            _logger.LogWarning("[API-CALL] FALHA - Resposta não bem-sucedida da API. PaymentId={PaymentId}, CorrelationId={CorrelationId}, StatusCode={StatusCode}, ErrorContent={ErrorContent}, DurationMs={DurationMs}, Timestamp={Timestamp}",
                request.PaymentId, request.CorrelationId, (int)response.StatusCode, errorContent, duration, responseTime);
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var endTime = DateTime.UtcNow;
            var duration = stopwatch.Elapsed.TotalMilliseconds;
            
            _observabilityService.TrackApiDependency("POST", endpoint, stopwatch.Elapsed, false);
            _logger.LogError(ex, "[API-CALL] ERRO - Exceção ao chamar API. PaymentId={PaymentId}, CorrelationId={CorrelationId}, Endpoint={Endpoint}, DurationMs={DurationMs}, Timestamp={Timestamp}, ExceptionType={ExceptionType}, ExceptionMessage={ExceptionMessage}",
                request.PaymentId, request.CorrelationId, endpoint, duration, endTime, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task<Payment?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var endpoint = $"/internal/payments/{paymentId}";
        
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}", cancellationToken);
            stopwatch.Stop();
            
            if (response.IsSuccessStatusCode)
            {
                _observabilityService.TrackApiDependency("GET", endpoint, stopwatch.Elapsed, true);
                return await response.Content.ReadFromJsonAsync<Payment>(cancellationToken: cancellationToken);
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _observabilityService.TrackApiDependency("GET", endpoint, stopwatch.Elapsed, true);
                return null;
            }
            
            _observabilityService.TrackApiDependency("GET", endpoint, stopwatch.Elapsed, false);
            _logger.LogWarning("Falha ao buscar pagamento {PaymentId}. Status: {StatusCode}", paymentId, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _observabilityService.TrackApiDependency("GET", endpoint, stopwatch.Elapsed, false);
            _logger.LogError(ex, "Erro ao buscar pagamento {PaymentId}", paymentId);
            throw;
        }
    }

    public async Task<bool> MarkProcessingAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await CallStatusEndpointAsync(paymentId, "mark-processing", cancellationToken);
    }

    public async Task<bool> MarkApprovedAsync(Guid paymentId, string providerResponse, CancellationToken cancellationToken = default)
    {
        var content = JsonContent.Create(new { providerResponse });
        return await CallStatusEndpointAsync(paymentId, "mark-approved", cancellationToken, content);
    }

    public async Task<bool> MarkDeclinedAsync(Guid paymentId, string providerResponse, string reason, CancellationToken cancellationToken = default)
    {
        var content = JsonContent.Create(new { providerResponse, reason });
        return await CallStatusEndpointAsync(paymentId, "mark-declined", cancellationToken, content);
    }

    public async Task<bool> MarkFailedAsync(Guid paymentId, string reason, CancellationToken cancellationToken = default)
    {
        var content = JsonContent.Create(new { reason });
        return await CallStatusEndpointAsync(paymentId, "mark-failed", cancellationToken, content);
    }

    private async Task<bool> CallStatusEndpointAsync(Guid paymentId, string endpoint, CancellationToken cancellationToken, HttpContent? content = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var fullEndpoint = $"/internal/payments/{paymentId}/{endpoint}";
        
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}{fullEndpoint}", content, cancellationToken);
            stopwatch.Stop();
            
            if (response.IsSuccessStatusCode)
            {
                _observabilityService.TrackApiDependency("POST", fullEndpoint, stopwatch.Elapsed, true);
                _logger.LogDebug("Status do pagamento {PaymentId} atualizado via {Endpoint}", paymentId, endpoint);
                return true;
            }
            
            _observabilityService.TrackApiDependency("POST", fullEndpoint, stopwatch.Elapsed, false);
            _logger.LogWarning("Falha ao atualizar status do pagamento {PaymentId} via {Endpoint}. Status: {StatusCode}", 
                paymentId, endpoint, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _observabilityService.TrackApiDependency("POST", fullEndpoint, stopwatch.Elapsed, false);
            _logger.LogError(ex, "Erro ao atualizar status do pagamento {PaymentId} via {Endpoint}", paymentId, endpoint);
            throw;
        }
    }
}
