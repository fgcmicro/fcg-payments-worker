using Amazon.Runtime;
using FCGPagamentos.Worker.Services;
using fcg.Contracts;
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
        var region = configuration["AWS:Region"] ?? "us-east-1"; // Default para us-east-1

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
                    // Se credenciais estiverem configuradas, usar elas
                    // Caso contrário, usar o perfil AWS padrão (busca automaticamente de ~/.aws/credentials ou variáveis de ambiente)
                    if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
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
                    }
                    // Se não houver credenciais configuradas, o AWS SDK usará automaticamente:
                    // 1. Variáveis de ambiente (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
                    // 2. Perfil AWS (~/.aws/credentials)
                    // 3. IAM Role (se rodando no EC2/ECS/Lambda)
                });

                // Configurar mapeamento de mensagens para filas
                cfg.Message<GamePurchaseRequested>(m =>
                {
                    m.SetEntityName("game-purchase-requested");
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
