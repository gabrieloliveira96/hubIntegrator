using Inbound.Api.Application.Queries;
using Inbound.Api.Domain.Repositories;
using MediatR;

namespace Inbound.Api.Application.Handlers;

public class GetRequestHandler : IRequestHandler<GetRequestQuery, GetRequestQueryResponse?>
{
    private readonly IRequestRepository _requestRepository;
    private readonly ILogger<GetRequestHandler> _logger;

    public GetRequestHandler(
        IRequestRepository requestRepository,
        ILogger<GetRequestHandler> logger)
    {
        _requestRepository = requestRepository;
        _logger = logger;
    }

    public async Task<GetRequestQueryResponse?> Handle(
        GetRequestQuery request,
        CancellationToken cancellationToken)
    {
        // Usa o repositório para buscar a entidade Request
        var requestEntity = await _requestRepository.GetByCorrelationIdAsync(request.Id, cancellationToken);

        if (requestEntity == null)
        {
            _logger.LogWarning("Request not found. CorrelationId: {CorrelationId}", request.Id);
            return null;
        }

        // Mapeia a entidade Request para GetRequestQueryResponse
        return new GetRequestQueryResponse
        {
            CorrelationId = Guid.Parse(requestEntity.CorrelationId),
            PartnerCode = requestEntity.PartnerCode,
            Type = requestEntity.Type,
            Status = requestEntity.Status,
            CreatedAt = requestEntity.CreatedAt,
            UpdatedAt = requestEntity.UpdatedAt
        };
    }
}

