# Fluxo de Autenticação

## Arquitetura

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ 1. POST /api/token/obter (IdentityServer)
       ▼
┌─────────────────┐
│ IdentityServer   │ (porta 5002)
│ - Gera JWT       │
└──────┬───────────┘
       │ 2. Retorna JWT token
       ▼
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ 3. POST /api/requests + Authorization: Bearer <token>
       ▼
┌─────────────────┐
│  Gateway.Yarp   │ (porta 5000)
│ - Valida JWT    │ ✅ Verifica token válido
│ - Verifica scope│ ✅ Verifica hub.api.write/read
│ - Rate Limiting │ ✅ Aplica rate limit
└──────┬──────────┘
       │ 4. Roteia apenas se autenticado
       ▼
┌─────────────────┐
│  Inbound.Api    │ (porta 5001)
│ - Confia Gateway│ ✅ Tudo que chega está autenticado
│ - Processa      │
└─────────────────┘
```

## Fluxo Detalhado

### 1. Obter Token JWT

```bash
curl -X POST http://localhost:5002/api/token/obter \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "hub-client",
    "clientSecret": "hub-secret",
    "scopes": ["hub.api.write", "hub.api.read"]
  }'
```

**Resposta:**
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "scope": "hub.api.write hub.api.read"
}
```

### 2. Gateway Valida Token

O Gateway (YARP) faz as seguintes validações:

1. **Token JWT válido**: Verifica assinatura, expiração, issuer
2. **Scope necessário**: Verifica se o token tem `hub.api.write` ou `hub.api.read`
3. **Rate Limiting**: Aplica limite por partner_code (extraído do token)

**Se inválido:**
- Retorna `401 Unauthorized` ou `403 Forbidden`
- Não roteia para Inbound.Api

**Se válido:**
- Adiciona headers:
  - `X-Authenticated-User`: Nome do usuário autenticado
  - `X-Partner-Code`: Código do parceiro (se presente no token)
- Roteia para Inbound.Api

### 3. Inbound.Api Processa

O Inbound.Api **confia no Gateway**:
- Se a requisição chegou aqui, já foi autenticada
- Não precisa validar token novamente
- Pode extrair informações do header `X-Authenticated-User` ou `X-Partner-Code` se necessário

## Configuração

### Gateway.Yarp

**appsettings.Development.json:**
```json
{
  "Jwt": {
    "Authority": "http://localhost:5002",
    "Audience": "hub-api",
    "AllowDevelopmentWithoutAuthority": false
  }
}
```

### Inbound.Api

**appsettings.Development.json:**
```json
{
  "Jwt": {
    "Authority": "",
    "Audience": "hub-api",
    "AllowDevelopmentWithoutAuthority": true
  }
}
```

**Nota:** Inbound.Api não precisa validar JWT porque confia no Gateway.

## Testando

### 1. Sem Token (deve falhar)

```bash
curl -X POST http://localhost:5000/api/requests \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{"partnerCode":"PARTNER01","type":"ORDER","payload":"{}"}'
```

**Resposta esperada:** `401 Unauthorized`

### 2. Com Token Válido (deve funcionar)

```bash
# Obter token
TOKEN=$(curl -s -X POST http://localhost:5002/api/token/obter \
  -H "Content-Type: application/json" \
  -d '{"clientId":"hub-client","clientSecret":"hub-secret"}' \
  | jq -r '.accessToken')

# Fazer requisição
curl -X POST http://localhost:5000/api/requests \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "X-Nonce: $(uuidgen)" \
  -H "X-Timestamp: $(date -u +%s)" \
  -d '{"partnerCode":"PARTNER01","type":"ORDER","payload":"{\"orderId\":\"12345\"}"}'
```

**Resposta esperada:** `202 Accepted`

### 3. Com Token Inválido (deve falhar)

```bash
curl -X POST http://localhost:5000/api/requests \
  -H "Authorization: Bearer token-invalido" \
  -H "Content-Type: application/json" \
  -d '{"partnerCode":"PARTNER01","type":"ORDER","payload":"{}"}'
```

**Resposta esperada:** `401 Unauthorized`

## Segurança

✅ **Gateway valida tudo:**
- Token JWT válido
- Scope correto
- Rate limiting aplicado

✅ **Inbound.Api confia no Gateway:**
- Não precisa validar token novamente
- Reduz overhead de validação
- Simplifica código

✅ **Defesa em profundidade (opcional):**
- Inbound.Api pode validar token também se configurado
- Útil para acesso direto (sem Gateway) em desenvolvimento

## Troubleshooting

### Gateway retorna 401

1. Verifique se IdentityServer está rodando: `http://localhost:5002/healthz`
2. Verifique se o token é válido: decode em https://jwt.io
3. Verifique se `Jwt:Authority` está configurado no Gateway

### Gateway retorna 403

1. Verifique se o token tem o scope necessário: `hub.api.write` ou `hub.api.read`
2. Verifique a configuração do cliente no IdentityServer

### Inbound.Api recebe requisições não autenticadas

1. Verifique se está acessando via Gateway (porta 5000) e não diretamente (porta 5001)
2. Verifique se o Gateway está validando tokens corretamente

