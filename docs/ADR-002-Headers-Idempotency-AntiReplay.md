# ADR-002: Headers para Idempotência e Anti-Replay

## Status
✅ Aceito (com possibilidade de revisão)

## Contexto

Precisávamos implementar dois mecanismos de segurança críticos:

1. **Idempotência**: Garantir que requisições duplicadas não sejam processadas múltiplas vezes
2. **Anti-Replay**: Prevenir ataques de replay (reutilização de requisições antigas)

## Decisão Atual

Utilizamos headers HTTP customizados:

- **`Idempotency-Key`**: Chave única para garantir idempotência
- **`X-Nonce`**: Valor único para prevenir replay attacks
- **`X-Timestamp`**: Timestamp Unix para validar janela de tempo

## Análise de Alternativas

### Opção 1: Headers Customizados (Atual) ✅

**Implementação:**
```
Idempotency-Key: <GUID>
X-Nonce: <GUID>
X-Timestamp: <Unix timestamp>
```

**Prós:**
- ✅ Simples e direto
- ✅ Não requer mudanças no body da requisição
- ✅ Headers são facilmente inspecionáveis em logs/proxies
- ✅ Padrão comum na indústria (Stripe, PayPal, etc.)
- ✅ Funciona bem com cache/CDN
- ✅ Não polui o payload da requisição

**Contras:**
- ⚠️ Requer que clientes enviem headers adicionais
- ⚠️ Pode ser esquecido por desenvolvedores
- ⚠️ Swagger não suporta headers customizados facilmente

**Exemplos na Indústria:**
- **Stripe**: `Idempotency-Key`
- **PayPal**: `PayPal-Request-Id`
- **AWS**: `X-Amz-Idempotency-Token`
- **GitHub**: `X-GitHub-Request-Id`

### Opção 2: Headers Padronizados (RFC)

**Implementação:**
```
Idempotency-Key: <GUID> (RFC em discussão)
X-Request-ID: <GUID>
X-Timestamp: <ISO 8601>
```

**Prós:**
- ✅ Mais padronizado
- ✅ Melhor suporte em ferramentas

**Contras:**
- ⚠️ RFC ainda não finalizado
- ⚠️ Menos adoção na indústria

### Opção 3: No Body da Requisição

**Implementação:**
```json
{
  "idempotencyKey": "...",
  "nonce": "...",
  "timestamp": "...",
  "partnerCode": "...",
  "type": "...",
  "payload": "..."
}
```

**Prós:**
- ✅ Tudo em um lugar
- ✅ Mais fácil para Swagger/testes

**Contras:**
- ❌ Polui o payload de negócio
- ❌ Mistura concerns (segurança vs. negócio)
- ❌ Não é padrão da indústria
- ❌ Mais difícil de inspecionar em proxies

### Opção 4: JWT Claims

**Implementação:**
```json
{
  "sub": "partner01",
  "jti": "<nonce>",
  "iat": <timestamp>,
  "idempotency_key": "<key>"
}
```

**Prós:**
- ✅ Integrado com autenticação
- ✅ Assinado e validado automaticamente

**Contras:**
- ❌ Requer JWT em todas as requisições
- ❌ Mais complexo
- ❌ Não resolve idempotência (JWT pode ser reutilizado)

### Opção 5: Query Parameters

**Implementação:**
```
POST /requests?idempotencyKey=...&nonce=...&timestamp=...
```

**Prós:**
- ✅ Simples

**Contras:**
- ❌ Aparece em logs/URLs
- ❌ Não é padrão
- ❌ Pode ser cacheado incorretamente
- ❌ Menos seguro (visível em logs)

## Comparação

| Aspecto | Headers (Atual) | No Body | JWT Claims | Query Params |
|---------|----------------|---------|------------|--------------|
| **Padrão Indústria** | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ⭐ |
| **Simplicidade** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ |
| **Segurança** | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ |
| **Observabilidade** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ |
| **Facilidade Teste** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ |
| **Separação Concerns** | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ |

## Decisão Final

**Escolhemos Headers Customizados** pelas seguintes razões:

1. ✅ **Padrão da Indústria**: Usado por Stripe, PayPal, AWS, GitHub
2. ✅ **Separação de Concerns**: Headers são metadados, não dados de negócio
3. ✅ **Observabilidade**: Fácil de inspecionar em logs, proxies, traces
4. ✅ **Performance**: Não polui o payload, melhor para cache
5. ✅ **Flexibilidade**: Pode ser adicionado/removido sem mudar contratos

## Melhorias Futuras

### 1. Suporte a RFC quando disponível
Quando o RFC para `Idempotency-Key` for finalizado, podemos migrar.

### 2. Melhorar Experiência no Swagger
- Adicionar suporte nativo para headers customizados
- Criar extensões do Swagger UI
- Documentação inline melhor

### 3. Middleware de Validação
Criar middleware que valida e retorna erros mais descritivos:
```csharp
if (missingHeaders.Any())
{
    return Results.BadRequest(new {
        error = "Missing required headers",
        missing = missingHeaders,
        example = new {
            IdempotencyKey = "550e8400-e29b-41d4-a716-446655440000",
            XNonce = "550e8400-e29b-41d4-a716-446655440001",
            XTimestamp = 1734048000
        }
    });
}
```

### 4. SDK/Client Libraries
Criar bibliotecas cliente que adicionam headers automaticamente:
```csharp
var client = new HubIntegrationClient();
client.SetIdempotencyKey(Guid.NewGuid());
var response = await client.CreateRequestAsync(...);
```

## Alternativas Consideradas e Rejeitadas

### ❌ No Body
Rejeitado porque mistura concerns e não é padrão da indústria.

### ❌ Query Parameters
Rejeitado por questões de segurança e observabilidade.

### ❌ Apenas JWT
Rejeitado porque não resolve idempotência e adiciona complexidade.

## Referências

- [Stripe Idempotency Keys](https://stripe.com/docs/api/idempotent_requests)
- [PayPal Request ID](https://developer.paypal.com/docs/api/overview/#api-request-id)
- [AWS Idempotency](https://docs.aws.amazon.com/AWSEC2/latest/APIReference/Run_Instance_Idempotency.html)
- [RFC 7231 - HTTP/1.1 Semantics](https://tools.ietf.org/html/rfc7231)
- [Idempotency Key RFC Draft](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/)

## Conclusão

A decisão de usar headers customizados está alinhada com as melhores práticas da indústria e oferece o melhor equilíbrio entre simplicidade, segurança e observabilidade. As melhorias futuras focarão em facilitar o uso (SDKs, melhor documentação) sem mudar a abordagem fundamental.

## Data da Decisão

2024-12-XX

## Revisão

Esta decisão deve ser revisada se:
- RFC para Idempotency-Key for finalizado
- Padrões da indústria mudarem significativamente
- Surgirem problemas de segurança ou usabilidade

