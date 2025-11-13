# ADR 003: Outbox e Inbox Patterns

## Status
Aceito

## Contexto
Garantir consistência entre persistência e publicação de mensagens (at-least-once delivery).

## Decisão
Implementar Outbox Pattern para publicação e Inbox Pattern para consumo.

## Consequências
- **Positivas:**
  - Garantia de publicação at-least-once
  - Idempotência de consumo
  - Consistência transacional

- **Negativas:**
  - Overhead de tabelas adicionais
  - Worker adicional para processar Outbox
  - Latência ligeiramente maior

