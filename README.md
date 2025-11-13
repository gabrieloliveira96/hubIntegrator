# Hub de Integração e Orquestração

Monorepo de referência para um Hub de Integração e Orquestração construído com .NET 8, focado em robustez, rastreabilidade e escalabilidade.

## 📚 Documentação Técnica Completa

**Para a documentação técnica completa da arquitetura, consulte:**

👉 **[Proposta de Arquitetura TOTVS - Hub de Integração](./docs/Proposta_Arquitetura_TOTVS_HubIntegracao.md)**

A documentação inclui:
- Visão geral da arquitetura com diagramas
- Decisões de arquitetura e justificativas técnicas
- Fluxo completo de dados e processos
- Estratégias de resiliência e observabilidade
- Segurança e governança
- Deploy e escalabilidade
- Matriz de requisitos não funcionais

**Para gerar o PDF:**
```powershell
.\docs\generate-pdf.ps1
```
Ou consulte [docs/GERAR-PDF.md](./docs/GERAR-PDF.md) para outras opções.

## Arquitetura

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ HTTPS + JWT
       ▼
┌─────────────────┐
│  Gateway.Yarp   │ (Rate Limiting, OIDC)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Inbound.Api    │ (Minimal APIs, Inbox Pattern)
└────────┬────────┘
         │
         │ RequestReceived
         ▼
┌─────────────────────┐
│ Orchestrator.Worker │ (MassTransit Saga)
└────────┬────────────┘
         │
         │ DispatchToPartner
         ▼
┌─────────────────┐
│ Outbound.Worker │ (HttpClientFactory + Polly)
└────────┬────────┘
         │
         │ RequestCompleted/Failed
         ▼
    [Third Party APIs]
```

### Componentes Principais

- **Gateway.Yarp**: API Gateway com rate limiting e autenticação OIDC
- **Inbound.Api**: Recebe requisições, valida idempotência e anti-replay, persiste na Inbox
- **Orchestrator.Worker**: Orquestra o fluxo via Saga State Machine (MassTransit)
- **Outbound.Worker**: Executa chamadas externas com resiliência (Polly) e Outbox pattern
- **Shared**: Contratos, observabilidade, persistência e segurança compartilhados

### Padrões Implementados

- **Outbox Pattern**: Garantia de publicação at-least-once
- **Inbox Pattern**: Idempotência de consumo de mensagens
- **Saga Pattern**: Orquestração distribuída com compensação
- **Circuit Breaker**: Proteção contra falhas em cascata
- **Retry com Backoff**: Resiliência em chamadas externas
- **Idempotência**: Via chave única + Redis lock distribuído
- **Anti-Replay**: Validação de nonce + timestamp

## Pré-requisitos

- .NET 8 SDK
- Docker Desktop (para docker-compose)
- Make (opcional, ou execute comandos manualmente)

## Como Rodar

### 1. Subir a infraestrutura

```bash
make up
# ou
docker-compose -f deploy/docker-compose.yml up -d
```

Isso sobe:
- PostgreSQL (porta 5432)
- RabbitMQ (porta 5672, UI em 15672)
- Redis (porta 6379)
- OpenTelemetry Collector
- Jaeger (UI em 16686)
- Prometheus (porta 9090)
- Grafana (porta 3000)
- Loki + Promtail

### 2. Aplicar migrations

```bash
make seed
```

### 3. Executar os serviços

#### Opção A: Com IdentityServer (recomendado para testes completos)

```bash
# Terminal 1 - IdentityServer (opcional, mas recomendado para testes)
cd src/IdentityServer
dotnet run

# Terminal 2 - Gateway
cd src/Gateway.Yarp
dotnet run

# Terminal 3 - Inbound
cd src/Inbound.Api
dotnet run

# Terminal 4 - Orchestrator
cd src/Orchestrator.Worker
dotnet run

# Terminal 5 - Outbound
cd src/Outbound.Worker
dotnet run
```

#### Opção B: Sem IdentityServer (desenvolvimento rápido)

Edite `appsettings.Development.json` e defina:
```json
{
  "Jwt": {
    "Authority": "",
    "AllowDevelopmentWithoutAuthority": true
  }
}
```

Veja [docs/TESTING-WITH-IDENTITYSERVER.md](docs/TESTING-WITH-IDENTITYSERVER.md) para mais detalhes.

### 4. Testar

```bash
# Obter JWT (ver seção Segurança)
TOKEN="<seu-jwt-token>"

# Criar requisição
curl -X POST http://localhost:5000/api/requests \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "X-Nonce: $(uuidgen)" \
  -H "X-Timestamp: $(date -u +%s)" \
  -d '{
    "partnerCode": "PARTNER01",
    "type": "ORDER",
    "payload": {"orderId": "12345"}
  }'

# Consultar status
curl http://localhost:5000/api/requests/{correlationId} \
  -H "Authorization: Bearer $TOKEN"
```

## Endpoints

### Gateway (porta 5000)
- `GET /healthz` - Health check
- `GET /readyz` - Readiness check
- `POST /api/requests` - Criar requisição (proxied para Inbound)

### Inbound.Api (porta 5001)
- `GET /healthz` - Health check
- `GET /readyz` - Readiness check
- `POST /requests` - Criar requisição
- `GET /requests/{id}` - Consultar status

## Observabilidade

### Jaeger
- URL: http://localhost:16686
- Visualizar traces distribuídos

### Grafana
- URL: http://localhost:3000
- Credenciais padrão: admin/admin
- Dashboards: ASP.NET Core, RabbitMQ, PostgreSQL

### Prometheus
- URL: http://localhost:9090
- Métricas expostas em `/metrics`

### RabbitMQ Management
- URL: http://localhost:15672
- Credenciais: guest/guest

## Segurança

### Autenticação para Testes

**Recomendado: Use IdentityServer para testes completos**

O IdentityServer está disponível como serviço opcional. Para habilitar:

1. Execute o IdentityServer: `cd src/IdentityServer && dotnet run`
2. Obtenha um token:
   ```bash
   curl -X POST http://localhost:5002/api/token/obter \
     -H "Content-Type: application/json" \
     -d '{"clientId":"hub-client","clientSecret":"hub-secret"}'
   ```
3. Use o token nas requisições

**Alternativa: Desenvolvimento sem autenticação**

Configure `appsettings.Development.json` com `AllowDevelopmentWithoutAuthority: true` para testar sem IdentityServer.

Veja [docs/TESTING-WITH-IDENTITYSERVER.md](docs/TESTING-WITH-IDENTITYSERVER.md) para guia completo.

### Headers Obrigatórios

- `Authorization: Bearer <jwt>` - Token OIDC válido
- `Idempotency-Key: <uuid>` - Chave de idempotência
- `X-Nonce: <uuid>` - Nonce único (anti-replay)
- `X-Timestamp: <unix-timestamp>` - Timestamp da requisição

## Testes

```bash
make test
# ou
dotnet test src/IntegrationHub.sln
```

Os testes de integração usam Testcontainers para criar instâncias isoladas de PostgreSQL e RabbitMQ.

## Kubernetes

Manifests estão em `deploy/k8s/`:

```bash
kubectl apply -f deploy/k8s/namespace.yaml
kubectl apply -f deploy/k8s/
```

## Limites & Próximos Passos

### Limitações Atuais
- mTLS interno: apenas placeholders de configuração
- Pact testing: não implementado
- Helm charts: apenas skeleton
- Seed de dados: precisa ser implementado

### Próximos Passos
- [ ] Implementar seed de dados de teste
- [ ] Adicionar Pact testing para contratos
- [ ] Completar Helm charts
- [ ] Adicionar mTLS real entre serviços
- [ ] Implementar ServiceMonitor para Prometheus
- [ ] Adicionar dashboards Grafana customizados
- [x] Autenticação OIDC centralizada no Gateway (YARP)

## Estrutura do Repositório

```
/
├── docs/              # Documentação e ADRs
├── deploy/            # Docker Compose e K8s manifests
├── src/               # Código fonte
│   ├── Gateway.Yarp/
│   ├── Inbound.Api/
│   ├── Orchestrator.Worker/
│   ├── Outbound.Worker/
│   ├── Shared/
│   └── Tests/
└── README.md
```

## Licença

MIT

