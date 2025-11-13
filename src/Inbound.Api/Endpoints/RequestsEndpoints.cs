using Inbound.Api.Application.Commands;
using Inbound.Api.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Inbound.Api.Endpoints;

public static class RequestsEndpoints
{
    public static void MapRequestsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/requests")
            .WithTags("Requests");
            // .RequireAuthorization("Write"); // TEMPORARIAMENTE DESABILITADO - Exige autenticação JWT

        group.MapPost("/", CreateRequest)
            .Produces<ReceiveRequestCommandResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetRequest)
            // .RequireAuthorization("Read") // TEMPORARIAMENTE DESABILITADO - Exige autenticação JWT para leitura
            .Produces<GetRequestQueryResponse>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateRequest(
        [FromBody] ReceiveRequestCommand command,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Extract Idempotency-Key from header
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            var exampleKey = Guid.NewGuid().ToString();
            return Results.BadRequest(new 
            { 
                error = "Idempotency-Key header is required",
                message = "The Idempotency-Key header is required to ensure idempotency",
                example = new { IdempotencyKey = exampleKey },
                howToFix = $"Add the Idempotency-Key header to your request. Example: {exampleKey}"
            });
        }

        // Create command with idempotency key from header
        var request = new ReceiveRequestCommandWithIdempotency(
            command.PartnerCode,
            command.Type,
            command.Payload,
            idempotencyKey);
        
        var response = await mediator.Send(request, cancellationToken);

        // Check if this is a duplicate (status would be different or we could check CreatedAt)
        // For now, always return Accepted
        return Results.Accepted($"/requests/{response.CorrelationId}", response);
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

        return Results.Ok(response);
    }
}


