# Interações Arquiteturais - Hub de Integração

**Versão:** 1.0  
**Data:** Novembro 2025  
**Propósito:** Documentação técnica detalhada das interações entre componentes para desenho de arquitetura

---

## 1. Visão Geral das Interações

Este documento descreve **exatamente** como cada componente se comunica com os outros, incluindo:
- Protocolos utilizados (HTTP, AMQP, SQL)
- Métodos/Operações executadas
- Dados trafegados
- Operações de banco de dados
- Fluxo completo passo a passo

---

## 2. Fluxo Principal - Requisição Bem-Sucedida

### 2.1 Cliente → Gateway.Yarp

**Protocolo:** HTTP/HTTPS  
**Método:** POST  
**Endpoint:** `http://localhost:5000/api/requests`  
**Headers:**
```
Authorization: Bearer <jwt-token>
Idempotency-Key: <guid>
X-Nonce: <guid>
X-Timestamp: <unix-timestamp>
Content-Type: application/json
```

**Body:**
```json
{
  "partnerCode": "PARTNER01",
  "type": "ORDER",
  "payload": "{\"orderId\":\"12345\"}"
}
```

**O que acontece no Gateway.Yarp:**
1. **Middleware de Autenticação** (`AuthenticationMiddleware.cs`)
   - Valida JWT token usando `context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)`
   - Verifica scopes (`hub.api.write` ou `hub.api.read`)
   - Se inválido → Retorna 401 JSON detalhado
   - Se válido → Adiciona headers `X-Authenticated-User` e `X-Partner-Code`

2. **Rate Limiter**
   - Extrai `partner_code` do JWT claim ou header
   - Aplica Token Bucket (100 tokens, 10/segundo)
   - Se excedido → Retorna 429

3. **YARP Reverse Proxy**
   - Roteia requisição para `http://localhost:5001/requests`
   - Transforma path: `/api/requests` → `/requests`
   - Adiciona `X-Correlation-Id` header (se disponível)

**Resposta:** Proxy reverso (requisição é encaminhada)

---

### 2.2 Gateway.Yarp → Inbound.Api

**Protocolo:** HTTP  
**Método:** POST  
**Endpoint:** `http://localhost:5001/requests`  
**Headers:** (mesmos do cliente, mais headers adicionados pelo Gateway)

**O que acontece no Inbound.Api:**

#### 2.2.1 Endpoint Handler (`RequestsEndpoints.CreateRequest`)

1. **Extrai Idempotency-Key do header**
   ```csharp
   var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
   ```
   - Se vazio → Retorna 400 Bad Request

2. **Cria Command** (`ReceiveRequestCommandWithIdempotency`)
   ```csharp
   var command = new ReceiveRequestCommandWithIdempotency(
       dto.PartnerCode,
       dto.Type,
       dto.Payload,
       idempotencyKey);
   ```

3. **Envia Command via MediatR**
   ```csharp
   var response = await mediator.Send(command, cancellationToken);
   ```
   - MediatR roteia para `ReceiveRequestHandler`

---

#### 2.2.2 Command Handler (`ReceiveRequestHandler.Handle`)

**Passo 1: Validação de Idempotência**

1. **Chama `IIdempotencyStore.GetExistingCorrelationIdAsync`**
   - **Implementação:** `IdempotencyStore.GetExistingCorrelationIdAsync`
   - **Operação Redis:**
     ```csharp
     // Tenta obter lock distribuído
     var lockKey = $"idempotency:{idempotencyKey}";
     var lockValue = await _redis.StringSetAsync(lockKey, correlationId, TimeSpan.FromMinutes(5), When.NotExists);
     ```
   - **Operação PostgreSQL:**
     ```sql
     SELECT CorrelationId FROM DedupKeys WHERE Key = @idempotencyKey
     ```
   - Se existe → Retorna `CorrelationId` existente (idempotência)
   - Se não existe → Continua processamento

**Passo 2: Criação de Nova Requisição**

2. **Gera novo CorrelationId**
   ```csharp
   var correlationId = Guid.NewGuid();
   ```

3. **Cria entidade Request**
   ```csharp
   var requestEntity = new Request
   {
       Id = Guid.NewGuid(),
       CorrelationId = correlationId.ToString(),
       PartnerCode = request.PartnerCode,
       Type = request.Type,
       Payload = request.Payload,
       IdempotencyKey = request.IdempotencyKey,
       Status = "Received",
       CreatedAt = DateTimeOffset.UtcNow
   };
   ```

4. **Salva Request no PostgreSQL**
   - **Chama:** `IRequestRepository.CreateAsync`
   - **Implementação:** `RequestRepository.CreateAsync`
   - **Operação PostgreSQL:**
     ```sql
     INSERT INTO "Requests" 
     (Id, CorrelationId, PartnerCode, Type, Payload, IdempotencyKey, Status, CreatedAt)
     VALUES 
     (@Id, @CorrelationId, @PartnerCode, @Type, @Payload, @IdempotencyKey, @Status, @CreatedAt)
     ```
   - **Tabela:** `Requests` (banco `inbound_db`)

5. **Cria DedupKey para idempotência**
   - **Chama:** `IIdempotencyStore.CreateDedupKeyAsync`
   - **Implementação:** `IdempotencyStore.CreateDedupKeyAsync`
   - **Operação PostgreSQL:**
     ```sql
     INSERT INTO "DedupKeys" (Id, Key, CorrelationId, CreatedAt)
     VALUES (@Id, @Key, @CorrelationId, @CreatedAt)
     ```
   - **Tabela:** `DedupKeys` (banco `inbound_db`)

6. **Persiste na Inbox (Inbox Pattern)**
   - **Chama:** `IInboxRepository.AddAsync`
   - **Implementação:** `InboxRepository.AddAsync`
   - **Cria InboxMessage:**
     ```csharp
     var inboxMessage = new InboxMessage
     {
         Id = Guid.NewGuid(),
         MessageId = Guid.NewGuid().ToString(),
         MessageType = "RequestReceived",
         Payload = JsonSerializer.Serialize(new { ... }),
         ReceivedAt = DateTimeOffset.UtcNow,
         CorrelationId = correlationId.ToString()
     };
     ```
   - **Operação PostgreSQL:**
     ```sql
     INSERT INTO "Inbox" 
     (Id, MessageId, MessageType, Payload, ReceivedAt, Processed, CorrelationId)
     VALUES 
     (@Id, @MessageId, @MessageType, @Payload, @ReceivedAt, false, @CorrelationId)
     ```
   - **Tabela:** `Inbox` (banco `inbound_db`)

7. **Publica evento no RabbitMQ**
   - **Chama:** `IMqPublisher.PublishRequestReceivedAsync`
   - **Implementação:** `MqPublisher.PublishRequestReceivedAsync`
   - **Usa MassTransit:**
     ```csharp
     var message = new RequestReceived(
         CorrelationId: Guid.Parse(request.CorrelationId),
         PartnerCode: request.PartnerCode,
         Type: request.Type,
         Payload: JsonSerializer.Deserialize<JsonElement>(request.Payload),
         CreatedAt: request.CreatedAt
     );
     
     await _publishEndpoint.Publish(message, cancellationToken);
     ```
   - **Protocolo:** AMQP (RabbitMQ)
   - **Exchange/Queue:** `RequestReceived`
   - **Mensagem:** Event `RequestReceived` serializado como JSON

8. **Retorna resposta**
   ```csharp
   return new ReceiveRequestCommandResponse
   {
       CorrelationId = correlationId,
       Status = "Received",
       CreatedAt = requestEntity.CreatedAt
   };
   ```

**Resposta HTTP:** 202 Accepted
```json
{
  "correlationId": "<guid>",
  "status": "Received",
  "createdAt": "2025-01-15T10:30:00Z"
}
```

---

### 2.3 Orchestrator.Worker → RabbitMQ (Consumo)

**Protocolo:** AMQP (RabbitMQ)  
**Operação:** CONSUME  
**Queue:** `RequestReceived`

**O que acontece no Orchestrator.Worker:**

#### 2.3.1 Saga State Machine (`RequestSaga`)

1. **MassTransit recebe evento `RequestReceived`**
   - **Consumer:** MassTransit State Machine automaticamente
   - **Correlação:** Por `CorrelationId` da mensagem

2. **Inicia nova Saga (estado Initial)**
   - **Cria instância de Saga:**
     ```csharp
     context.Saga.CorrelationId = context.Message.CorrelationId;
     context.Saga.PartnerCode = context.Message.PartnerCode;
     context.Saga.RequestType = context.Message.Type;
     context.Saga.Payload = context.Message.Payload.ToString();
     context.Saga.CreatedAt = context.Message.CreatedAt;
     context.Saga.CurrentState = "Initial";
     ```

3. **Salva Saga no PostgreSQL**
   - **Operação PostgreSQL:**
     ```sql
     INSERT INTO "Sagas" 
     (CorrelationId, CurrentState, PartnerCode, RequestType, Payload, CreatedAt, RowVersion)
     VALUES 
     (@CorrelationId, 'Initial', @PartnerCode, @RequestType, @Payload, @CreatedAt, @RowVersion)
     ```
   - **Tabela:** `Sagas` (banco `orchestrator_db`)

4. **Valida regras de negócio**
   - **Chama:** `IBusinessRulesService.ValidateRequestAsync`
   - **Implementação:** `BusinessRulesService.ValidateRequestAsync`
   - Valida `PartnerCode` e `Type`
   - Enriquece dados se necessário

5. **Transiciona para estado `Processing`**
   - **Atualiza Saga:**
     ```csharp
     context.Saga.CurrentState = "Processing";
     ```
   - **Operação PostgreSQL:**
     ```sql
     UPDATE "Sagas" 
     SET CurrentState = 'Processing', UpdatedAt = @UpdatedAt, RowVersion = @RowVersion
     WHERE CorrelationId = @CorrelationId
     ```

6. **Publica comando `DispatchToPartner`**
   - **Usa MassTransit:**
     ```csharp
     await context.Publish(new DispatchToPartner(
         context.Saga.CorrelationId,
         context.Saga.PartnerCode,
         new Uri($"http://localhost:8080/mock-partner/{context.Saga.PartnerCode}"),
         JsonSerializer.Deserialize<JsonElement>(context.Saga.Payload ?? "{}")
     ));
     ```
   - **Protocolo:** AMQP (RabbitMQ)
   - **Exchange/Queue:** `DispatchToPartner`
   - **Mensagem:** Command `DispatchToPartner` serializado como JSON

7. **Persiste na Outbox (Outbox Pattern)**
   - **MassTransit automaticamente:**
     - Cria `OutboxMessage` com `MessageType = "DispatchToPartner"`
     - **Operação PostgreSQL:**
       ```sql
       INSERT INTO "Outbox" 
       (Id, MessageId, MessageType, Payload, CreatedAt, Published, CorrelationId)
       VALUES 
       (@Id, @MessageId, @MessageType, @Payload, @CreatedAt, false, @CorrelationId)
       ```
     - **Tabela:** `Outbox` (banco `orchestrator_db`)
   - **MassTransit Outbox Publisher:**
     - Lê mensagens não publicadas da `Outbox`
     - Publica no RabbitMQ
     - Marca como `Published = true`

---

### 2.4 Outbound.Worker → RabbitMQ (Consumo)

**Protocolo:** AMQP (RabbitMQ)  
**Operação:** CONSUME  
**Queue:** `DispatchToPartner`

**O que acontece no Outbound.Worker:**

#### 2.4.1 Consumer (`DispatchToPartnerConsumer.Consume`)

1. **MassTransit recebe comando `DispatchToPartner`**
   - **Consumer:** `DispatchToPartnerConsumer`
   - **Método:** `Consume(ConsumeContext<DispatchToPartner> context)`

2. **Chama API externa via HTTP**
   - **Chama:** `IThirdPartyClient.SendRequestAsync`
   - **Implementação:** `ThirdPartyClient.SendRequestAsync`
   - **Protocolo:** HTTP/HTTPS
   - **Método:** POST
   - **Endpoint:** `command.Endpoint` (ex: `http://localhost:8080/mock-partner/PARTNER01`)
   - **Body:** `command.Payload` (JSON)
   - **Políticas de Resiliência (Polly):**
     - **Retry:** 3 tentativas com backoff exponencial (1s, 2s, 4s)
     - **Circuit Breaker:** Abre após 5 falhas, fecha após 30s
     - **Timeout:** 30 segundos

3. **Se sucesso (HTTP 200-299):**
   - **Cria evento `RequestCompleted`:**
     ```csharp
     var completedEvent = new RequestCompleted(
         command.CorrelationId,
         command.PartnerCode,
         response.StatusCode,
         "Completed",
         response.Response
     );
     ```

4. **Persiste na Outbox**
   - **Cria OutboxMessage:**
     ```csharp
     var outboxMessage = new OutboxMessage
     {
         Id = Guid.NewGuid(),
         MessageType = "RequestCompleted",
         Payload = JsonSerializer.Serialize(completedEvent),
         CreatedAt = DateTimeOffset.UtcNow,
         CorrelationId = command.CorrelationId.ToString()
     };
     ```
   - **Operação PostgreSQL:**
     ```sql
     INSERT INTO "Outbox" 
     (Id, MessageId, MessageType, Payload, CreatedAt, Published, CorrelationId)
     VALUES 
     (@Id, @MessageId, @MessageType, @Payload, @CreatedAt, false, @CorrelationId)
     ```
   - **Tabela:** `Outbox` (banco `outbound_db`)

5. **Salva no banco (transação)**
   ```csharp
   _dbContext.Outbox.Add(outboxMessage);
   await _dbContext.SaveChangesAsync(context.CancellationToken);
   ```

6. **Publica evento `RequestCompleted`**
   - **Usa MassTransit:**
     ```csharp
     await _publishEndpoint.Publish(completedEvent, context.CancellationToken);
     ```
   - **Protocolo:** AMQP (RabbitMQ)
   - **Exchange/Queue:** `RequestCompleted`
   - **Mensagem:** Event `RequestCompleted` serializado como JSON

7. **MassTransit Outbox Publisher:**
   - Lê mensagem da `Outbox` (já publicada, mas garante at-least-once)
   - Marca como `Published = true`

---

### 2.5 Orchestrator.Worker → RabbitMQ (Consumo de RequestCompleted)

**Protocolo:** AMQP (RabbitMQ)  
**Operação:** CONSUME  
**Queue:** `RequestCompleted`

**O que acontece no Orchestrator.Worker:**

#### 2.5.1 Saga State Machine (`RequestSaga` - Estado Processing)

1. **MassTransit recebe evento `RequestCompleted`**
   - **Correlação:** Por `CorrelationId` da mensagem
   - **Estado atual:** `Processing`

2. **Transiciona para estado `Succeeded`**
   - **Atualiza Saga:**
     ```csharp
     context.Saga.CurrentState = "Succeeded";
     context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
     ```
   - **Operação PostgreSQL:**
     ```sql
     UPDATE "Sagas" 
     SET CurrentState = 'Succeeded', UpdatedAt = @UpdatedAt, RowVersion = @RowVersion
     WHERE CorrelationId = @CorrelationId
     ```
   - **Tabela:** `Sagas` (banco `orchestrator_db`)

3. **Saga finalizada** (estado final)

---

### 2.6 Inbound.Api → RabbitMQ (Consumo de RequestCompleted)

**Protocolo:** AMQP (RabbitMQ)  
**Operação:** CONSUME  
**Queue:** `RequestCompleted`

**O que acontece no Inbound.Api:**

#### 2.6.1 Consumer (`RequestStatusUpdateConsumer.Consume`)

1. **MassTransit recebe evento `RequestCompleted`**
   - **Consumer:** `RequestStatusUpdateConsumer`
   - **Método:** `Consume(ConsumeContext<RequestCompleted> context)`

2. **Valida Inbox (idempotência)**
   - **Verifica se mensagem já foi processada:**
     ```sql
     SELECT * FROM "Inbox" 
     WHERE MessageId = @messageId AND Processed = true
     ```
   - Se já processada → Ignora (idempotência)
   - Se não processada → Continua

3. **Atualiza status da Request**
   - **Chama:** `IRequestRepository.UpdateStatusAsync`
   - **Implementação:** `RequestRepository.UpdateStatusAsync`
   - **Operação PostgreSQL:**
     ```sql
     UPDATE "Requests" 
     SET Status = 'Completed', UpdatedAt = @UpdatedAt
     WHERE CorrelationId = @CorrelationId
     ```
   - **Tabela:** `Requests` (banco `inbound_db`)

4. **Marca Inbox como processada**
   - **Operação PostgreSQL:**
     ```sql
     UPDATE "Inbox" 
     SET Processed = true, ProcessedAt = @ProcessedAt
     WHERE MessageId = @MessageId
     ```
   - **Tabela:** `Inbox` (banco `inbound_db`)

---

## 3. Fluxo de Falha

### 3.1 Outbound.Worker → Falha na Chamada HTTP

**Cenário:** API externa retorna erro ou timeout

1. **Polly retry policy:**
   - Tenta 3 vezes com backoff exponencial
   - Se todas falharem → Lança exceção

2. **Catch da exceção:**
   ```csharp
   catch (Exception ex)
   {
       var failedEvent = new RequestFailed(
           command.CorrelationId,
           command.PartnerCode,
           ex.Message,
           null
       );
       
       await _publishEndpoint.Publish(failedEvent, context.CancellationToken);
   }
   ```

3. **Publica evento `RequestFailed`**
   - **Protocolo:** AMQP (RabbitMQ)
   - **Exchange/Queue:** `RequestFailed`
   - **Mensagem:** Event `RequestFailed` serializado como JSON

---

### 3.2 Orchestrator.Worker → Consumo de RequestFailed

1. **MassTransit recebe evento `RequestFailed`**
   - **Estado atual:** `Processing`

2. **Transiciona para estado `Failed`**
   - **Operação PostgreSQL:**
     ```sql
     UPDATE "Sagas" 
     SET CurrentState = 'Failed', UpdatedAt = @UpdatedAt, RowVersion = @RowVersion
     WHERE CorrelationId = @CorrelationId
     ```

---

### 3.3 Inbound.Api → Consumo de RequestFailed

1. **Atualiza status da Request para "Failed"**
   - **Operação PostgreSQL:**
     ```sql
     UPDATE "Requests" 
     SET Status = 'Failed', UpdatedAt = @UpdatedAt
     WHERE CorrelationId = @CorrelationId
     ```

---

## 4. Consulta de Requisição (GET)

### 4.1 Cliente → Gateway.Yarp → Inbound.Api

**Protocolo:** HTTP  
**Método:** GET  
**Endpoint:** `http://localhost:5000/api/requests/{id}`

**O que acontece:**

1. **Gateway.Yarp:** Roteia para `http://localhost:5001/requests/{id}`

2. **Inbound.Api Endpoint:** `RequestsEndpoints.GetRequest`
   - **Cria Query:** `GetRequestQuery(id)`
   - **Envia via MediatR:** `await mediator.Send(query)`

3. **Query Handler:** `GetRequestHandler.Handle`
   - **Chama:** `IRequestRepository.GetByCorrelationIdAsync`
   - **Operação PostgreSQL:**
     ```sql
     SELECT * FROM "Requests" 
     WHERE CorrelationId = @correlationId
     ```

4. **Retorna:** 200 OK com dados da Request

---

## 5. Resumo das Interações

### 5.1 Protocolos Utilizados

| Protocolo | Onde | Para que |
|-----------|------|----------|
| **HTTP/HTTPS** | Cliente ↔ Gateway ↔ Inbound.Api | API REST |
| **HTTP/HTTPS** | Outbound.Worker → APIs Externas | Chamadas para parceiros |
| **AMQP (RabbitMQ)** | Todos os serviços ↔ RabbitMQ | Mensageria assíncrona |
| **SQL (PostgreSQL)** | Todos os serviços ↔ PostgreSQL | Persistência de dados |
| **Redis Protocol** | Inbound.Api ↔ Redis | Locks distribuídos |

### 5.2 Operações de Banco de Dados

#### Inbound.Api → PostgreSQL (`inbound_db`)

| Operação | Tabela | Quando |
|----------|--------|--------|
| `INSERT` | `Requests` | Nova requisição recebida |
| `INSERT` | `DedupKeys` | Nova chave de idempotência |
| `INSERT` | `Inbox` | Mensagem recebida (Inbox Pattern) |
| `INSERT` | `Nonces` | Validação anti-replay |
| `UPDATE` | `Requests` | Atualização de status |
| `UPDATE` | `Inbox` | Marca mensagem como processada |
| `SELECT` | `Requests` | Consulta de requisição |
| `SELECT` | `DedupKeys` | Verificação de idempotência |
| `SELECT` | `Inbox` | Verificação de mensagem já processada |

#### Orchestrator.Worker → PostgreSQL (`orchestrator_db`)

| Operação | Tabela | Quando |
|----------|--------|--------|
| `INSERT` | `Sagas` | Nova saga iniciada |
| `UPDATE` | `Sagas` | Transição de estado |
| `INSERT` | `Outbox` | Mensagem a ser publicada (Outbox Pattern) |
| `UPDATE` | `Outbox` | Marca mensagem como publicada |

#### Outbound.Worker → PostgreSQL (`outbound_db`)

| Operação | Tabela | Quando |
|----------|--------|--------|
| `INSERT` | `Outbox` | Mensagem a ser publicada (Outbox Pattern) |
| `UPDATE` | `Outbox` | Marca mensagem como publicada |

### 5.3 Mensagens RabbitMQ

| Evento/Command | Publisher | Consumer | Descrição |
|----------------|-----------|----------|-----------|
| `RequestReceived` | Inbound.Api | Orchestrator.Worker | Nova requisição recebida |
| `DispatchToPartner` | Orchestrator.Worker | Outbound.Worker | Comando para disparar para parceiro |
| `RequestCompleted` | Outbound.Worker | Orchestrator.Worker, Inbound.Api | Requisição completada com sucesso |
| `RequestFailed` | Outbound.Worker | Orchestrator.Worker, Inbound.Api | Requisição falhou |

---

## 6. Diagrama de Sequência Simplificado

```
Cliente
  │
  │ POST /api/requests
  ▼
Gateway.Yarp
  │ (Valida JWT, Rate Limit)
  │ POST /requests (proxy)
  ▼
Inbound.Api
  │
  │ 1. Valida Idempotência (Redis + PostgreSQL)
  │ 2. INSERT Requests (PostgreSQL)
  │ 3. INSERT DedupKeys (PostgreSQL)
  │ 4. INSERT Inbox (PostgreSQL)
  │ 5. PUBLISH RequestReceived (RabbitMQ)
  │
  │ 202 Accepted
  │◄─────────────────┐
  │                  │
  ▼                  │
RabbitMQ             │
  │                  │
  │ RequestReceived  │
  ▼                  │
Orchestrator.Worker  │
  │                  │
  │ 1. INSERT Sagas (PostgreSQL)
  │ 2. Valida regras
  │ 3. UPDATE Sagas (PostgreSQL)
  │ 4. INSERT Outbox (PostgreSQL)
  │ 5. PUBLISH DispatchToPartner (RabbitMQ)
  │
  ▼
RabbitMQ
  │
  │ DispatchToPartner
  ▼
Outbound.Worker
  │
  │ 1. POST API Externa (HTTP)
  │ 2. INSERT Outbox (PostgreSQL)
  │ 3. PUBLISH RequestCompleted (RabbitMQ)
  │
  ▼
RabbitMQ
  │
  │ RequestCompleted
  ├─────────────────► Orchestrator.Worker
  │                     │ UPDATE Sagas (PostgreSQL)
  │                     │
  └─────────────────► Inbound.Api
                        │
                        │ 1. UPDATE Requests (PostgreSQL)
                        │ 2. UPDATE Inbox (PostgreSQL)
                        │
                        ▼
                      Fim
```

---

## 7. Detalhes Técnicos Importantes

### 7.1 Transações

- **Inbound.Api:** Cada operação (INSERT Requests, DedupKeys, Inbox) é feita em transação separada
- **Orchestrator.Worker:** Saga state é atualizado em transação com Outbox
- **Outbound.Worker:** Outbox é salvo em transação antes de publicar

### 7.2 Idempotência

- **Nível 1:** Redis lock distribuído (evita processamento simultâneo)
- **Nível 2:** DedupKeys table (verifica se já processou)
- **Nível 3:** Inbox Pattern (evita processar mensagem duplicada)

### 7.3 Garantias de Entrega

- **Outbox Pattern:** Garante publicação at-least-once
- **Inbox Pattern:** Garante processamento idempotente
- **MassTransit:** Retry automático em caso de falha

### 7.4 Observabilidade

- **Correlation ID:** Propagado em todos os serviços
- **OpenTelemetry:** Traces distribuídos
- **Serilog:** Structured logging em todos os serviços

---

**Fim do Documento**


