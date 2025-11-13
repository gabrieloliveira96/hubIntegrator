# ADR 001: YARP como API Gateway

## Status
✅ Aceito

## Contexto
Precisamos de um API Gateway para centralizar autenticação, rate limiting, roteamento e observabilidade.

As principais opções consideradas foram:
1. **YARP (Yet Another Reverse Proxy)** - Desenvolvido pela Microsoft
2. **Ocelot** - Gateway popular na comunidade .NET
3. **Kong/Ambassador** - Gateways externos (descartados por complexidade)

## Decisão
Utilizar **YARP** como solução de API Gateway.

## Razões da Decisão

### 1. Suporte Nativo da Microsoft
- ✅ YARP é desenvolvido e mantido pela Microsoft
- ✅ Integração nativa com .NET 8 e ASP.NET Core
- ✅ Suporte oficial e roadmap alinhado com .NET

### 2. Performance e Eficiência
- ✅ YARP é extremamente performático
- ✅ Menor overhead e uso de memória
- ✅ Processamento assíncrono nativo

### 3. Simplicidade e Foco
- ✅ YARP é focado em reverse proxy
- ✅ API simples e intuitiva
- ✅ Configuração via código ou JSON

### 4. Integração com .NET 8
- ✅ YARP usa Minimal APIs nativamente
- ✅ Integração perfeita com ASP.NET Core
- ✅ Suporte completo a .NET 8 features

## Consequências
- **Positivas:**
  - Nativo do .NET, fácil integração
  - Configuração via appsettings.json
  - Suporte a rate limiting nativo .NET 8
  - Performance excelente
  - Suporte oficial da Microsoft

- **Negativas:**
  - Menos features que soluções como Kong/Ambassador
  - Requer configuração manual de políticas avançadas
  - Comunidade menor que Ocelot (mas crescente)

## Alternativas Consideradas
- **Ocelot**: Descartado por ser mais pesado e menos alinhado com .NET 8
- **Kong**: Descartado por ser externo e adicionar complexidade
- **Ambassador**: Descartado por ser focado em Kubernetes

## Referências
- [YARP GitHub](https://github.com/microsoft/reverse-proxy)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)

