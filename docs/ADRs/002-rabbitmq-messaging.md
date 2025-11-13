# ADR 002: RabbitMQ para Mensageria

## Status
Aceito

## Contexto
Precisamos de um message broker para comunicação assíncrona entre serviços com garantias de entrega e DLQ.

## Decisão
Utilizar RabbitMQ com MassTransit.

## Consequências
- **Positivas:**
  - MassTransit abstrai complexidade
  - Suporte nativo a DLQ
  - Integração fácil com .NET
  - Padrões de mensageria robustos

- **Negativas:**
  - Requer gerenciamento de cluster para alta disponibilidade
  - Performance menor que Kafka para alto throughput

