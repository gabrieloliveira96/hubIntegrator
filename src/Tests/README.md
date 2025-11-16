# Testes - Hub de Integração

Este diretório contém os testes automatizados do sistema Hub de Integração.

## Estrutura

```
Tests/
├── Unit/                    # Testes unitários
│   └── Unit.Tests/
│       ├── Handlers/        # Testes de handlers (CQRS)
│       └── Services/        # Testes de serviços de domínio
└── Integration/              # Testes de integração
    └── Integration.Tests/
        ├── InboundTests.cs           # Testes da API Inbound
        ├── OrchestratorSagaTests.cs  # Testes da Saga
        └── InboundWebApplicationFactory.cs  # Factory para testes de API
```

## Testes Unitários

### Handlers

- **ReceiveRequestHandlerTests**: Testa a lógica de recebimento de requisições
  - Idempotência (chave duplicada)
  - Criação de nova requisição
  - Processamento de payload JSON e texto
  - Publicação de eventos

- **GetRequestHandlerTests**: Testa a consulta de requisições
  - Busca por CorrelationId
  - Tratamento de requisição não encontrada
  - Mapeamento de entidades para DTOs

### Services

- **BusinessRulesServiceTests**: Testa as regras de negócio
  - Validação de requisições
  - Enriquecimento de dados
  - Tratamento de cancelamento

## Testes de Integração

### Inbound API

- **Database_ShouldBeAccessible**: Valida conectividade com banco de dados
- **CreateRequest_WithoutIdempotencyKey**: Valida que Idempotency-Key é obrigatório
- **CreateRequest_WithIdempotencyKey**: Testa criação de requisição
- **CreateRequest_WithDuplicateIdempotencyKey**: Testa idempotência
- **GetRequest_WhenRequestExists**: Testa consulta de requisição existente
- **GetRequest_WhenRequestNotFound**: Testa tratamento de requisição não encontrada

### Saga (Orchestrator)

- **Database_ShouldBeAccessible**: Valida conectividade com banco de dados
- **Saga_WhenRequestReceived_ShouldCreateSagaInstance**: Testa criação de instância de saga
- **Saga_WhenRequestCompleted_ShouldTransitionToSucceeded**: Testa transição para estado Succeeded
- **Saga_WhenRequestFailed_ShouldTransitionToFailed**: Testa transição para estado Failed

## Como Executar

### Executar todos os testes

```bash
dotnet test
```

### Executar apenas testes unitários

```bash
dotnet test src/Tests/Unit/Unit.Tests/Unit.Tests.csproj
```

### Executar apenas testes de integração

```bash
dotnet test src/Tests/Integration/Integration.Tests/Integration.Tests.csproj
```

### Executar com cobertura de código

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Dependências

### Testes Unitários
- **xUnit**: Framework de testes
- **Moq**: Mocking de dependências
- **FluentAssertions**: Asserções mais legíveis

### Testes de Integração
- **xUnit**: Framework de testes
- **Testcontainers**: Containers Docker para PostgreSQL, RabbitMQ e Redis
- **Microsoft.AspNetCore.Mvc.Testing**: Testes de API
- **MassTransit**: Para testes de mensageria
- **FluentAssertions**: Asserções mais legíveis

## Observações

- Os testes de integração usam **Testcontainers** para criar containers Docker isolados
- Cada teste de integração tem seu próprio banco de dados e filas
- Os containers são criados e destruídos automaticamente
- Os testes podem levar mais tempo devido à inicialização dos containers

## Próximos Passos

- [ ] Adicionar testes para Consumer do Outbound
- [ ] Adicionar testes end-to-end completos
- [ ] Adicionar testes de performance
- [ ] Adicionar testes de carga
- [ ] Configurar CI/CD para execução automática de testes

