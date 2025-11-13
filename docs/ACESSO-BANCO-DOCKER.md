# Guia: Acessar PostgreSQL no Docker

## Pré-requisitos
- Docker Desktop rodando
- Docker Compose executado (`docker-compose up -d`)

## Método 1: Via psql (linha de comando)

### Passo 1: Verificar se o container está rodando
```powershell
docker ps | Select-String postgres
```

### Passo 2: Acessar o container PostgreSQL
```powershell
docker exec -it hub-postgres psql -U postgres
```

Ou se o nome do container for diferente:
```powershell
docker exec -it $(docker ps --filter "ancestor=postgres" --format "{{.Names}}") psql -U postgres
```

### Passo 3: Listar bancos de dados
```sql
\l
```

### Passo 4: Conectar a um banco específico

**Inbound API (inbound_db):**
```sql
\c inbound_db
```

**Orchestrator Worker (orchestrator_db):**
```sql
\c orchestrator_db
```

**Outbound Worker (outbound_db):**
```sql
\c outbound_db
```

### Passo 5: Comandos úteis

**Listar tabelas:**
```sql
\dt
```

**Ver estrutura de uma tabela:**
```sql
\d "Requests"
```

**Consultar dados:**
```sql
SELECT * FROM "Requests" LIMIT 10;
```

**Sair do psql:**
```sql
\q
```

## Método 2: Via Docker exec com comando direto

### Acessar banco Inbound
```powershell
docker exec -it hub-postgres psql -U postgres -d inbound_db
```

### Acessar banco Orchestrator
```powershell
docker exec -it hub-postgres psql -U postgres -d orchestrator_db
```

### Acessar banco Outbound
```powershell
docker exec -it hub-postgres psql -U postgres -d outbound_db
```

## Método 3: Via cliente gráfico (DBeaver, pgAdmin, etc.)

### Configuração de conexão:
- **Host:** localhost
- **Porta:** 5432
- **Usuário:** postgres
- **Senha:** postgres
- **Database:** 
  - `inbound_db` (Inbound API)
  - `orchestrator_db` (Orchestrator Worker)
  - `outbound_db` (Outbound Worker)

### String de conexão:
```
Host=localhost;Port=5432;Database=inbound_db;Username=postgres;Password=postgres
```

## Método 4: Via PowerShell com psql local

Se você tiver o PostgreSQL instalado localmente:

```powershell
$env:PGPASSWORD = "postgres"
psql -h localhost -p 5432 -U postgres -d inbound_db
```

## Comandos SQL úteis

### Ver todas as requests criadas:
```sql
SELECT 
    "CorrelationId",
    "PartnerCode",
    "Type",
    "Status",
    "CreatedAt"
FROM "Requests"
ORDER BY "CreatedAt" DESC
LIMIT 10;
```

### Ver sagas do Orchestrator:
```sql
SELECT 
    "CorrelationId",
    "CurrentState",
    "PartnerCode",
    "RequestType",
    "CreatedAt"
FROM "Sagas"
ORDER BY "CreatedAt" DESC
LIMIT 10;
```

### Limpar dados de teste:
```sql
-- CUIDADO: Isso apaga todos os dados!
DELETE FROM "Requests";
DELETE FROM "DedupKeys";
DELETE FROM "Nonces";
DELETE FROM "Sagas";
DELETE FROM "Outbox";
DELETE FROM "Inbox";
```

### Verificar estados inválidos nas sagas:
```sql
SELECT 
    "CorrelationId",
    "CurrentState",
    "CreatedAt"
FROM "Sagas"
WHERE "CurrentState" IS NULL 
   OR "CurrentState" = '' 
   OR "CurrentState" NOT IN ('Initial', 'Received', 'Validating', 'Processing', 'Succeeded', 'Failed');
```

### Corrigir estados inválidos:
```sql
UPDATE "Sagas" 
SET "CurrentState" = 'Initial' 
WHERE "CurrentState" IS NULL 
   OR "CurrentState" = '' 
   OR "CurrentState" NOT IN ('Initial', 'Received', 'Validating', 'Processing', 'Succeeded', 'Failed');
```

## Troubleshooting

### Container não está rodando:
```powershell
docker-compose -f deploy/docker-compose.yml up -d postgres
```

### Ver logs do PostgreSQL:
```powershell
docker logs hub-postgres
```

### Reiniciar o container:
```powershell
docker restart hub-postgres
```

### Verificar portas:
```powershell
docker port hub-postgres
```

## Informações dos Bancos

### Inbound API (inbound_db)
- **Tabelas principais:**
  - `Requests` - Requisições recebidas
  - `DedupKeys` - Chaves de idempotência
  - `Nonces` - Valores nonce para anti-replay
  - `Inbox` - Mensagens recebidas (Inbox Pattern)

### Orchestrator Worker (orchestrator_db)
- **Tabelas principais:**
  - `Sagas` - Estados das sagas (MassTransit)
  - `Outbox` - Mensagens a serem publicadas (Outbox Pattern)

### Outbound Worker (outbound_db)
- **Tabelas principais:**
  - `Outbox` - Mensagens a serem publicadas (Outbox Pattern)

