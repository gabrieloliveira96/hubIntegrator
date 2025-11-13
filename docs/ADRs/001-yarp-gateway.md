# ADR 001: YARP como API Gateway

## Status
Aceito

## Contexto
Precisamos de um API Gateway para centralizar autenticação, rate limiting, roteamento e observabilidade.

## Decisão
Utilizar YARP (Yet Another Reverse Proxy) como API Gateway.

## Consequências
- **Positivas:**
  - Nativo do .NET, fácil integração
  - Configuração via appsettings.json
  - Suporte a rate limiting nativo .NET 8
  - Performance excelente

- **Negativas:**
  - Menos features que soluções como Kong/Ambassador
  - Requer configuração manual de políticas avançadas

