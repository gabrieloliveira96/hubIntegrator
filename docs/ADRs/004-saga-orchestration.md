# ADR 004: Saga Pattern para Orquestração

## Status
Aceito

## Contexto
Orquestrar fluxos distribuídos com compensação em caso de falha.

## Decisão
Utilizar MassTransit Saga State Machine com EF Core como repository.

## Consequências
- **Positivas:**
  - Estado persistido no banco
  - Compensação automática
  - Rastreabilidade completa
  - Integração nativa com MassTransit

- **Negativas:**
  - Complexidade adicional
  - Requer cuidado com concorrência (pessimistic locking)

