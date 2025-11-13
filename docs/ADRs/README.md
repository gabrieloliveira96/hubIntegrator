# Architecture Decision Records (ADRs)

Este diretório contém as decisões arquiteturais importantes do projeto.

## Índice

| ADR | Título | Status | Descrição |
|-----|--------|--------|-----------|
| [001](./001-yarp-gateway.md) | YARP como API Gateway | ✅ Aceito | Decisão de usar YARP ao invés de Ocelot ou outras soluções |
| [002](./002-rabbitmq-messaging.md) | RabbitMQ para Mensageria | ✅ Aceito | Decisão de usar RabbitMQ com MassTransit |
| [003](./003-outbox-inbox-pattern.md) | Outbox e Inbox Patterns | ✅ Aceito | Implementação de padrões para garantir entrega de mensagens |
| [004](./004-saga-orchestration.md) | Saga Pattern para Orquestração | ✅ Aceito | Uso de MassTransit Saga State Machine |
| [005](./005-headers-idempotency-anti-replay.md) | Headers para Idempotência e Anti-Replay | ✅ Aceito | Decisão sobre headers HTTP para segurança |

## Formato dos ADRs

Cada ADR segue o formato padrão:

- **Status**: Aceito, Proposto, Rejeitado, Depreciado
- **Contexto**: Situação que levou à decisão
- **Decisão**: O que foi decidido
- **Consequências**: Impactos positivos e negativos
- **Alternativas**: Outras opções consideradas

## Como Criar um Novo ADR

1. Crie um arquivo `XXX-titulo.md` onde XXX é o próximo número sequencial
2. Use o template abaixo
3. Adicione referência neste README

### Template

```markdown
# ADR XXX: Título da Decisão

## Status
Proposto

## Contexto
[Descreva o contexto que levou à decisão]

## Decisão
[Descreva a decisão tomada]

## Consequências
- **Positivas:**
  - [Benefício 1]
  - [Benefício 2]

- **Negativas:**
  - [Desvantagem 1]
  - [Desvantagem 2]

## Alternativas Consideradas
- [Alternativa 1]: [Por que foi rejeitada]
- [Alternativa 2]: [Por que foi rejeitada]
```

