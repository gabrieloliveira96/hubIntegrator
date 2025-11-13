# ADR-001: Escolha do YARP como API Gateway

## Status
✅ Aceito

## Contexto

Precisávamos escolher uma solução de API Gateway para o Hub de Integração. As principais opções consideradas foram:

1. **YARP (Yet Another Reverse Proxy)** - Desenvolvido pela Microsoft
2. **Ocelot** - Gateway popular na comunidade .NET
3. **Kong/Ambassador** - Gateways externos (descartados por complexidade)

## Decisão

Escolhemos **YARP** como solução de API Gateway.

## Razões da Decisão

### 1. **Suporte Nativo da Microsoft**

- ✅ YARP é desenvolvido e mantido pela Microsoft
- ✅ Integração nativa com .NET 8 e ASP.NET Core
- ✅ Suporte oficial e roadmap alinhado com .NET
- ✅ Documentação oficial e exemplos atualizados

**Ocelot:**
- ⚠️ Projeto open-source da comunidade
- ⚠️ Menos alinhado com as últimas versões do .NET
- ⚠️ Dependência de mantenedores voluntários

### 2. **Performance e Eficiência**

- ✅ YARP é extremamente performático (desenvolvido para alta performance)
- ✅ Menor overhead e uso de memória
- ✅ Processamento assíncrono nativo
- ✅ Otimizado para cenários de alta concorrência

**Ocelot:**
- ⚠️ Mais pesado e com mais overhead
- ⚠️ Processamento síncrono em algumas operações
- ⚠️ Mais dependências e abstrações

### 3. **Simplicidade e Foco**

- ✅ YARP é focado em reverse proxy (faz bem uma coisa)
- ✅ API simples e intuitiva
- ✅ Configuração via código ou JSON
- ✅ Fácil de entender e manter

**Ocelot:**
- ⚠️ Mais complexo com muitas features
- ⚠️ Curva de aprendizado maior
- ⚠️ Pode ser "overkill" para necessidades simples
- ⚠️ Configuração mais verbosa

### 4. **Integração com .NET 8**

- ✅ YARP usa Minimal APIs nativamente
- ✅ Integração perfeita com ASP.NET Core
- ✅ Suporte completo a .NET 8 features
- ✅ Middleware pipeline nativo

**Ocelot:**
- ⚠️ Desenvolvido antes do .NET 8
- ⚠️ Pode ter limitações com Minimal APIs
- ⚠️ Requer mais configuração para integração moderna

### 5. **Manutenção e Atualizações**

- ✅ YARP recebe atualizações regulares da Microsoft
- ✅ Bugs corrigidos rapidamente
- ✅ Roadmap transparente
- ✅ Compatibilidade garantida com novas versões do .NET

**Ocelot:**
- ⚠️ Depende da comunidade
- ⚠️ Atualizações podem ser mais lentas
- ⚠️ Risco de abandono do projeto

### 6. **Features Necessárias**

Para nosso caso de uso, precisamos:
- ✅ Reverse proxy (roteamento)
- ✅ Rate limiting (implementado via .NET 8 nativo)
- ✅ Autenticação/Authorization (JWT via ASP.NET Core)
- ✅ Health checks (nativo ASP.NET Core)
- ✅ Observabilidade (OpenTelemetry)

**YARP fornece:**
- ✅ Reverse proxy excelente
- ✅ Integração perfeita com features nativas do .NET 8

**Ocelot fornece:**
- ✅ Reverse proxy + muitas features extras
- ⚠️ Mas muitas features não são necessárias para nosso caso

### 7. **Comunidade e Ecossistema**

- ✅ YARP tem crescente adoção na comunidade .NET
- ✅ Exemplos e tutoriais oficiais
- ✅ Stack Overflow com respostas atualizadas
- ✅ GitHub ativo com issues resolvidas rapidamente

**Ocelot:**
- ✅ Grande comunidade (mais antiga)
- ⚠️ Mas menos ativa recentemente
- ⚠️ Alguns issues antigos sem resolução

## Comparação Técnica

| Aspecto | YARP | Ocelot |
|---------|------|--------|
| **Desenvolvedor** | Microsoft | Comunidade |
| **Performance** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Simplicidade** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Features** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Documentação** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Manutenção** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **.NET 8 Support** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Tamanho** | Leve | Médio |

## Quando Usar Ocelot?

Ocelot pode ser melhor escolha quando você precisa de:

- ✅ Features avançadas de gateway (service discovery, load balancing complexo)
- ✅ Integração com sistemas legados
- ✅ Configuração muito complexa de roteamento
- ✅ Features específicas que Ocelot oferece e YARP não

## Quando Usar YARP?

YARP é melhor escolha quando você precisa de:

- ✅ Reverse proxy simples e performático
- ✅ Integração nativa com .NET 8
- ✅ Performance crítica
- ✅ Manutenção a longo prazo
- ✅ Simplicidade e clareza

## Consequências

### Positivas

1. ✅ Gateway leve e performático
2. ✅ Código mais simples e fácil de manter
3. ✅ Melhor integração com .NET 8
4. ✅ Suporte oficial da Microsoft
5. ✅ Facilidade para novos desenvolvedores

### Negativas

1. ⚠️ Menos features "out-of-the-box" (mas podemos implementar)
2. ⚠️ Comunidade menor que Ocelot (mas crescente)
3. ⚠️ Menos exemplos na internet (mas documentação oficial excelente)

## Alternativas Consideradas

1. **Kong** - Descartado por ser externo e adicionar complexidade
2. **Ambassador** - Descartado por ser focado em Kubernetes
3. **Traefik** - Descartado por não ser .NET nativo
4. **Ocelot** - Considerado, mas YARP foi escolhido pelas razões acima

## Referências

- [YARP GitHub](https://github.com/microsoft/reverse-proxy)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [Ocelot GitHub](https://github.com/ThreeMammals/Ocelot)
- [.NET 8 Performance Improvements](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-8/)

## Data da Decisão

2024-12-XX

## Revisão

Esta decisão deve ser revisada se:
- YARP não atender requisitos futuros
- Ocelot evoluir significativamente
- Novas soluções de gateway surgirem

