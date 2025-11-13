using MediatR;

namespace Inbound.Api.Application.Queries;

public record GetRequestQuery(Guid Id) : IRequest<GetRequestQueryResponse?>;

public record GetRequestQueryResponse
{
    public Guid CorrelationId { get; init; }
    public string PartnerCode { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

