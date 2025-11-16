# 📚 Documentação do Hub de Integração

Este diretório contém toda a documentação técnica do projeto.

## 📋 Estrutura

```
docs/
├── ADRs/                          # Architecture Decision Records
│   ├── README.md                  # Índice dos ADRs
│   ├── 001-yarp-gateway.md
│   ├── 002-rabbitmq-messaging.md
│   ├── 003-outbox-inbox-pattern.md
│   ├── 004-saga-orchestration.md
│   └── 005-headers-idempotency-anti-replay.md
│
├── Proposta_Arquitetura_TOTVS_HubIntegracao.md  # Documentação principal
│
├── Guias de Setup e Configuração
│   ├── SETUP.md                   # Guia de configuração inicial
│   ├── ACESSO-BANCO-DOCKER.md     # Como acessar bancos no Docker
│   └── SEQ-LOGGING.md              # Configuração de logging com Seq
│
├── Guias de Uso
│   ├── TESTING-WITH-IDENTITYSERVER.md  # Como testar com IdentityServer
│   └── AUTHENTICATION-FLOW.md          # Fluxo de autenticação

```

## 🎯 Documentos Principais

### 📖 Documentação Técnica Completa

**[Proposta de Arquitetura TOTVS - Hub de Integração](./Proposta_Arquitetura_TOTVS_HubIntegracao.md)**

Documentação técnica completa com:
- Visão geral da arquitetura
- Decisões arquiteturais
- Fluxo de dados e processos
- Estratégias de resiliência
- Segurança e governança
- Deploy e escalabilidade

### 🏗️ Architecture Decision Records (ADRs)

**[ADRs](./ADRs/README.md)** - Decisões arquiteturais importantes:
- ADR 001: YARP como API Gateway
- ADR 002: RabbitMQ para Mensageria
- ADR 003: Outbox e Inbox Patterns
- ADR 004: Saga Pattern para Orquestração
- ADR 005: Headers para Idempotência e Anti-Replay

## 📚 Guias por Categoria

### 🚀 Início Rápido

1. **[SETUP.md](./SETUP.md)** - Configuração inicial do projeto
2. **[TESTING-WITH-IDENTITYSERVER.md](./TESTING-WITH-IDENTITYSERVER.md)** - Como testar com autenticação

### 🔧 Configuração

- **[ACESSO-BANCO-DOCKER.md](./ACESSO-BANCO-DOCKER.md)** - Acessar PostgreSQL/Redis no Docker
- **[SEQ-LOGGING.md](./SEQ-LOGGING.md)** - Configurar logging estruturado

### 🔍 Entendendo o Sistema

- **[AUTHENTICATION-FLOW.md](./AUTHENTICATION-FLOW.md)** - Como funciona a autenticação (exemplos práticos e troubleshooting)


## 🔗 Links Úteis

- [README Principal](../README.md) - Visão geral do projeto

## 📝 Convenções

- **ADRs**: Sempre na pasta `ADRs/` com numeração sequencial
- **Guias**: Nomes descritivos em UPPERCASE
- **Scripts**: Nomes em lowercase com extensão apropriada

---

**Última atualização**: Novembro 2025

