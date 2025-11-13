# Configuração do Seq para Logs Estruturados

## Visão Geral

O Seq foi configurado para centralizar e visualizar logs estruturados de todos os serviços do Hub de Integração.

## Configuração

### 1. Docker Compose

O Seq foi adicionado ao `deploy/docker-compose.yml`:

```yaml
seq:
  image: datalust/seq:latest
  container_name: hub-seq
  environment:
    ACCEPT_EULA: "Y"
  ports:
    - "5341:80"   # HTTP UI
    - "5342:5341" # Ingestion API
  volumes:
    - seq_data:/data
```

### 2. Pacotes NuGet

O pacote `Serilog.Sinks.Seq` versão 8.0.0 foi adicionado aos seguintes projetos:

- ✅ `Gateway.Yarp`
- ✅ `Orchestrator.Worker`
- ✅ `Outbound.Worker`
- ✅ `Inbound.Api` (já estava instalado)

### 3. Configuração do Serilog

Todos os `Program.cs` foram atualizados para enviar logs ao Seq:

```csharp
var seqUrl = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "NomeDoServico")
    .WriteTo.Console()
    .WriteTo.Seq(seqUrl)  // ← Adicionado
    .CreateLogger();
```

### 4. Appsettings.json

A configuração do Seq foi adicionada em todos os `appsettings.json`:

```json
{
  "Seq": {
    "ServerUrl": "http://localhost:5341"
  }
}
```

## Como Usar

### 1. Iniciar o Seq

```powershell
docker-compose -f deploy/docker-compose.yml up -d seq
```

### 2. Acessar a Interface Web

Abra seu navegador em: **http://localhost:5341**

### 3. Executar as Aplicações

Execute suas aplicações normalmente. Os logs aparecerão automaticamente no Seq!

```powershell
# Gateway
dotnet run --project src/Gateway.Yarp/Gateway.Yarp.csproj

# Inbound API
dotnet run --project src/Inbound.Api/Inbound.Api.csproj

# Orchestrator Worker
dotnet run --project src/Orchestrator.Worker/Orchestrator.Worker.csproj

# Outbound Worker
dotnet run --project src/Outbound.Worker/Outbound.Worker.csproj
```

## Benefícios

### ✅ Logs Estruturados Centralizados
- Todos os serviços enviam logs para o mesmo lugar
- Facilita debugging e troubleshooting

### ✅ Busca e Filtros Poderosos
- Busque por propriedades estruturadas
- Filtre por Service, CorrelationId, Level, etc.
- Exemplos de queries:
  - `Service = 'Inbound.Api'`
  - `CorrelationId = '550e8400-e29b-41d4-a716-446655440000'`
  - `Level = 'Error'`

### ✅ Visualização por Service
- Cada log inclui a propriedade `Service`
- Filtre facilmente por serviço específico
- Veja logs de todos os serviços ou de um só

### ✅ Alertas e Dashboards
- Configure alertas baseados em queries
- Crie dashboards personalizados
- Monitore métricas de logs

## Propriedades Estruturadas

Os logs incluem automaticamente:

- **Service**: Nome do serviço (Inbound.Api, Gateway.Yarp, etc.)
- **CorrelationId**: ID de correlação da requisição
- **RequestId**: ID da requisição HTTP
- **ConnectionId**: ID da conexão
- **Level**: Nível do log (Information, Warning, Error, etc.)
- **Timestamp**: Data e hora do log
- **Message**: Mensagem do log
- **Exception**: Stack trace (quando aplicável)

## Exemplos de Queries no Seq

### Filtrar por Service
```
Service = 'Inbound.Api'
```

### Filtrar por CorrelationId
```
CorrelationId = '550e8400-e29b-41d4-a716-446655440000'
```

### Filtrar apenas erros
```
Level = 'Error'
```

### Filtrar por múltiplos serviços
```
Service in ['Inbound.Api', 'Orchestrator.Worker']
```

### Buscar por texto na mensagem
```
Message like '%Request created%'
```

### Filtrar por tempo
```
@Timestamp > ago(5m)
```

## Configuração Avançada

### Alterar URL do Seq

Edite o `appsettings.json` de cada projeto:

```json
{
  "Seq": {
    "ServerUrl": "http://seu-seq-server:5342"
  }
}
```

### Configurar Nível de Log

No `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

### Adicionar Propriedades Customizadas

No `Program.cs`:

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Inbound.Api")
    .Enrich.WithProperty("Environment", "Development")  // ← Customizado
    .WriteTo.Console()
    .WriteTo.Seq(seqUrl)
    .CreateLogger();
```

## Troubleshooting

### Seq não está recebendo logs

1. Verifique se o Seq está rodando:
   ```powershell
   docker ps | Select-String seq
   ```

2. Verifique os logs do container:
   ```powershell
   docker logs hub-seq
   ```

3. Verifique a URL no `appsettings.json`:
   - Deve ser `http://localhost:5341` para desenvolvimento local
   - Em Docker, use `http://seq:5341` (nome do serviço)

4. Verifique se o pacote está instalado:
   ```powershell
   dotnet list package | Select-String Seq
   ```

### Logs não aparecem no Seq

1. Verifique se o `WriteTo.Seq()` está no `Program.cs`
2. Verifique se a URL está correta
3. Verifique se há erros de conexão nos logs do console
4. Tente acessar a API diretamente: `http://localhost:5341/api/events/raw`

## Referências

- [Documentação do Seq](https://docs.datalust.co/docs)
- [Serilog.Sinks.Seq](https://github.com/serilog/serilog-sinks-seq)
- [Serilog Documentation](https://serilog.net/)

