# Fluxo de Criação de Request

## Resumo
**SIM**, o handler está gerando o registro no banco de dados **ANTES** de retornar o `correlationId`.

## Fluxo Detalhado

### 1. ReceiveRequestHandler.Handle()
```csharp
// Linha 66: Chama CreateRequestAsync
var correlationId = await _idempotencyStore.CreateRequestAsync(
    request.IdempotencyKey,
    request.PartnerCode,
    request.Type,
    request.Payload,
    cancellationToken);
```

### 2. IdempotencyStore.CreateRequestAsync()
```csharp
// Linha 72: Gera novo correlationId
var correlationId = Guid.NewGuid();

// Linha 75-85: Cria objeto Request
var request = new Request
{
    Id = Guid.NewGuid(),
    CorrelationId = correlationId.ToString(),
    PartnerCode = partnerCode,
    Type = type,
    Payload = payload,
    IdempotencyKey = idempotencyKey,
    Status = "Received",
    CreatedAt = now
};

// Linha 87-93: Cria DedupKey para idempotência
var dedupKey = new DedupKey { ... };

// Linha 95-96: Adiciona ao DbContext
_dbContext.Requests.Add(request);
_dbContext.DedupKeys.Add(dedupKey);

// ⭐ LINHA 98: SALVA NO BANCO DE DADOS ⭐
await _dbContext.SaveChangesAsync(cancellationToken);

// Linha 103: Retorna correlationId
return correlationId;
```

### 3. ReceiveRequestHandler.Handle() (continuação)
```csharp
// Linha 123: Salva mensagem no Inbox
await _inboxRepository.AddAsync(inboxMessage, cancellationToken);

// Linha 126: Busca novamente para garantir que existe
var requestEntity = await _requestRepository.GetByCorrelationIdAsync(
    correlationId, cancellationToken);

// Linha 134: Publica mensagem no RabbitMQ
await _mqPublisher.PublishRequestReceivedAsync(requestEntity, cancellationToken);

// Linha 138-143: Retorna resposta com correlationId
return new ReceiveRequestCommandResponse
{
    CorrelationId = correlationId,
    Status = "Received",
    CreatedAt = requestEntity.CreatedAt
};
```

## Pontos Importantes

### ✅ O registro é salvo ANTES de retornar
- O `SaveChangesAsync()` acontece na **linha 98** do `IdempotencyStore`
- O `correlationId` só é retornado na **linha 103** (depois do save)
- Você pode consultar imediatamente após receber o `correlationId`

### ✅ Transação atômica
- O `Request` e o `DedupKey` são salvos na mesma transação
- Se houver erro, nada é salvo (rollback automático)

### ✅ Verificação de existência
- Na linha 126, o handler busca novamente o registro
- Isso garante que o registro existe antes de publicar a mensagem
- Se não encontrar, lança exceção

## Tabelas Afetadas

1. **Requests** - Registro principal da request
   - Salvo na linha 98 do `IdempotencyStore`

2. **DedupKeys** - Chave de idempotência
   - Salvo na mesma transação (linha 98)

3. **Inbox** - Mensagem recebida (Inbox Pattern)
   - Salvo na linha 123 do `ReceiveRequestHandler`

## Consulta Imediata

Após receber o `correlationId` na resposta do POST, você pode consultar imediatamente:

```powershell
# Exemplo de uso
$response = Invoke-RestMethod -Uri "http://localhost:5001/requests" -Method Post ...
$correlationId = $response.correlationId

# Consulta imediata - vai funcionar!
$request = Invoke-RestMethod -Uri "http://localhost:5001/requests/$correlationId" -Method Get
```

## Diagrama de Sequência

```
Cliente
  │
  ├─> POST /requests
  │     │
  │     ├─> ReceiveRequestHandler.Handle()
  │     │     │
  │     │     ├─> IdempotencyStore.CreateRequestAsync()
  │     │     │     │
  │     │     │     ├─> Cria Request object
  │     │     │     ├─> Cria DedupKey object
  │     │     │     ├─> ⭐ SaveChangesAsync() ⭐
  │     │     │     │     └─> [BANCO DE DADOS]
  │     │     │     │
  │     │     │     └─> Retorna correlationId
  │     │     │
  │     │     ├─> Salva InboxMessage
  │     │     ├─> Busca Request novamente (verificação)
  │     │     ├─> Publica mensagem no RabbitMQ
  │     │     │
  │     │     └─> Retorna correlationId
  │     │
  └─<─ 202 Accepted { correlationId: "..." }
        │
        └─> GET /requests/{correlationId}
              │
              └─> ✅ Request encontrada no banco!
```

## Conclusão

O registro **é salvo no banco de dados** antes de retornar o `correlationId`. Você pode consultar imediatamente após receber a resposta do POST.

