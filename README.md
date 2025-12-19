# FCG Payments Worker

Um worker desenvolvido em .NET 8 para processamento de pagamentos de jogos, utilizando filas AWS SQS para comunicação assíncrona e executando no Kubernetes.

## 📋 Visão Geral

O FCG Payments Worker é responsável por processar pagamentos de jogos de forma assíncrona através de dois consumers principais:

- **GamePurchaseRequestedConsumer**: Processa eventos de compra de jogos e cria pagamentos
- **ProcessPaymentConsumer**: Processa mensagens de pagamento da fila e executa o pagamento

## 🏗️ Arquitetura

Este worker faz parte de uma arquitetura de microsserviços orquestrada no Kubernetes. Para documentação completa da arquitetura e fluxo assíncrono, consulte:

- **[Arquitetura do Sistema](../fcg.GameService/docs/architecture.md)**: Diagrama completo da arquitetura no Kubernetes
- **[Fluxo de Comunicação Assíncrona](../fcg.GameService/docs/async-communication.md)**: Documentação detalhada do fluxo de mensagens

### Diagrama Simplificado

```
┌─────────────────────┐         ┌─────────────────────┐         ┌─────────────────────┐
│   Game Purchase     │────────▶│  game-purchase-     │────────▶│  GamePurchase       │
│   Requested Event   │         │  requested (SQS)    │         │  RequestedConsumer │
└─────────────────────┘         └─────────────────────┘         │  (K8s Worker)       │
                                                                 └─────────────────────┘
                                                                          │
                                                                          ▼
┌─────────────────────┐         ┌─────────────────────┐         ┌─────────────────────┐
│   Payment           │────────▶│  payments-to-       │────────▶│  ProcessPayment     │
│   Requested         │         │  process (SQS)      │         │  Consumer (K8s)     │
└─────────────────────┘         └─────────────────────┘         └─────────────────────┘
                                                                          │
                                                                          ▼
                                                               ┌─────────────────────┐
                                                               │  game-purchase-     │
                                                               │  completed (SQS)    │
                                                               └─────────────────────┘
```

## 🚀 Pré-requisitos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (para build da imagem)
- [Kubernetes](https://kubernetes.io/) cluster configurado
- [kubectl](https://kubernetes.io/docs/tasks/tools/) instalado e configurado
- Conta AWS com permissões para SQS
- [AWS CLI](https://aws.amazon.com/cli/) configurado (opcional, para testes)

## 📦 Instalação

### 1. Clone o repositório

```bash
git clone https://github.com/RodigoLima/fcg-payments-worker.git
cd fcg-payments-worker/FCGPagamentos.Worker
```

### 2. Restaure as dependências

```bash
dotnet restore
```

### 3. Configure as variáveis de ambiente

#### Opção 1: Arquivo appsettings.Development.json (para testes locais)

Copie o arquivo de exemplo:

```bash
cp appsettings.Development.json.example appsettings.Development.json
```

Edite `appsettings.Development.json` e preencha com suas credenciais:

```json
{
  "PaymentsApi": {
    "BaseUrl": "http://localhost:5080",
    "InternalToken": "super-secret"
  },
  "AWS": {
    "AccessKey": "sua-access-key",
    "SecretKey": "sua-secret-key",
    "Region": "us-east-1",
    "AccountId": "seu-account-id"
  }
}
```

#### Opção 2: Variáveis de ambiente do sistema

```bash
export PaymentsApi__BaseUrl="http://localhost:5080"
export PaymentsApi__InternalToken="super-secret"
export AWS__AccessKey="sua-access-key"
export AWS__SecretKey="sua-secret-key"
export AWS__Region="us-east-1"
export AWS__AccountId="seu-account-id"
```

## 🧪 Testes Locais

### Executar localmente

```bash
cd FCGPagamentos.Worker
dotnet run
```

O worker iniciará e começará a consumir mensagens das filas SQS automaticamente.

### Verificar Health Check

```bash
curl http://localhost:8080/health
```

## 🚀 Deploy no Kubernetes

### 1. Build da imagem Docker

```bash
docker build -t fcg-payments-worker:latest -f FCGPagamentos.Worker/Dockerfile .
```

### 2. Push para registry (se necessário)

```bash
docker tag fcg-payments-worker:latest seu-registry/fcg-payments-worker:latest
docker push seu-registry/fcg-payments-worker:latest
```

### 3. Criar Secret no Kubernetes

Copie o arquivo de exemplo e preencha com valores reais:

```bash
cp k8s/secret.yaml.example k8s/secret.yaml
# Edite k8s/secret.yaml com valores reais
```

### 4. Aplicar manifestos Kubernetes

```bash
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/hpa.yaml
```

### 5. Verificar deploy

```bash
kubectl get pods -l app=fcg-payments-worker
kubectl logs -l app=fcg-payments-worker -f
```

Veja mais detalhes em [k8s/README.md](k8s/README.md).

## ⚙️ Configuração

### Variáveis de Ambiente Obrigatórias

| Variável | Descrição | Exemplo |
|----------|-----------|---------|
| `PaymentsApi__BaseUrl` | URL base da API de pagamentos | `https://api.payments.com` |
| `PaymentsApi__InternalToken` | Token de autenticação interno | `super-secret-token` |
| `AWS__AccessKey` | AWS Access Key ID | `AKIAXXXXXXXXXXXXXXXX` |
| `AWS__SecretKey` | AWS Secret Access Key | `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx` |
| `AWS__Region` | Região AWS onde as filas SQS estão | `us-east-1` |
| `AWS__AccountId` | ID da conta AWS | `238576301773` |

**Nota**: As variáveis de ambiente devem usar `__` (dois underscores) como separador para compatibilidade com Kubernetes. Por exemplo: `PaymentsApi__BaseUrl` ao invés de `PaymentsApi:BaseUrl`.

### Configuração de Consumers SQS

Os consumers MassTransit são automaticamente configurados para processar mensagens das filas SQS:

- **GamePurchaseRequestedConsumer**: Consome da fila `game-purchase-requested`
- **ProcessPaymentConsumer**: Consome da fila `payments-to-process`

O MassTransit gerencia automaticamente o polling, retry e dead letter queues.

## 📊 Filas SQS Utilizadas

### `game-purchase-requested`
- **Consumer**: `GamePurchaseRequestedConsumer`
- **Payload**: `GamePurchaseRequestedEvent`
- **Descrição**: Processa eventos de compra de jogos e cria pagamentos
- **Prefetch Count**: 10 mensagens por vez

### `payments-to-process`
- **Consumer**: `ProcessPaymentConsumer`
- **Payload**: `PaymentRequestedMessage`
- **Descrição**: Processa mensagens de pagamento e executa o pagamento
- **Prefetch Count**: 10 mensagens por vez

### `game-purchase-completed`
- **Publicado por**: `EventPublisher`
- **Payload**: `GamePurchaseCompletedEvent`
- **Descrição**: Evento publicado após processamento bem-sucedido do pagamento

## 🔧 Desenvolvimento

### Estrutura do Projeto

```
FCGPagamentos.Worker/
├── Workers/                    # Consumers MassTransit
│   ├── GamePurchaseRequestedConsumer.cs
│   └── ProcessPaymentConsumer.cs
├── Models/                     # Modelos de dados
│   ├── GamePurchaseRequestedEvent.cs
│   ├── PaymentRequestedMessage.cs
│   └── Payment.cs
├── Services/                   # Serviços de negócio
│   ├── PaymentService.cs
│   ├── PaymentsApiClient.cs
│   ├── EventPublisher.cs
│   ├── ObservabilityService.cs
│   └── SqsClientFactory.cs
├── Extensions/                 # Extensões de configuração
│   └── ServiceCollectionExtensions.cs
├── HealthChecks/               # Health checks para K8s
│   └── PaymentWorkerHealthCheck.cs
├── Program.cs                  # Entry point do serviço
└── appsettings.json           # Configurações base
```

### Build para produção

```bash
dotnet build -c Release
```

### Publicar para Docker

```bash
docker build -t fcg-payments-worker:latest -f FCGPagamentos.Worker/Dockerfile .
```

## 📝 Observabilidade

O projeto utiliza logs estruturados que incluem:

- Correlation IDs para rastreamento distribuído
- Payment IDs para rastreamento de pagamentos
- Métricas de performance e erros

### Health Checks

O worker expõe um endpoint de health check em `/health` para monitoramento do Kubernetes.

## 🔄 Migração do Azure Functions / AWS Lambda

Este projeto foi migrado de Azure Functions para AWS Lambda e depois para Kubernetes. As principais mudanças:

- ✅ Removidas dependências do Azure Functions e AWS Lambda
- ✅ Implementado MassTransit para consumo de filas SQS
- ✅ Criados consumers para processamento assíncrono
- ✅ Configuração via Kubernetes manifests
- ✅ Health checks para K8s
- ✅ Suporte a appsettings.json para desenvolvimento local

## 🐛 Troubleshooting

### Erro: "AWS:AccessKey não configurado"

Verifique se as credenciais AWS estão configuradas no `appsettings.Development.json` ou variáveis de ambiente.

### Erro: "Failed to connect to SQS"

1. Verifique se as credenciais AWS estão corretas
2. Verifique se você tem permissões para acessar SQS
3. Verifique se a região está correta
4. Teste a conexão: `aws sqs list-queues --region us-east-1`

### Erro: "No messages being consumed"

1. Verifique se as filas existem: `aws sqs list-queues`
2. Verifique se há mensagens na fila
3. Verifique os logs para erros de conexão
4. Verifique se o nome da fila está correto no código

### Pod não inicia no Kubernetes

1. Verifique os logs: `kubectl logs <pod-name>`
2. Verifique os eventos: `kubectl describe pod <pod-name>`
3. Verifique se os secrets estão configurados: `kubectl get secrets`

## 📚 Recursos

- [MassTransit Documentation](https://masstransit.io/)
- [Kubernetes Documentation](https://kubernetes.io/docs/)
- [AWS SQS Documentation](https://docs.aws.amazon.com/sqs/)

## 📄 Licença

Este projeto é privado e proprietário.
