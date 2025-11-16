# 📦 Deploy - Hub de Integração

Esta pasta contém todos os arquivos necessários para fazer deploy do Hub de Integração, tanto em ambiente Docker Compose quanto em Kubernetes.

## 📁 Estrutura

```
deploy/
├── docker-compose.yml          # Orquestração completa com Docker Compose
├── init-databases.sh           # Script de inicialização dos bancos PostgreSQL
├── prometheus.yml              # Configuração do Prometheus
├── promtail-config.yml         # Configuração do Promtail (logs)
├── otel-collector-config.yaml  # Configuração do OpenTelemetry Collector
├── k8s/                        # Manifests Kubernetes
│   ├── namespace.yaml          # Namespace do cluster
│   ├── configmap.yaml         # Configurações compartilhadas
│   ├── secrets.yaml           # Secrets (senhas, connection strings)
│   ├── hpa-inbound.yaml       # Horizontal Pod Autoscaler para Inbound
│   ├── pdb.yaml               # Pod Disruption Budgets
│   ├── networkpolicies.yaml   # Políticas de rede
│   ├── gateway/               # Gateway YARP
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── identityserver/        # IdentityServer
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── inbound/               # Inbound API
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── orchestrator/          # Orchestrator Worker
│   │   └── deployment.yaml
│   └── outbound/              # Outbound Worker
│       └── deployment.yaml
└── helm/                      # Helm Charts (futuro)
```

## 🐳 Docker Compose

### Pré-requisitos
- Docker Desktop ou Docker Engine
- Docker Compose v2+

### Como usar

```bash
# Subir toda a infraestrutura e aplicações
docker-compose -f deploy/docker-compose.yml up -d

# Ver logs
docker-compose -f deploy/docker-compose.yml logs -f

# Parar tudo
docker-compose -f deploy/docker-compose.yml down

# Parar e remover volumes
docker-compose -f deploy/docker-compose.yml down -v
```

### Serviços expostos

- **Gateway**: http://localhost:5000
- **Inbound API**: http://localhost:5001
- **IdentityServer**: http://localhost:5002
- **RabbitMQ Management**: http://localhost:15672 (guest/guest)
- **Jaeger UI**: http://localhost:16686
- **Grafana**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Seq**: http://localhost:5341

### Aplicar Migrations

Após subir os serviços, execute as migrations:

```bash
cd src/Inbound.Api && dotnet ef database update
cd ../Orchestrator.Worker && dotnet ef database update
cd ../Outbound.Worker && dotnet ef database update
```

## ☸️ Kubernetes

### Pré-requisitos
- Cluster Kubernetes (minikube, kind, EKS, AKS, GKE, etc.)
- `kubectl` configurado
- Imagens Docker disponíveis no registry (ou use `kind load` para desenvolvimento local)

### Ordem de Deploy

1. **Namespace**
```bash
kubectl apply -f deploy/k8s/namespace.yaml
```

2. **ConfigMaps e Secrets**
```bash
kubectl apply -f deploy/k8s/configmap.yaml
kubectl apply -f deploy/k8s/secrets.yaml
```

3. **Infraestrutura** (PostgreSQL, RabbitMQ, Redis - se não usar serviços gerenciados)
```bash
# Se você tiver deployments para infraestrutura
kubectl apply -f deploy/k8s/postgres/
kubectl apply -f deploy/k8s/rabbitmq/
kubectl apply -f deploy/k8s/redis/
```

4. **Aplicações**
```bash
# IdentityServer primeiro (necessário para autenticação)
kubectl apply -f deploy/k8s/identityserver/

# Gateway e Inbound
kubectl apply -f deploy/k8s/gateway/
kubectl apply -f deploy/k8s/inbound/

# Workers
kubectl apply -f deploy/k8s/orchestrator/
kubectl apply -f deploy/k8s/outbound/
```

5. **Políticas e Autoscaling**
```bash
kubectl apply -f deploy/k8s/networkpolicies.yaml
kubectl apply -f deploy/k8s/pdb.yaml
kubectl apply -f deploy/k8s/hpa-inbound.yaml
```

### Deploy Completo (tudo de uma vez)

```bash
kubectl apply -f deploy/k8s/
```

### Verificar Status

```bash
# Ver pods
kubectl get pods -n integration-hub

# Ver serviços
kubectl get svc -n integration-hub

# Ver logs
kubectl logs -f deployment/inbound-api -n integration-hub
```

### Configuração de Secrets

⚠️ **IMPORTANTE**: Os secrets em `secrets.yaml` contêm valores padrão para desenvolvimento. 
**NUNCA** commite secrets reais em produção. Use:
- Kubernetes Secrets gerenciados
- External Secrets Operator
- Sealed Secrets
- Cloud Provider Secrets Manager

### Imagens Docker

Os deployments assumem que as imagens estão disponíveis como:
- `integrationhub/gateway-yarp:latest`
- `integrationhub/identityserver:latest`
- `integrationhub/inbound-api:latest`
- `integrationhub/orchestrator-worker:latest`
- `integrationhub/outbound-worker:latest`

Para desenvolvimento local com `kind`:

```bash
# Build das imagens
docker build -f Dockerfile.gateway -t integrationhub/gateway-yarp:latest .
docker build -f Dockerfile.identityserver -t integrationhub/identityserver:latest .
docker build -f Dockerfile.inbound -t integrationhub/inbound-api:latest .
docker build -f Dockerfile.orchestrator -t integrationhub/orchestrator-worker:latest .
docker build -f Dockerfile.outbound -t integrationhub/outbound-worker:latest .

# Carregar no kind
kind load docker-image integrationhub/gateway-yarp:latest
kind load docker-image integrationhub/identityserver:latest
kind load docker-image integrationhub/inbound-api:latest
kind load docker-image integrationhub/orchestrator-worker:latest
kind load docker-image integrationhub/outbound-worker:latest
```

## 🔧 Configurações

### Variáveis de Ambiente

Os serviços usam as seguintes variáveis principais:

- `ConnectionStrings__PostgreSQL`: String de conexão PostgreSQL
- `ConnectionStrings__Redis`: String de conexão Redis
- `ConnectionStrings__RabbitMQ`: String de conexão RabbitMQ
- `Jwt__Authority`: URL do IdentityServer
- `Jwt__Audience`: Audience do JWT
- `OpenTelemetry__OtlpEndpoint`: Endpoint do OpenTelemetry Collector
- `Seq__ServerUrl`: URL do Seq para logging

### Health Checks

- **Gateway**: `/healthz` (liveness), `/readyz` (readiness)
- **IdentityServer**: `/healthz` (liveness), `/readyz` (readiness)
- **Inbound API**: `/healthz` (liveness), `/readyz` (readiness)
- **Orchestrator/Outbound**: Process check via `pgrep`

## 📊 Monitoramento

### Prometheus
- Configuração: `prometheus.yml`
- Scrape interval: 15s
- Endpoints: Prometheus próprio e OpenTelemetry Collector

### OpenTelemetry
- Configuração: `otel-collector-config.yaml`
- Recebe traces e metrics via OTLP
- Exporta para Jaeger (traces) e Prometheus (metrics)

### Logs
- **Promtail**: Coleta logs do sistema
- **Loki**: Armazena logs
- **Seq**: Logging estruturado (opcional)

## 🔒 Segurança

### Network Policies
As políticas de rede em `networkpolicies.yaml` restringem:
- Tráfego ingress apenas do Gateway para Inbound API
- Tráfego egress apenas para serviços necessários (PostgreSQL, Redis, RabbitMQ)

### Secrets
- Use Kubernetes Secrets ou ferramentas de gerenciamento de secrets
- Rotacione credenciais regularmente
- Use diferentes credenciais por ambiente

## 🚀 Próximos Passos

- [ ] Completar Helm Charts
- [ ] Adicionar ServiceMonitor para Prometheus
- [ ] Criar dashboards Grafana customizados
- [ ] Implementar mTLS entre serviços
- [ ] Adicionar deployments para infraestrutura (PostgreSQL, RabbitMQ, Redis) se necessário
- [ ] Configurar backups automáticos

