# ADR 005: Headers para Idempotência e Anti-Replay

## Status
✅ Aceito

## Contexto
Precisávamos implementar dois mecanismos de segurança críticos:
1. **Idempotência**: Garantir que requisições duplicadas não sejam processadas múltiplas vezes
2. **Anti-Replay**: Prevenir ataques de replay (reutilização de requisições antigas)

## Decisão
Utilizar headers HTTP customizados:
- **`Idempotency-Key`**: Chave única para garantir idempotência
- **`X-Nonce`**: Valor único para prevenir replay attacks
- **`X-Timestamp`**: Timestamp Unix para validar janela de tempo

## Razões da Decisão

### 1. Padrão da Indústria
- ✅ Usado por Stripe (`Idempotency-Key`), PayPal, AWS, GitHub
- ✅ Prática amplamente adotada e reconhecida

### 2. Separação de Concerns
- ✅ Headers são metadados, não dados de negócio
- ✅ Não polui o payload da requisição

### 3. Observabilidade
- ✅ Fácil de inspecionar em logs, proxies, traces
- ✅ Headers são facilmente visíveis em ferramentas de debug

### 4. Performance
- ✅ Não polui o payload, melhor para cache
- ✅ Headers são processados antes do body

## Alternativas Consideradas

### ❌ No Body da Requisição
Rejeitado porque mistura concerns e não é padrão da indústria.

### ❌ Query Parameters
Rejeitado por questões de segurança e observabilidade (aparecem em logs/URLs).

### ❌ Apenas JWT
Rejeitado porque não resolve idempotência e adiciona complexidade.

## Implementação

### Headers Obrigatórios
```
Idempotency-Key: <GUID>
X-Nonce: <GUID>
X-Timestamp: <Unix timestamp>
```

### Validação
- **Idempotency-Key**: Armazenado no Redis com lock distribuído
- **X-Nonce**: Armazenado no Redis com TTL de 5 minutos
- **X-Timestamp**: Validado contra tempo do servidor (±5 minutos)

## Consequências
- **Positivas:**
  - Padrão da indústria
  - Separação de concerns
  - Fácil observabilidade
  - Não polui payload

- **Negativas:**
  - Requer que clientes enviem headers adicionais
  - Pode ser esquecido por desenvolvedores
  - Swagger não suporta headers customizados facilmente

## Melhorias Futuras
1. Suporte a RFC quando disponível
2. Melhorar experiência no Swagger
3. SDK/Client Libraries que adicionam headers automaticamente

## Referências
- [Stripe Idempotency Keys](https://stripe.com/docs/api/idempotent_requests)
- [PayPal Request ID](https://developer.paypal.com/docs/api/overview/#api-request-id)
- [Idempotency Key RFC Draft](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/)

