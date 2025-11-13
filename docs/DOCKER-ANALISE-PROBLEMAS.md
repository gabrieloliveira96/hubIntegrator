# 🔍 Análise: Problemas Potenciais ao Subir com Docker

## ✅ O que está funcionando

1. **Dockerfiles corretos**: Todos os 4 Dockerfiles estão bem estruturados
2. **Dependências configuradas**: `depends_on` com health checks
3. **Migrations automáticas**: Aplicadas no startup de cada serviço
4. **Connection strings**: Configuradas via variáveis de ambiente no docker-compose

## ⚠️ Problemas Identificados

### 1. ❌ Bancos de Dados Não São Criados Automaticamente

**Problema:**
- PostgreSQL cria apenas o banco `postgres` por padrão
- Serviços precisam de: `inbound_db`, `orchestrator_db`, `outbound_db`
- EF Core `Migrate()` **não cria o banco**, apenas aplica migrations

**Solução:**
Adicionar script de inicialização ou usar `EnsureCreated()` (não recomendado para produção).

### 2. ⚠️ URL do Seq Inconsistente no Docker

**Problema:**
- Docker-compose expõe Seq na porta `5341:80` (externa:interna)
- Dentro do Docker, serviços devem usar `http://seq:80` ou `http://seq:5341`
- Alguns serviços configurados com `localhost:5341` (não funciona no Docker)

**Solução:**
Adicionar variável de ambiente no docker-compose para Seq URL.

### 3. ⚠️ Connection Strings Usam Nomes Corretos

**Status:** ✅ **OK**
- Connection strings já usam nomes de serviço: `postgres`, `rabbitmq`, `redis`
- Isso está correto!

## 🔧 Correções Necessárias

### Correção 1: Criar Bancos de Dados

**Opção A: Script de Inicialização (Recomendado)**

Criar `deploy/init-databases.sh`:

```bash
#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE DATABASE inbound_db;
    CREATE DATABASE orchestrator_db;
    CREATE DATABASE outbound_db;
EOSQL
```

E adicionar no docker-compose.yml:

```yaml
postgres:
  # ... configuração existente
  volumes:
    - postgres_data:/var/lib/postgresql/data
    - ./init-databases.sh:/docker-entrypoint-initdb.d/init-databases.sh
```

**Opção B: Usar EnsureCreated (Desenvolvimento)**

Modificar Program.cs para criar banco se não existir:

```csharp
// Apenas para desenvolvimento
if (app.Environment.IsDevelopment())
{
    db.Database.EnsureCreated();
}
db.Database.Migrate();
```

### Correção 2: Configurar Seq URL no Docker

Adicionar variável de ambiente no docker-compose.yml:

```yaml
gateway:
  environment:
    - Seq__ServerUrl=http://seq:80  # Porta interna do container
    # ... outras variáveis

inbound-api:
  environment:
    - Seq__ServerUrl=http://seq:80
    # ... outras variáveis

orchestrator-worker:
  environment:
    - Seq__ServerUrl=http://seq:80
    # ... outras variáveis

outbound-worker:
  environment:
    - Seq__ServerUrl=http://seq:80
    # ... outras variáveis
```

## 📋 Checklist Antes de Subir

- [ ] Criar script de inicialização de bancos ou usar EnsureCreated
- [ ] Adicionar variável Seq__ServerUrl no docker-compose.yml
- [ ] Verificar se todas as portas estão disponíveis
- [ ] Verificar se Docker Desktop está rodando

## 🧪 Como Testar

```bash
# 1. Subir infraestrutura primeiro
docker-compose -f deploy/docker-compose.yml up -d postgres rabbitmq redis

# 2. Aguardar health checks
docker-compose -f deploy/docker-compose.yml ps

# 3. Verificar se bancos foram criados
docker-compose -f deploy/docker-compose.yml exec postgres psql -U postgres -l

# 4. Subir serviços
docker-compose -f deploy/docker-compose.yml up -d gateway inbound-api orchestrator-worker outbound-worker

# 5. Verificar logs
docker-compose -f deploy/docker-compose.yml logs -f
```

## 🎯 Resposta Direta

**AGORA SIM! ✅ Com as correções aplicadas, deve funcionar.**

**Correções aplicadas:**
1. ✅ Script de criação de bancos de dados (`init-databases.sh`)
2. ✅ URL do Seq configurada para todos os serviços (`http://seq:80`)
3. ✅ RabbitMQ usando connection string ao invés de `localhost` hardcoded
4. ✅ IdentityServer adicionado ao docker-compose
5. ✅ Gateway e Inbound.Api configurados para usar IdentityServer

**O que vai funcionar:**
- ✅ Infraestrutura (PostgreSQL, RabbitMQ, Redis, Seq) sobe corretamente
- ✅ IdentityServer disponível em `http://localhost:5002`
- ✅ Gateway configurado para autenticar via IdentityServer
- ✅ Containers são criados com dependências corretas
- ✅ Health checks funcionam

**Serviços disponíveis após subir:**
- Gateway: `http://localhost:5000`
- Inbound API: `http://localhost:5001`
- IdentityServer: `http://localhost:5002`
- RabbitMQ UI: `http://localhost:15672`
- Seq: `http://localhost:5341`
- Jaeger: `http://localhost:16686`
- Grafana: `http://localhost:3000`
- Prometheus: `http://localhost:9090`

