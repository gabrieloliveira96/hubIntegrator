using Inbound.Api.Application.Commands;
using Inbound.Api.Application.Queries;
using Inbound.Api.Presentation.Dtos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Inbound.Api.Presentation.Endpoints;

/// <summary>
/// Endpoints da API de requisições - Camada de Apresentação
/// </summary>
public static class RequestsEndpoints
{
    public static void MapRequestsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/requests")
            .WithTags("Requests");
            // Não precisa RequireAuthorization aqui - o Gateway já validou o token
            // Se a requisição chegou aqui, já passou pela autenticação no Gateway

        group.MapPost("/", CreateRequest)
            .Produces<CreateRequestResponseDto>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetRequest)
            // .RequireAuthorization("Read") // TEMPORARIAMENTE DESABILITADO - Exige autenticação JWT para leitura
            .Produces<GetRequestResponseDto>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateRequest(
        [FromBody] CreateRequestDto dto,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Extract Idempotency-Key from header
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            var exampleKey = Guid.NewGuid().ToString();
            return Results.BadRequest(new ErrorResponseDto
            { 
                Error = "Idempotency-Key header is required",
                Message = "The Idempotency-Key header is required to ensure idempotency",
                Example = new { IdempotencyKey = exampleKey },
                HowToFix = $"Add the Idempotency-Key header to your request. Example: {exampleKey}"
            });
        }

        // Create command with idempotency key from header
        var command = new ReceiveRequestCommandWithIdempotency(
            dto.PartnerCode,
            dto.Type,
            dto.Payload,
            idempotencyKey);
        
        var response = await mediator.Send(command, cancellationToken);

        // Map Application response to Presentation DTO
        var responseDto = new CreateRequestResponseDto
        {
            CorrelationId = response.CorrelationId,
            Status = response.Status,
            CreatedAt = response.CreatedAt
        };

        return Results.Accepted($"/requests/{responseDto.CorrelationId}", responseDto);
    }

    private static async Task<IResult> GetRequest(
        Guid id,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetRequestQuery(id);
        var response = await mediator.Send(query, cancellationToken);

        if (response == null)
        {
            return Results.NotFound();
        }

        // Map Application response to Presentation DTO
        var responseDto = new GetRequestResponseDto
        {
            CorrelationId = response.CorrelationId,
            PartnerCode = response.PartnerCode,
            Type = response.Type,
            Status = response.Status,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt
        };

        return Results.Ok(responseDto);
    }
}

