using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Inbound.Api.Infrastructure.Swagger;

/// <summary>
/// Adiciona automaticamente os headers obrigatórios (Idempotency-Key, X-Nonce, X-Timestamp) 
/// aos endpoints no Swagger UI
/// </summary>
public class RequiredHeadersOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Adicionar headers apenas para endpoints POST/PUT/PATCH/DELETE (operações que modificam estado)
        // GET requests não precisam de headers obrigatórios
        var method = context.ApiDescription.HttpMethod?.ToUpper();
        if (method != "POST" && method != "PUT" && method != "PATCH" && method != "DELETE")
        {
            return;
        }
        
        // Adicionar headers apenas para endpoints POST (que criam recursos)
        if (operation.OperationId?.Contains("CreateRequest") == true || 
            context.ApiDescription.HttpMethod == "POST")
        {
            if (operation.Parameters == null)
            {
                operation.Parameters = new List<OpenApiParameter>();
            }

            // Idempotency-Key
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Chave única para garantir idempotência da requisição (GUID recomendado)",
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Format = "uuid",
                    Example = new Microsoft.OpenApi.Any.OpenApiString(Guid.NewGuid().ToString())
                }
            });

            // X-Nonce
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Nonce",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Valor único para prevenir replay attacks (GUID recomendado)",
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Format = "uuid",
                    Example = new Microsoft.OpenApi.Any.OpenApiString(Guid.NewGuid().ToString())
                }
            });

            // X-Timestamp
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Timestamp",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Unix timestamp em segundos (ex: 1734048000). Deve estar dentro de 5 minutos do tempo do servidor.",
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Example = new Microsoft.OpenApi.Any.OpenApiString(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
                }
            });
        }
    }
}

