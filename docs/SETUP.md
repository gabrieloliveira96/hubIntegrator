# Guia de Configuração

## Pré-requisitos

- .NET 8 SDK
- Docker Desktop
- Make (opcional)

## Configuração Inicial

### 1. Restaurar dependências

```bash
dotnet restore src/IntegrationHub.sln
```

### 2. Subir infraestrutura

```bash
make up
# ou
docker-compose -f deploy/docker-compose.yml up -d
```

### 3. Aplicar migrations

Para cada serviço que usa banco de dados:

```bash
# Inbound.Api
cd src/Inbound.Api
dotnet ef migrations add InitialCreate
dotnet ef database update

# Orchestrator.Worker
cd src/Orchestrator.Worker
dotnet ef migrations add InitialCreate
dotnet ef database update

# Outbound.Worker
cd src/Outbound.Worker
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Executar serviços

Em terminais separados:

```bash
# Terminal 1 - Gateway
cd src/Gateway.Yarp
dotnet run

# Terminal 2 - Inbound
cd src/Inbound.Api
dotnet run

# Terminal 3 - Orchestrator
cd src/Orchestrator.Worker
dotnet run

# Terminal 4 - Outbound
cd src/Outbound.Worker
dotnet run
```

## Configuração de Autenticação

O Gateway (YARP) centraliza a autenticação OIDC e rate limiting. Para configurar:

### Desenvolvimento

Por padrão, o Gateway está configurado para permitir desenvolvimento sem autenticação (`AllowDevelopmentWithoutAuthority: true`).

### Produção

Configure o provedor OIDC no Gateway através da configuração `Jwt:Authority`:

```json
{
  "Jwt": {
    "Authority": "https://seu-provedor-oidc.com",
    "Audience": "hub-api",
    "AllowDevelopmentWithoutAuthority": false
  }
}
```

O Gateway validará os tokens JWT e aplicará rate limiting antes de rotear as requisições para os serviços backend.

## Testes

```bash
make test
# ou
dotnet test src/IntegrationHub.sln
```

Os testes de integração usam Testcontainers e criam instâncias isoladas de PostgreSQL e RabbitMQ.

## Notas Importantes

1. **Program Classes**: Para testes de integração completos, você pode precisar tornar as classes `Program` públicas ou usar uma abordagem diferente (ex: `InternalsVisibleTo`).

2. **Migrations**: As migrations são aplicadas automaticamente na inicialização dos serviços, mas você pode executá-las manualmente se necessário.

3. **Portas**: 
   - Gateway: 5000
   - Inbound.Api: 5001
   - PostgreSQL: 5432
   - RabbitMQ: 5672 (UI: 15672)
   - Redis: 6379
   - Jaeger: 16686
   - Grafana: 3000
   - Prometheus: 9090

