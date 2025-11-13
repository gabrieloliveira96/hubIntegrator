# Minimal APIs vs Controllers - Decisão Arquitetural

## Por que Minimal APIs?

Escolhemos **Minimal APIs** (introduzidas no .NET 6, melhoradas no .NET 8) em vez de Controllers tradicionais pelos seguintes motivos:

### Vantagens das Minimal APIs

1. **Menos Boilerplate**
   - Código mais conciso e direto
   - Menos arquivos e classes necessárias
   - Menos herança e abstrações

2. **Performance**
   - Menor overhead de inicialização
   - Menos alocações de memória
   - Melhor para APIs de alto throughput

3. **Simplicidade**
   - Tudo em um lugar (endpoints e handlers)
   - Fácil de entender o fluxo
   - Ideal para APIs pequenas/médias

4. **Modernidade**
   - Padrão recomendado pelo .NET 8 para novas APIs
   - Alinhado com tendências modernas (FastAPI, Express.js)
   - Suporte completo a OpenAPI/Swagger

5. **Flexibilidade**
   - Fácil de organizar por domínio (como fizemos com `RequestsEndpoints`)
   - Pode usar `MapGroup` para agrupar endpoints relacionados
   - Suporta injeção de dependência da mesma forma

### Comparação

#### Minimal APIs (Atual)
```csharp
var group = app.MapGroup("/requests")
    .WithTags("Requests");

group.MapPost("/", CreateRequest)
    .Produces<CreateRequestResponse>(StatusCodes.Status202Accepted);
```

#### Controllers (Alternativa)
```csharp
[ApiController]
[Route("requests")]
public class RequestsController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateRequestResponse), 202)]
    public async Task<IActionResult> CreateRequest(...)
    {
        // ...
    }
}
```

### Quando Usar Cada Abordagem

**Use Minimal APIs quando:**
- ✅ API pequena/média (até ~20 endpoints)
- ✅ Performance é crítica
- ✅ Quer código mais simples e direto
- ✅ Time pequeno e precisa de produtividade

**Use Controllers quando:**
- ✅ API grande/complexa (muitos endpoints)
- ✅ Precisa de convenções MVC (filters, model binding avançado)
- ✅ Time acostumado com MVC
- ✅ Precisa de funcionalidades específicas de Controllers

## Nossa Situação

No **Inbound.Api**, temos apenas **2 endpoints**:
- `POST /requests` - Criar requisição
- `GET /requests/{id}` - Consultar status

Para este caso, **Minimal APIs são ideais** porque:
- Código mais limpo e fácil de manter
- Melhor performance
- Alinhado com as melhores práticas do .NET 8

## Quer Converter para Controllers?

Se você preferir usar Controllers, posso converter facilmente. Os benefícios seriam:
- Estrutura mais familiar para times acostumados com MVC
- Melhor organização se a API crescer muito
- Mais opções de filters e model binding

**Desvantagens:**
- Mais código boilerplate
- Performance ligeiramente inferior
- Mais arquivos para manter

## Recomendação

**Manter Minimal APIs** porque:
1. A API é pequena (2 endpoints)
2. Código está mais limpo e direto
3. Performance é melhor
4. É o padrão moderno do .NET 8

Se a API crescer significativamente (mais de 10-15 endpoints), podemos considerar migrar para Controllers.

