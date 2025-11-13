using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchestrator.Worker.Infrastructure.Persistence;
using Orchestrator.Worker.Services;
using Shared.Contracts;

namespace Orchestrator.Worker.Sagas;

public class RequestSaga : MassTransitStateMachine<SagaStateMap>
{
    public RequestSaga()
    {
        // Define the initial state - CurrentState is a string property
        InstanceState(x => x.CurrentState);

        // Configure events with proper correlation
        Event(() => RequestReceivedEvent, x => 
        {
            x.CorrelateById(m => m.Message.CorrelationId);
            x.InsertOnInitial = true; // Allow creating new saga instances
        });
        
        Event(() => RequestCompletedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => RequestFailedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
            When(RequestReceivedEvent)
                .Then(context =>
                {
                    // Set saga properties
                    context.Saga.PartnerCode = context.Message.PartnerCode;
                    context.Saga.RequestType = context.Message.Type;
                    context.Saga.Payload = context.Message.Payload.ToString();
                    context.Saga.CreatedAt = context.Message.CreatedAt;
                    
                    // CurrentState will be set automatically by MassTransit to Initial
                    // But ensure it's not empty if somehow it is
                    if (string.IsNullOrWhiteSpace(context.Saga.CurrentState))
                    {
                        context.Saga.CurrentState = Initial.Name;
                    }
                })
                .ThenAsync(async context =>
                {
                    // Validate and enrich
                    var businessRulesService = context.GetServiceOrCreateInstance<IBusinessRulesService>();
                    await businessRulesService.ValidateRequestAsync(context.Message.PartnerCode, context.Message.Type, context.CancellationToken);
                    await businessRulesService.EnrichRequestDataAsync(context.Saga.CorrelationId, context.Saga.PartnerCode, context.CancellationToken);
                })
                .Publish(context => new DispatchToPartner(
                    context.Saga.CorrelationId,
                    context.Saga.PartnerCode,
                    // Use HTTP in development, HTTPS in production
                    new Uri($"http://localhost:8080/mock-partner/{context.Saga.PartnerCode}"),
                    System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(context.Saga.Payload ?? "{}")))
                .TransitionTo(Processing)
        );

        During(Processing,
            When(RequestCompletedEvent)
                .Then(context =>
                {
                    context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
                })
                .TransitionTo(Succeeded),
            When(RequestFailedEvent)
                .Then(context =>
                {
                    context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
                })
                .TransitionTo(Failed)
        );

        // Handle duplicate events in final states (idempotency)
        // If we receive RequestCompleted when already Succeeded, ignore it
        During(Succeeded,
            When(RequestCompletedEvent)
                .Then(context =>
                {
                    // Event already processed, just log and ignore
                    // This ensures idempotency for duplicate messages
                    var logger = context.GetPayload<ILogger<RequestSaga>>();
                    logger?.LogWarning(
                        "Received duplicate RequestCompleted event for saga {CorrelationId} in state {State}. Ignoring.",
                        context.Message.CorrelationId,
                        context.Saga.CurrentState);
                }),
            // If we receive RequestFailed when already Succeeded, log but don't change state
            // (saga already succeeded, can't fail now)
            When(RequestFailedEvent)
                .Then(context =>
                {
                    var logger = context.GetPayload<ILogger<RequestSaga>>();
                    logger?.LogWarning(
                        "Received RequestFailed event for saga {CorrelationId} in state {State}. Saga already succeeded, ignoring.",
                        context.Message.CorrelationId,
                        context.Saga.CurrentState);
                })
        );

        // If we receive RequestFailed when already Failed, ignore it
        During(Failed,
            When(RequestFailedEvent)
                .Then(context =>
                {
                    // Event already processed, just log and ignore
                    // This ensures idempotency for duplicate messages
                    var logger = context.GetPayload<ILogger<RequestSaga>>();
                    logger?.LogWarning(
                        "Received duplicate RequestFailed event for saga {CorrelationId} in state {State}. Ignoring.",
                        context.Message.CorrelationId,
                        context.Saga.CurrentState);
                }),
            // If we receive RequestCompleted when already Failed, log but don't change state
            // (saga already failed, can't succeed now)
            When(RequestCompletedEvent)
                .Then(context =>
                {
                    var logger = context.GetPayload<ILogger<RequestSaga>>();
                    logger?.LogWarning(
                        "Received RequestCompleted event for saga {CorrelationId} in state {State}. Saga already failed, ignoring.",
                        context.Message.CorrelationId,
                        context.Saga.CurrentState);
                })
        );

        // Mark final states as completed
        SetCompletedWhenFinalized();
    }

    // States must be initialized as properties
    // Note: Initial state is provided by MassTransitStateMachine base class
    public State Received { get; private set; } = null!;
    public State Validating { get; private set; } = null!;
    public State Processing { get; private set; } = null!;
    public State Succeeded { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<RequestReceived> RequestReceivedEvent { get; private set; } = null!;
    public Event<RequestCompleted> RequestCompletedEvent { get; private set; } = null!;
    public Event<RequestFailed> RequestFailedEvent { get; private set; } = null!;
}


