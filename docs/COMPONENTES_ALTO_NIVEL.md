# Componentes de Alto Nível - Hub de Integração

**Versão:** 1.0  
**Data:** Novembro 2025  
**Propósito:** Documentação dos componentes de alto nível para desenho de arquitetura

---

## 1. Componentes de Aplicação

### 1.1 Gateway

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-gateway` |
| **Service Name** | `gateway` |
| **Description** | YARP Reverse Proxy - API Gateway centralizado com autenticação OIDC/JWT, rate limiting e roteamento reverso |
| **Technology** | .NET 8 |
| **Type** | Web API (ASP.NET Core) |
| **Port** | 5000 (host) → 8080 (container) |
| **Framework** | YARP (Yet Another Reverse Proxy) 2.2.0 |
| **Main Responsibilities** | Autenticação JWT, Rate Limiting, Reverse Proxy, Correlation ID propagation |
| **Dependencies** | IdentityServer (opcional), Inbound.Api, OpenTelemetry Collector, Seq |
| **Health Check** | `/healthz`, `/readyz` |

**Principais Funcionalidades:**
- Validação de tokens JWT via IdentityServer
- Rate limiting por parceiro (Token Bucket: 100 tokens, 10/segundo)
- Roteamento reverso para serviços backend
- Propagação de Correlation ID
- Respostas JSON detalhadas para erros de autenticação

**Tecnologias Principais:**
- YARP.ReverseProxy 2.2.0
- Microsoft.AspNetCore.Authentication.JwtBearer 8.0.4
- Serilog (structured logging)
- OpenTelemetry (observability)

---

### 1.2 IdentityServer

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-identityserver` |
| **Service Name** | `identityserver` |
| **Description** | Provedor de autenticação OIDC - Emissão de tokens JWT para clientes autorizados |
| **Technology** | .NET 8 |
| **Type** | Web API (ASP.NET Core) |
| **Port** | 5002 (host) → 8080 (container) |
| **Framework** | Duende.IdentityServer 7.0.0 |
| **Main Responsibilities** | Emissão de tokens JWT, OIDC Discovery, Client Management |
| **Dependencies** | OpenTelemetry Collector, Seq |
| **Health Check** | `/healthz` |

**Principais Funcionalidades:**
- Emissão de tokens JWT
- OIDC Discovery endpoint (`.well-known/openid-configuration`)
- Gerenciamento de clientes (hub-client)
- Scopes: `hub.api.read`, `hub.api.write`
- Audience: `hub-api`

**Tecnologias Principais:**
- Duende.IdentityServer 7.0.0
- Serilog (structured logging)
- OpenTelemetry (observability)

---

### 1.3 Inbound.Api

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-inbound-api` |
| **Service Name** | `inbound-api` |
| **Description** | API de Recepção - Recebe requisições de parceiros, valida idempotência e anti-replay, persiste na Inbox e publica eventos |
| **Technology** | .NET 8 |
| **Type** | Web API (ASP.NET Core Minimal APIs) |
| **Port** | 5001 (host) → 8080 (container) |
| **Architecture** | Clean Architecture (Presentation, Application, Domain, Infrastructure) |
| **Framework** | ASP.NET Core 8, MediatR (CQRS), MassTransit, Entity Framework Core |
| **Main Responsibilities** | Recepção de requisições, Validação de idempotência, Anti-replay, Inbox Pattern, Publicação de eventos |
| **Dependencies** | PostgreSQL (inbound_db), Redis, RabbitMQ, IdentityServer (opcional), OpenTelemetry Collector, Seq |
| **Health Check** | `/healthz` |

**Principais Funcionalidades:**
- Endpoint REST para receber requisições (`POST /requests`)
- Validação de idempotência via `Idempotency-Key` header + Redis lock
- Validação anti-replay via `X-Nonce` e `X-Timestamp` headers
- Persistência na tabela `Requests`
- Inbox Pattern para garantir processamento idempotente
- Publicação de evento `RequestReceived` no RabbitMQ
- Consulta de requisições (`GET /requests/{id}`)

**Tecnologias Principais:**
- MediatR 13.1.0 (CQRS)
- MassTransit.RabbitMQ 8.2.3 (mensageria)
- Entity Framework Core 8.0.4 + Npgsql (PostgreSQL)
- StackExchange.Redis 2.8.16 (locks distribuídos)
- Serilog (structured logging)
- OpenTelemetry (observability)

**Banco de Dados (PostgreSQL - `inbound_db`):**
- `Requests` - Requisições recebidas
- `DedupKeys` - Chaves de idempotência
- `Nonces` - Valores nonce para anti-replay
- `Inbox` - Mensagens recebidas (Inbox Pattern)

---

### 1.4 Orchestrator.Worker

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-orchestrator-worker` |
| **Service Name** | `orchestrator-worker` |
| **Description** | Worker de Orquestração - Orquestra o fluxo de processamento via Saga Pattern (MassTransit State Machine), valida regras de negócio e publica comandos |
| **Technology** | .NET 8 |
| **Type** | Background Worker Service |
| **Port** | N/A (não expõe HTTP) |
| **Framework** | MassTransit (Saga Pattern), Entity Framework Core |
| **Main Responsibilities** | Orquestração distribuída, Validação de regras de negócio, Saga State Machine, Publicação de comandos |
| **Dependencies** | PostgreSQL (orchestrator_db), RabbitMQ, OpenTelemetry Collector, Seq |
| **Health Check** | N/A (background service) |

**Principais Funcionalidades:**
- Consome evento `RequestReceived` do RabbitMQ
- Inicia Saga (MassTransit State Machine)
- Valida e enriquece dados de negócio
- Gerencia estados da saga (Initial → Processing → Succeeded/Failed)
- Publica comando `DispatchToPartner` no RabbitMQ
- **Outbox Pattern (MassTransit nativo):** MassTransit automaticamente persiste mensagens na Outbox dentro da mesma transação do DbContext ao publicar eventos
- Consome eventos `RequestCompleted` e `RequestFailed` para finalizar saga

**Outbox Pattern - Implementação:**
- **Tecnologia:** MassTransit.EntityFrameworkCore (suporte nativo)
- **Como funciona:** Quando `context.Publish()` é chamado, MassTransit automaticamente:
  1. Persiste a mensagem na tabela `Outbox` dentro da mesma transação
  2. Um worker interno do MassTransit processa mensagens não publicadas
  3. Publica no RabbitMQ e marca como `Published = true`
- **Garantia:** At-least-once delivery (mensagem é publicada mesmo se o serviço cair após salvar no banco)

**Tecnologias Principais:**
- MassTransit.RabbitMQ 8.2.3 (mensageria e saga)
- MassTransit.EntityFrameworkCore 8.2.3 (persistência de saga)
- Entity Framework Core 8.0.4 + Npgsql (PostgreSQL)
- Polly 8.3.1 (resiliência)
- Serilog (structured logging)
- OpenTelemetry (observability)

**Banco de Dados (PostgreSQL - `orchestrator_db`):**
- `Sagas` - Estados das sagas (MassTransit State Machine)
- `Outbox` - Mensagens a serem publicadas (Outbox Pattern)

**Estados da Saga:**
- `Initial` - Estado inicial
- `Processing` - Processando requisição
- `Succeeded` - Requisição completada com sucesso
- `Failed` - Requisição falhou

---

### 1.5 Outbound.Worker

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-outbound-worker` |
| **Service Name** | `outbound-worker` |
| **Description** | Worker de Disparo Externo - Consome comandos do RabbitMQ e dispara requisições HTTP para APIs externas com resiliência (Polly) e Outbox Pattern |
| **Technology** | .NET 8 |
| **Type** | Background Worker Service |
| **Port** | N/A (não expõe HTTP) |
| **Framework** | MassTransit, HttpClientFactory, Polly, Entity Framework Core |
| **Main Responsibilities** | Chamadas HTTP externas, Resiliência (retry, circuit breaker), Outbox Pattern, Publicação de eventos de resultado |
| **Dependencies** | PostgreSQL (outbound_db), RabbitMQ, APIs Externas (HTTP/HTTPS), OpenTelemetry Collector, Seq |
| **Health Check** | N/A (background service) |

**Principais Funcionalidades:**
- Consome comando `DispatchToPartner` do RabbitMQ
- Dispara requisições HTTP/HTTPS para APIs de parceiros externos
- Políticas de resiliência (Polly):
  - Retry: 3 tentativas com backoff exponencial (1s, 2s, 4s)
  - Circuit Breaker: Abre após 5 falhas, fecha após 30s
  - Timeout: 30 segundos
- **Outbox Pattern (manual):** Persiste mensagens na Outbox antes de publicar, com worker dedicado (`OutboxDispatcher`) que processa mensagens não publicadas
- Publica eventos `RequestCompleted` ou `RequestFailed` no RabbitMQ

**Outbox Pattern - Implementação:**
- **Tecnologia:** Implementação manual com `OutboxDispatcher` (BackgroundService)
- **Como funciona:**
  1. Consumer salva mensagem na tabela `Outbox` manualmente dentro da transação
  2. Publica imediatamente no RabbitMQ (opcional, para reduzir latência)
  3. `OutboxDispatcher` roda a cada 5 segundos:
     - Lê mensagens com `Published = false` da tabela `Outbox`
     - Publica no RabbitMQ
     - Marca como `Published = true` e `PublishedAt = DateTimeOffset.UtcNow`
  4. Se o serviço cair antes de publicar, o dispatcher garante publicação na próxima execução
- **Garantia:** At-least-once delivery (mensagem é publicada mesmo se o serviço cair após salvar no banco)

**Tecnologias Principais:**
- MassTransit.RabbitMQ 8.2.3 (mensageria)
- Entity Framework Core 8.0.4 + Npgsql (PostgreSQL)
- Polly 8.3.1 + Polly.Extensions.Http 3.0.0 (resiliência HTTP)
- HttpClientFactory (chamadas HTTP)
- Serilog (structured logging)
- OpenTelemetry (observability)

**Banco de Dados (PostgreSQL - `outbound_db`):**
- `Outbox` - Mensagens a serem publicadas (Outbox Pattern)

---

## 2. Componentes de Infraestrutura

### 2.1 PostgreSQL

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-postgres` |
| **Service Name** | `postgres` |
| **Description** | Banco de Dados Relacional - Armazena dados de todos os serviços (Requests, Sagas, Outbox, Inbox) |
| **Technology** | PostgreSQL 16 Alpine |
| **Type** | Database |
| **Port** | 5432 |
| **Version** | postgres:16-alpine |
| **Main Responsibilities** | Persistência de dados, Transações ACID, Outbox/Inbox Pattern |
| **Databases** | `inbound_db`, `orchestrator_db`, `outbound_db` |

**Bancos de Dados:**
- **`inbound_db`** (Inbound.Api)
  - `Requests` - Requisições recebidas
  - `DedupKeys` - Chaves de idempotência
  - `Nonces` - Valores nonce para anti-replay
  - `Inbox` - Mensagens recebidas (Inbox Pattern)

- **`orchestrator_db`** (Orchestrator.Worker)
  - `Sagas` - Estados das sagas (MassTransit State Machine)
  - `Outbox` - Mensagens a serem publicadas (Outbox Pattern)

- **`outbound_db`** (Outbound.Worker)
  - `Outbox` - Mensagens a serem publicadas (Outbox Pattern)

**Configuração:**
- User: `postgres`
- Password: `postgres`
- Network: `hub-network`

---

### 2.2 RabbitMQ

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-rabbitmq` |
| **Service Name** | `rabbitmq` |
| **Description** | Message Broker - Mensageria assíncrona para comunicação entre serviços (eventos e comandos) |
| **Technology** | RabbitMQ 3 Management Alpine |
| **Type** | Message Broker |
| **Port AMQP** | 5672 |
| **Port Management UI** | 15672 |
| **Version** | rabbitmq:3-management-alpine |
| **Main Responsibilities** | Mensageria assíncrona, Pub/Sub, Queues, Exchanges |
| **Management UI** | `http://localhost:15672` (guest/guest) |

**Exchanges/Queues:**
- `RequestReceived` - Evento publicado por Inbound.Api, consumido por Orchestrator.Worker
- `DispatchToPartner` - Comando publicado por Orchestrator.Worker, consumido por Outbound.Worker
- `RequestCompleted` - Evento publicado por Outbound.Worker, consumido por Orchestrator.Worker e Inbound.Api
- `RequestFailed` - Evento publicado por Outbound.Worker, consumido por Orchestrator.Worker e Inbound.Api

**Configuração:**
- User: `guest`
- Password: `guest`
- Network: `hub-network`

---

### 2.3 Redis

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-redis` |
| **Service Name** | `redis` |
| **Description** | In-Memory Data Store - Locks distribuídos para garantir idempotência |
| **Technology** | Redis 7 Alpine |
| **Type** | Cache / Distributed Lock |
| **Port** | 6379 |
| **Version** | redis:7-alpine |
| **Main Responsibilities** | Locks distribuídos para idempotência, Cache (futuro) |

**Uso Atual:**
- **Locks Distribuídos:** Para garantir idempotência (evitar processamento duplicado)
- **Chave:** `idempotency:lock:{idempotency-key}`
- **TTL:** 5 segundos

**Configuração:**
- Network: `hub-network`

---

### 2.4 OpenTelemetry Collector

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-otel-collector` |
| **Service Name** | `otel-collector` |
| **Description** | Telemetry Collector - Coleta traces e métricas dos serviços e exporta para Jaeger e Prometheus |
| **Technology** | OpenTelemetry Collector |
| **Type** | Observability |
| **Port OTLP gRPC** | 4317 |
| **Port OTLP HTTP** | 4318 |
| **Port Metrics** | 8888 |
| **Version** | otel/opentelemetry-collector:latest |
| **Main Responsibilities** | Coleta de traces OTLP, Coleta de métricas OTLP, Exportação para Jaeger e Prometheus |

**Funcionalidades:**
- Recebe traces OTLP dos serviços (.NET 8)
- Recebe métricas OTLP dos serviços
- Exporta traces para Jaeger
- Exporta métricas para Prometheus

**Configuração:**
- Network: `hub-network`

---

### 2.5 Jaeger

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-jaeger` |
| **Service Name** | `jaeger` |
| **Description** | Distributed Tracing System - Visualização de traces distribuídos para análise de performance e debugging |
| **Technology** | Jaeger All-in-One |
| **Type** | Observability (Tracing) |
| **Port UI** | 16686 |
| **Port OTLP gRPC** | 4317 |
| **Port OTLP HTTP** | 4318 |
| **Version** | jaegertracing/all-in-one:latest |
| **Main Responsibilities** | Armazenamento de traces, Visualização de traces distribuídos, Análise de performance |

**Acesso:**
- UI: `http://localhost:16686`

**Configuração:**
- Network: `hub-network`

---

### 2.6 Prometheus

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-prometheus` |
| **Service Name** | `prometheus` |
| **Description** | Metrics Database - Coleta e armazena métricas dos serviços para análise e alertas |
| **Technology** | Prometheus |
| **Type** | Observability (Metrics) |
| **Port** | 9090 |
| **Version** | prom/prometheus:latest |
| **Main Responsibilities** | Coleta de métricas, Armazenamento de métricas, Query Language (PromQL) |

**Acesso:**
- UI: `http://localhost:9090`

**Configuração:**
- Network: `hub-network`

---

### 2.7 Grafana

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-grafana` |
| **Service Name** | `grafana` |
| **Description** | Analytics and Monitoring Platform - Visualização de métricas e logs em dashboards |
| **Technology** | Grafana |
| **Type** | Observability (Visualization) |
| **Port** | 3000 |
| **Version** | grafana/grafana:latest |
| **Main Responsibilities** | Dashboards de métricas, Visualização de logs, Alertas |
| **Data Sources** | Prometheus (métricas), Loki (logs) |

**Acesso:**
- UI: `http://localhost:3000`
- User: `admin`
- Password: `admin`

**Configuração:**
- Network: `hub-network`

---

### 2.8 Seq

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-seq` |
| **Service Name** | `seq` |
| **Description** | Structured Log Server - Armazenamento e busca de logs estruturados dos serviços |
| **Technology** | Seq |
| **Type** | Observability (Logging) |
| **Port HTTP** | 5341 |
| **Port Ingestion API** | 5342 |
| **Version** | datalust/seq:latest |
| **Main Responsibilities** | Armazenamento de logs estruturados, Query language para busca, Dashboards |

**Acesso:**
- UI: `http://localhost:5341`
- Default Password: `Admin@123`

**Configuração:**
- Network: `hub-network`

---

### 2.9 Loki + Promtail

| Propriedade | Valor |
|-------------|-------|
| **ContainerName** | `hub-loki`, `hub-promtail` |
| **Service Name** | `loki`, `promtail` |
| **Description** | Log Aggregation System - Coleta de logs de containers e aplicações para visualização no Grafana |
| **Technology** | Loki, Promtail |
| **Type** | Observability (Log Aggregation) |
| **Port Loki** | 3100 |
| **Version** | grafana/loki:latest, grafana/promtail:latest |
| **Main Responsibilities** | Agregação de logs, Envio de logs para Loki, Integração com Grafana |

**Configuração:**
- Network: `hub-network`

---

## 3. Resumo por Categoria

### 3.1 Aplicações (.NET 8)

| Componente | ContainerName | Port | Type |
|-------------|---------------|------|------|
| Gateway | `hub-gateway` | 5000 | Web API |
| IdentityServer | `hub-identityserver` | 5002 | Web API |
| Inbound.Api | `hub-inbound-api` | 5001 | Web API |
| Orchestrator.Worker | `hub-orchestrator-worker` | - | Background Worker |
| Outbound.Worker | `hub-outbound-worker` | - | Background Worker |

### 3.2 Infraestrutura de Dados

| Componente | ContainerName | Port | Type |
|-------------|---------------|------|------|
| PostgreSQL | `hub-postgres` | 5432 | Database |
| RabbitMQ | `hub-rabbitmq` | 5672, 15672 | Message Broker |
| Redis | `hub-redis` | 6379 | Cache/Lock |

### 3.3 Observabilidade

| Componente | ContainerName | Port | Type |
|-------------|---------------|------|------|
| OpenTelemetry Collector | `hub-otel-collector` | 4317, 4318, 8888 | Telemetry Collector |
| Jaeger | `hub-jaeger` | 16686 | Tracing |
| Prometheus | `hub-prometheus` | 9090 | Metrics |
| Grafana | `hub-grafana` | 3000 | Visualization |
| Seq | `hub-seq` | 5341 | Logging |
| Loki | `hub-loki` | 3100 | Log Aggregation |
| Promtail | `hub-promtail` | - | Log Shipper |

---

## 4. Dependências entre Componentes

### 4.1 Gateway
- **Depende de:** IdentityServer (opcional), Inbound.Api, OpenTelemetry Collector, Seq

### 4.2 IdentityServer
- **Depende de:** OpenTelemetry Collector, Seq

### 4.3 Inbound.Api
- **Depende de:** PostgreSQL (`inbound_db`), Redis, RabbitMQ, IdentityServer (opcional), OpenTelemetry Collector, Seq

### 4.4 Orchestrator.Worker
- **Depende de:** PostgreSQL (`orchestrator_db`), RabbitMQ, OpenTelemetry Collector, Seq

### 4.5 Outbound.Worker
- **Depende de:** PostgreSQL (`outbound_db`), RabbitMQ, APIs Externas (HTTP/HTTPS), OpenTelemetry Collector, Seq

### 4.6 OpenTelemetry Collector
- **Depende de:** Jaeger, Prometheus

### 4.7 Grafana
- **Depende de:** Prometheus, Loki

### 4.8 Promtail
- **Depende de:** Loki

---

## 5. Network

| Network | Driver | Description |
|---------|--------|-------------|
| `hub-network` | bridge | Rede interna para comunicação entre containers |

---

**Fim do Documento**

