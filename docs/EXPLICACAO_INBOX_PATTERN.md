# O que é o Inbox Pattern?

## Resumo

O **Inbox Pattern** é um padrão arquitetural usado para garantir **idempotência no consumo de mensagens** em sistemas distribuídos. Ele evita que a mesma mensagem seja processada múltiplas vezes, mesmo que seja entregue mais de uma vez pelo message broker.

---

## Problema que Resolve

### Cenário sem Inbox Pattern

Imagine que você recebe uma mensagem do RabbitMQ:

```
1. RabbitMQ entrega mensagem "RequestCompleted" para Inbound.Api
2. Inbound.Api processa e atualiza status da Request
3. ❌ Serviço cai ANTES de confirmar (ACK) ao RabbitMQ
4. RabbitMQ reenvia a mensagem (pensando que não foi processada)
5. ❌ Inbound.Api processa NOVAMENTE → Duplicação!
```

**Resultado:** A mesma requisição é atualizada duas vezes, causando inconsistências.

### Solução com Inbox Pattern

```
1. RabbitMQ entrega mensagem "RequestCompleted"
2. Inbound.Api verifica se MessageId já existe na tabela Inbox
3. Se NÃO existe → Salva na Inbox e processa
4. Se JÁ existe → Ignora (idempotência)
5. Confirma (ACK) ao RabbitMQ
```

**Resultado:** Mesmo que a mensagem seja reenviada, ela não é processada novamente.

---

## Como Funciona

### 1. Quando uma Mensagem é Recebida

**Passo 1: Verificar se já foi processada**
```sql
SELECT * FROM "Inbox" 
WHERE MessageId = @messageId AND Processed = true
```

**Passo 2: Se não existe, salvar na Inbox ANTES de processar**
```sql
INSERT INTO "Inbox" 
(Id, MessageId, MessageType, Payload, ReceivedAt, Processed, CorrelationId)
VALUES 
(@Id, @MessageId, @MessageType, @Payload, @ReceivedAt, false, @CorrelationId)
```

**Passo 3: Processar a mensagem** (atualizar Request, etc.)

**Passo 4: Marcar como processada**
```sql
UPDATE "Inbox" 
SET Processed = true, ProcessedAt = @ProcessedAt
WHERE MessageId = @MessageId
```

### 2. Se a Mensagem Chegar Novamente

```sql
-- Mensagem já existe na Inbox com Processed = true
SELECT * FROM "Inbox" WHERE MessageId = @messageId
-- Resultado: Já processada → IGNORA
```

---

## Implementação no Projeto

### Onde é Usado?

**Inbound.Api** usa Inbox Pattern para processar eventos `RequestCompleted` e `RequestFailed` de forma idempotente.

### Estrutura da Tabela Inbox

```csharp
public class InboxMessage
{
    public Guid Id { get; set; }
    public string MessageId { get; set; }        // ← ÚNICO (evita duplicatas)
    public string MessageType { get; set; }      // Ex: "RequestCompleted"
    public string Payload { get; set; }          // JSON da mensagem
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public bool Processed { get; set; }          // ← Flag de processamento
    public string? CorrelationId { get; set; }   // Para rastreabilidade
}
```

**Índices:**
- `MessageId` → **UNIQUE** (garante que não há duplicatas)
- `(Processed, ReceivedAt)` → Para queries eficientes
- `CorrelationId` → Para rastreabilidade

### Fluxo no Código

#### 1. Quando Inbound.Api RECEBE uma requisição (POST /requests)

```csharp
// ReceiveRequestHandler.cs
public async Task<ReceiveRequestCommandResponse> Handle(...)
{
    // ... valida idempotência ...
    
    // Cria Request
    var requestEntity = new Request { ... };
    await _requestRepository.CreateAsync(requestEntity);
    
    // Persiste na Inbox (ANTES de publicar)
    var inboxMessage = new InboxMessage
    {
        Id = Guid.NewGuid(),
        MessageId = Guid.NewGuid().ToString(),  // ← ID único da mensagem
        MessageType = "RequestReceived",
        Payload = JsonSerializer.Serialize(new { ... }),
        ReceivedAt = DateTimeOffset.UtcNow,
        Processed = false,
        CorrelationId = correlationId.ToString()
    };
    
    await _inboxRepository.AddAsync(inboxMessage);
    
    // Publica evento no RabbitMQ
    await _mqPublisher.PublishRequestReceivedAsync(requestEntity);
}
```

**Nota:** Neste caso, a Inbox é usada para rastrear mensagens que **enviamos**, não que recebemos. Mas o padrão é o mesmo.

#### 2. Quando Inbound.Api RECEBE um evento (RequestCompleted)

```csharp
// RequestStatusUpdateConsumer.cs
public async Task Consume(ConsumeContext<RequestCompleted> context)
{
    var message = context.Message;
    
    // ⚠️ ATENÇÃO: No código atual, não há verificação explícita da Inbox aqui
    // Mas o padrão deveria ser:
    
    // 1. Verificar se MessageId já foi processado
    var messageId = context.MessageId.ToString();
    var existingInbox = await _inboxRepository.GetByMessageIdAsync(messageId);
    
    if (existingInbox != null && existingInbox.Processed)
    {
        // Já processado → Ignora (idempotência)
        _logger.LogInformation("Message {MessageId} already processed, skipping", messageId);
        return;
    }
    
    // 2. Salvar na Inbox ANTES de processar
    var inboxMessage = new InboxMessage
    {
        MessageId = messageId,
        MessageType = "RequestCompleted",
        Payload = JsonSerializer.Serialize(message),
        ReceivedAt = DateTimeOffset.UtcNow,
        Processed = false,
        CorrelationId = message.CorrelationId.ToString()
    };
    await _inboxRepository.AddAsync(inboxMessage);
    
    // 3. Processar (atualizar Request)
    var request = await _requestRepository.GetByCorrelationIdAsync(message.CorrelationId);
    request.Status = "Completed";
    await _requestRepository.UpdateAsync(request);
    
    // 4. Marcar como processada
    inboxMessage.Processed = true;
    inboxMessage.ProcessedAt = DateTimeOffset.UtcNow;
    await _inboxRepository.UpdateAsync(inboxMessage);
}
```

**Nota:** O código atual do `RequestStatusUpdateConsumer` não implementa a verificação da Inbox explicitamente, mas o padrão está disponível na infraestrutura.

---

## Diferença: Outbox vs Inbox

### Outbox Pattern (Publicação)

**Problema:** Garantir que mensagens sejam **publicadas** mesmo se o serviço cair.

**Como funciona:**
1. Salva mensagem na tabela `Outbox` dentro da transação
2. Publica no RabbitMQ
3. Se o serviço cair antes de publicar, um worker garante publicação posterior

**Garantia:** **At-least-once delivery** (mensagem é publicada pelo menos uma vez)

**Usado em:**
- `Orchestrator.Worker` → Publica `DispatchToPartner`
- `Outbound.Worker` → Publica `RequestCompleted` / `RequestFailed`

### Inbox Pattern (Consumo)

**Problema:** Garantir que mensagens sejam **processadas apenas uma vez**, mesmo se chegarem múltiplas vezes.

**Como funciona:**
1. Ao receber mensagem, verifica se `MessageId` já existe na `Inbox`
2. Se não existe → Salva na `Inbox` e processa
3. Se já existe → Ignora (idempotência)

**Garantia:** **Exactly-once processing** (mensagem é processada exatamente uma vez)

**Usado em:**
- `Inbound.Api` → Processa `RequestCompleted` / `RequestFailed`

---

## Comparação Visual

### Outbox Pattern (Publicação)

```
Serviço A                    Outbox (DB)              RabbitMQ
   │                            │                        │
   ├─ Salva mensagem ──────────►│                        │
   │   (transação)              │                        │
   │                            │                        │
   ├─ Publica ──────────────────────────────────────────►│
   │                            │                        │
   │  [Se cair aqui]            │                        │
   │                            │                        │
   │                            ├─ Worker processa ─────►│
   │                            │   (garante publicação) │
```

### Inbox Pattern (Consumo)

```
RabbitMQ                    Inbox (DB)              Serviço B
   │                            │                        │
   ├─ Entrega mensagem ────────────────────────────────►│
   │                            │                        │
   │                            │◄─ Verifica MessageId ─┤
   │                            │   (já processada?)     │
   │                            │                        │
   │                            ├─ Salva (se nova) ─────┤
   │                            │                        │
   │                            │                        ├─ Processa
   │                            │                        │
   │                            │◄─ Marca Processed ────┤
   │                            │                        │
   │  [Se reenviar]             │                        │
   │                            │                        │
   │                            ├─ Já processada ───────►│ (ignora)
```

---

## Vantagens do Inbox Pattern

✅ **Idempotência:** Mesma mensagem pode chegar múltiplas vezes sem causar problemas  
✅ **Consistência:** Evita processamento duplicado  
✅ **Rastreabilidade:** Histórico de todas as mensagens recebidas  
✅ **Debugging:** Fácil identificar mensagens não processadas  
✅ **Tolerância a Falhas:** Se o serviço cair, pode reprocessar mensagens não processadas

---

## Desvantagens

❌ **Overhead:** Tabela adicional no banco de dados  
❌ **Latência:** Verificação adicional antes de processar  
❌ **Manutenção:** Precisa limpar mensagens antigas periodicamente

---

## Quando Usar?

### Use Inbox Pattern quando:

- ✅ Você precisa garantir processamento exatamente uma vez
- ✅ O message broker pode entregar mensagens duplicadas (at-least-once)
- ✅ O processamento tem efeitos colaterais (ex: atualizar banco, chamar APIs)
- ✅ Você precisa de rastreabilidade de mensagens recebidas

### Não precisa quando:

- ❌ O processamento é idempotente por natureza
- ❌ Mensagens duplicadas não causam problemas
- ❌ O message broker garante exactly-once delivery (raro)

---

## Implementação no Projeto - Resumo

### Tabela Inbox

**Banco:** `inbound_db` (PostgreSQL)  
**Tabela:** `Inbox`

**Campos principais:**
- `MessageId` (UNIQUE) → Identificador único da mensagem
- `MessageType` → Tipo da mensagem (ex: "RequestCompleted")
- `Payload` → JSON da mensagem
- `Processed` → Flag indicando se foi processada
- `CorrelationId` → Para rastreabilidade

### Uso Atual

1. **Quando recebe requisição (POST /requests):**
   - Salva na Inbox antes de publicar `RequestReceived`
   - Rastreia mensagens que enviamos

2. **Quando recebe eventos (RequestCompleted/RequestFailed):**
   - ⚠️ **Nota:** O código atual não verifica Inbox explicitamente no Consumer
   - Mas a infraestrutura está pronta para isso

### Melhoria Sugerida

O `RequestStatusUpdateConsumer` poderia ser melhorado para verificar a Inbox antes de processar:

```csharp
public async Task Consume(ConsumeContext<RequestCompleted> context)
{
    var messageId = context.MessageId.ToString();
    
    // Verificar Inbox
    var existing = await _inboxRepository.GetByMessageIdAsync(messageId);
    if (existing != null && existing.Processed)
    {
        _logger.LogInformation("Message {MessageId} already processed", messageId);
        return; // Idempotência
    }
    
    // Salvar na Inbox
    var inboxMessage = new InboxMessage { ... };
    await _inboxRepository.AddAsync(inboxMessage);
    
    // Processar...
    // ...
    
    // Marcar como processada
    inboxMessage.Processed = true;
    await _inboxRepository.UpdateAsync(inboxMessage);
}
```

---

## Conclusão

O **Inbox Pattern** é essencial para garantir **idempotência no consumo de mensagens** em sistemas distribuídos. Ele complementa o **Outbox Pattern** (que garante publicação) para criar um sistema robusto e confiável.

**Resumo:**
- **Outbox Pattern** → Garante que mensagens sejam **publicadas** (at-least-once)
- **Inbox Pattern** → Garante que mensagens sejam **processadas apenas uma vez** (exactly-once processing)

Ambos trabalham juntos para criar um sistema resiliente! 🎯




