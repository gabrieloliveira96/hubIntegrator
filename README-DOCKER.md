# Executando com Docker Compose

Este guia mostra como executar todo o Hub de Integração usando Docker Compose.

## Pré-requisitos

- Docker Desktop instalado e rodando
- Docker Compose v2 (incluído no Docker Desktop)

## Iniciar Tudo

### Opção 1: Usando Makefile

```bash
make up
```

### Opção 2: Usando Docker Compose diretamente

```bash
docker-compose -f deploy/docker-compose.yml up -d --build
```

Isso irá:
1. ✅ Construir as imagens dos serviços .NET
2. ✅ Subir toda a infraestrutura (PostgreSQL, RabbitMQ, Redis, etc.)
3. ✅ Subir todos os serviços da aplicação (Gateway, Inbound API, Workers)
4. ✅ Aplicar migrations automaticamente no startup

## Serviços Disponíveis

Após iniciar, os seguintes serviços estarão disponíveis:

| Serviço | URL | Descrição |
|---------|-----|-----------|
| Gateway | http://localhost:5000 | API Gateway (YARP) |
| Inbound API | http://localhost:5001 | API de recebimento de requisições |
| RabbitMQ UI | http://localhost:15672 | Interface de gerenciamento (guest/guest) |
| Jaeger | http://localhost:16686 | Visualização de traces |
| Grafana | http://localhost:3000 | Dashboards (admin/admin) |
| Prometheus | http://localhost:9090 | Métricas |

## Verificar Status

```bash
# Ver todos os containers
docker-compose -f deploy/docker-compose.yml ps

# Ver logs de um serviço específico
docker-compose -f deploy/docker-compose.yml logs -f inbound-api

# Ver logs de todos os serviços
docker-compose -f deploy/docker-compose.yml logs -f
```

## Parar Tudo

```bash
# Parar e remover containers
docker-compose -f deploy/docker-compose.yml down

# Parar e remover containers + volumes (CUIDADO: apaga dados!)
docker-compose -f deploy/docker-compose.yml down -v
```

## Reconstruir Imagens

Se você fez alterações no código e precisa reconstruir:

```bash
docker-compose -f deploy/docker-compose.yml build --no-cache
docker-compose -f deploy/docker-compose.yml up -d
```

## Aplicar Migrations Manualmente

As migrations são aplicadas automaticamente no startup, mas você pode aplicá-las manualmente:

```bash
# Inbound API
docker-compose -f deploy/docker-compose.yml exec inbound-api dotnet ef database update

# Orchestrator Worker
docker-compose -f deploy/docker-compose.yml exec orchestrator-worker dotnet ef database update

# Outbound Worker
docker-compose -f deploy/docker-compose.yml exec outbound-worker dotnet ef database update
```

## Testar o Fluxo

Após iniciar tudo, você pode testar o fluxo completo:

```powershell
# No Windows PowerShell
.\test-simple.ps1
```

Ou manualmente:

```bash
curl -X POST http://localhost:5001/requests \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "X-Nonce: $(uuidgen)" \
  -H "X-Timestamp: $(date +%s)" \
  -d '{
    "partnerCode": "PARTNER01",
    "type": "ORDER",
    "payload": "{\"orderId\":\"12345\",\"customerId\":\"CUST001\"}"
  }'
```

## Troubleshooting

### Containers não iniciam

1. Verifique se o Docker Desktop está rodando
2. Verifique os logs: `docker-compose -f deploy/docker-compose.yml logs`
3. Verifique se as portas não estão em uso

### Erro de conexão com banco de dados

Os serviços aguardam o PostgreSQL ficar saudável antes de iniciar. Se ainda assim houver erro:
- Verifique os logs do PostgreSQL: `docker-compose -f deploy/docker-compose.yml logs postgres`
- Verifique se as migrations foram aplicadas

### Erro de conexão com RabbitMQ

- Verifique se o RabbitMQ está saudável: `docker-compose -f deploy/docker-compose.yml ps rabbitmq`
- Verifique os logs: `docker-compose -f deploy/docker-compose.yml logs rabbitmq`

### Rebuild necessário

Se você alterou código e os containers não refletem as mudanças:
```bash
docker-compose -f deploy/docker-compose.yml down
docker-compose -f deploy/docker-compose.yml build --no-cache
docker-compose -f deploy/docker-compose.yml up -d
```

## Estrutura dos Containers

```
hub-postgres              → PostgreSQL (porta 5432)
hub-rabbitmq              → RabbitMQ (portas 5672, 15672)
hub-redis                 → Redis (porta 6379)
hub-otel-collector        → OpenTelemetry Collector
hub-jaeger                → Jaeger (porta 16686)
hub-prometheus            → Prometheus (porta 9090)
hub-grafana               → Grafana (porta 3000)
hub-loki                  → Loki (porta 3100)
hub-promtail              → Promtail
hub-gateway               → Gateway.Yarp (porta 5000)
hub-inbound-api           → Inbound.Api (porta 5001)
hub-orchestrator-worker   → Orchestrator.Worker
hub-outbound-worker       → Outbound.Worker
```

## Variáveis de Ambiente

As variáveis de ambiente são configuradas no `docker-compose.yml`. Para desenvolvimento local, você pode criar um arquivo `.env` ou modificar diretamente o docker-compose.yml.

## Próximos Passos

- [ ] Adicionar health checks mais robustos
- [ ] Configurar restart policies mais granulares
- [ ] Adicionar resource limits
- [ ] Configurar secrets management
- [ ] Adicionar service discovery

