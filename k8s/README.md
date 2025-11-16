# Kubernetes Deployment - FCG Payments Worker

Este diretório contém os manifestos Kubernetes para deploy do FCG Payments Worker.

## Arquivos

- `deployment.yaml` - Deployment do worker com configuração de recursos, health checks e variáveis de ambiente
- `service.yaml` - Service para expor o worker no cluster
- `configmap.yaml` - ConfigMap com configurações não sensíveis
- `secret.yaml.example` - Exemplo de Secret (criar o arquivo `secret.yaml` com valores reais)
- `hpa.yaml` - Horizontal Pod Autoscaler para escalar automaticamente baseado em CPU/memória

## Pré-requisitos

1. Cluster Kubernetes configurado
2. `kubectl` instalado e configurado
3. Imagem Docker do worker construída e disponível no registry

## Deploy

### 1. Criar o Secret

Copie o arquivo de exemplo e preencha com valores reais:

```bash
cp secret.yaml.example secret.yaml
# Edite secret.yaml com valores reais
```

**IMPORTANTE**: Não commite o arquivo `secret.yaml` com valores reais no controle de versão!

### 2. Aplicar os manifestos

```bash
# Aplicar ConfigMap
kubectl apply -f configmap.yaml

# Aplicar Secret
kubectl apply -f secret.yaml

# Aplicar Deployment
kubectl apply -f deployment.yaml

# Aplicar Service
kubectl apply -f service.yaml

# Aplicar HPA (opcional)
kubectl apply -f hpa.yaml
```

### 3. Verificar o deploy

```bash
# Verificar pods
kubectl get pods -l app=fcg-payments-worker

# Verificar logs
kubectl logs -l app=fcg-payments-worker -f

# Verificar status do deployment
kubectl get deployment fcg-payments-worker

# Verificar HPA
kubectl get hpa fcg-payments-worker-hpa
```

## Configuração

### Variáveis de Ambiente

As variáveis de ambiente são configuradas via ConfigMap e Secret:

**ConfigMap** (`configmap.yaml`):
- `payments-api-base-url` - URL da API de pagamentos
- `aws-region` - Região AWS
- `aws-account-id` - ID da conta AWS

**Secret** (`secret.yaml`):
- `payments-api-internal-token` - Token de autenticação da API
- `aws-access-key` - AWS Access Key
- `aws-secret-key` - AWS Secret Key

### Recursos

O deployment está configurado com:
- **Requests**: 256Mi de memória, 250m de CPU
- **Limits**: 512Mi de memória, 500m de CPU

Ajuste conforme necessário baseado no volume de mensagens.

### Health Checks

- **Liveness Probe**: Verifica se o pod está vivo (porta 8080, path `/health`)
- **Readiness Probe**: Verifica se o pod está pronto para receber tráfego
- **Startup Probe**: Aguarda até 5 minutos para o pod iniciar

### Escalabilidade

O HPA está configurado para:
- **Mínimo**: 2 réplicas
- **Máximo**: 10 réplicas
- **Escala baseada em**: CPU (70%) e Memória (80%)

## Troubleshooting

### Ver logs

```bash
kubectl logs -l app=fcg-payments-worker -f
```

### Verificar eventos

```bash
kubectl get events --sort-by='.lastTimestamp'
```

### Descrever pod

```bash
kubectl describe pod <pod-name>
```

### Executar comando no pod

```bash
kubectl exec -it <pod-name> -- /bin/sh
```

### Verificar conectividade com SQS

```bash
kubectl exec -it <pod-name> -- curl http://localhost:8080/health
```

## Atualização

Para atualizar o deployment:

```bash
# Atualizar imagem
kubectl set image deployment/fcg-payments-worker worker=fcg-payments-worker:v1.1.0

# Ou fazer rolling update
kubectl rollout restart deployment/fcg-payments-worker

# Verificar status do rollout
kubectl rollout status deployment/fcg-payments-worker
```

## Rollback

Se necessário fazer rollback:

```bash
# Ver histórico de rollouts
kubectl rollout history deployment/fcg-payments-worker

# Fazer rollback para versão anterior
kubectl rollout undo deployment/fcg-payments-worker

# Fazer rollback para versão específica
kubectl rollout undo deployment/fcg-payments-worker --to-revision=2
```

