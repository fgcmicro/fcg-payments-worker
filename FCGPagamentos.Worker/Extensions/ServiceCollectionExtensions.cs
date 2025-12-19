using Amazon.Runtime;
using FCGPagamentos.Worker.Services;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurar HttpClient para API
        services.AddHttpClient<IPaymentsApiClient, PaymentsApiClient>(client =>
        {
            var baseUrl = configuration["PaymentsApi:BaseUrl"];
            if (!string.IsNullOrEmpty(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
            client.DefaultRequestHeaders.Add("User-Agent", "FCGPagamentos-Worker/1.0");
        });

        // Configurar AWS Credentials
        var accessKey = configuration["AWS:AccessKey"];
        var secretKey = configuration["AWS:SecretKey"];
        var sessionToken = configuration["AWS:SessionToken"];
        var region = configuration["AWS:Region"];

        if (string.IsNullOrEmpty(accessKey))
            throw new InvalidOperationException("AWS:AccessKey não configurado");
        if (string.IsNullOrEmpty(secretKey))
            throw new InvalidOperationException("AWS:SecretKey não configurado");
        if (string.IsNullOrEmpty(region))
            throw new InvalidOperationException("AWS:Region não configurado");

        // Configurar MassTransit com Amazon SQS para Kubernetes
        // Configura tanto consumo quanto publicação de mensagens
        services.AddMassTransit(x =>
        {
            x.UsingAmazonSqs((context, cfg) =>
            {
                cfg.Host(region, h =>
                {
                    AWSCredentials credentials;
                    if (!string.IsNullOrEmpty(sessionToken))
                    {
                        credentials = new SessionAWSCredentials(accessKey, secretKey, sessionToken);
                    }
                    else
                    {
                        credentials = new BasicAWSCredentials(accessKey, secretKey);
                    }
                    h.Credentials(credentials);
                });

                // Configurar filas para consumo
                cfg.ReceiveEndpoint("game-purchase-requested", e =>
                {
                    e.ConfigureConsumer<Workers.GamePurchaseRequestedConsumer>(context);
                    e.PrefetchCount = 10; // Processar até 10 mensagens por vez
                });

                cfg.ReceiveEndpoint("payments-to-process", e =>
                {
                    e.ConfigureConsumer<Workers.ProcessPaymentConsumer>(context);
                    e.PrefetchCount = 10;
                });
            });

            // Registrar consumers
            x.AddConsumer<Workers.GamePurchaseRequestedConsumer>();
            x.AddConsumer<Workers.ProcessPaymentConsumer>();
        });

        // Nota: O MassTransit Hosted Service é automaticamente registrado pelo AddMassTransit

        // Registrar serviços
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IEventPublisher, EventPublisher>();
        services.AddScoped<IObservabilityService, ObservabilityService>();
        
        // Configurar AWS X-Ray para APM
        services.AddXRay(configuration);

        return services;
    }
}
