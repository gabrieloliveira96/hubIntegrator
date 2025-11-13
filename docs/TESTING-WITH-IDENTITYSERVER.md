# Testando com IdentityServer

## Por que usar IdentityServer para testes?

✅ **Vantagens:**
- Testa o fluxo completo de autenticação OIDC
- Gera tokens JWT reais e válidos
- Testa integração Gateway → IdentityServer → Backend
- Mais próximo do ambiente de produção
- Facilita testes de integração end-to-end

## Configuração Rápida

### 1. Habilitar IdentityServer

Edite os arquivos `appsettings.Development.json`:

**Gateway.Yarp/appsettings.Development.json:**
```json
{
  "Jwt": {
    "Authority": "http://localhost:5002",
    "Audience": "hub-api",
    "AllowDevelopmentWithoutAuthority": false
  }
}
```

**Inbound.Api/appsettings.Development.json:**
```json
{
  "Jwt": {
    "Authority": "http://localhost:5002",
    "Audience": "hub-api",
    "AllowDevelopmentWithoutAuthority": false
  }
}
```

### 2. Executar os serviços

```bash
# Terminal 1 - IdentityServer
cd src/IdentityServer
dotnet run

# Terminal 2 - Gateway
cd src/Gateway.Yarp
dotnet run

# Terminal 3 - Inbound.Api
cd src/Inbound.Api
dotnet run
```

### 3. Obter Token JWT

#### Opção A: Via TokenController (mais fácil)

```bash
curl -X POST http://localhost:5002/api/token/obter \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "hub-client",
    "clientSecret": "hub-secret",
    "scopes": ["hub.api.write", "hub.api.read"]
  }'
```

#### Opção B: Via endpoint OIDC padrão

```bash
curl -X POST http://localhost:5002/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=hub-client&client_secret=hub-secret&scope=hub.api.write hub.api.read"
```

### 4. Testar a API

```bash
TOKEN="<seu-token-jwt>"

curl -X POST http://localhost:5000/api/requests \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "X-Nonce: $(uuidgen)" \
  -H "X-Timestamp: $(date -u +%s)" \
  -d '{
    "partnerCode": "PARTNER01",
    "type": "ORDER",
    "payload": "{\"orderId\":\"12345\"}"
  }'
```

## Configuração sem IdentityServer

Se preferir testar sem autenticação (desenvolvimento rápido):

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

Neste modo, o Gateway e Inbound.Api funcionam sem validação de token.

## Endpoints do IdentityServer

- **Discovery**: `http://localhost:5002/.well-known/openid-configuration`
- **Token (OIDC)**: `http://localhost:5002/connect/token`
- **Token (Simplificado)**: `http://localhost:5002/api/token/obter`
- **Info**: `http://localhost:5002/api/token/info`
- **Swagger**: `http://localhost:5002/swagger`

## Clientes Configurados

### hub-client (Client Credentials)
- **ClientId**: `hub-client`
- **ClientSecret**: `hub-secret`
- **Scopes**: `hub.api.write`, `hub.api.read`
- **Grant Type**: `client_credentials`

## Scripts de Teste

Veja também:
- `test-without-auth.ps1` - Teste sem autenticação
- `token-request-example.json` - Exemplo de requisição de token

