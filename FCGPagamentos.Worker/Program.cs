using FCGPagamentos.Worker.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FCGPagamentos.Worker;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                // Carregar appsettings.json primeiro
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                
                // Carregar appsettings.{Environment}.json (ex: appsettings.Development.json)
                var env = context.HostingEnvironment;
                config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                
                // Carregar variáveis de ambiente (sobrescreve appsettings)
                config.AddEnvironmentVariables();
                
                // Suporta variáveis de ambiente com __ (dois underscores) para compatibilidade com K8s
                // Converte PaymentsApi__BaseUrl para PaymentsApi:BaseUrl
                var envVars = Environment.GetEnvironmentVariables();
                var inMemoryConfig = new Dictionary<string, string?>();
                
                foreach (System.Collections.DictionaryEntry entry in envVars)
                {
                    var key = entry.Key.ToString();
                    if (key != null && key.Contains("__"))
                    {
                        var configKey = key.Replace("__", ":");
                        inMemoryConfig[configKey] = entry.Value?.ToString();
                    }
                }
                
                config.AddInMemoryCollection(inMemoryConfig);
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://0.0.0.0:8080");
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHealthChecks("/health");
                        endpoints.MapGet("/", async context =>
                        {
                            await context.Response.WriteAsync("FCG Payments Worker - Running");
                        });
                    });
                });
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                
                // Configurar serviços de pagamento (inclui MassTransit que gerencia os workers automaticamente)
                services.AddPaymentServices(configuration);
                
                // Configurar Health Checks
                services.AddHealthChecks()
                    .AddCheck<HealthChecks.PaymentWorkerHealthCheck>("payment-worker");
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                
                // Carregar configuração de logging primeiro
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                
                // Habilitar logs detalhados do MassTransit para diagnóstico
                logging.AddFilter("MassTransit", LogLevel.Debug);
                logging.AddFilter("MassTransit.AmazonSqsTransport", LogLevel.Debug);
                
                // Filtrar logs de health checks do middleware de diagnóstico
                // Mantém logs de Information dos workers (FCGPagamentos.Worker.*, MassTransit.*)
                logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", 
                    logLevel => 
                    {
                        // Filtrar apenas logs de Information que são relacionados a endpoints
                        // Logs de Error e Warning são mantidos
                        return logLevel > LogLevel.Information;
                    });
                
                // Filtrar logs de health checks do EndpointMiddleware (que gera logs de "Executing/Executed endpoint")
                // IMPORTANTE: Este filtro deve ser aplicado DEPOIS do AddConfiguration para sobrescrever configurações do appsettings.json
                logging.AddFilter("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.Warning);
            });
}
