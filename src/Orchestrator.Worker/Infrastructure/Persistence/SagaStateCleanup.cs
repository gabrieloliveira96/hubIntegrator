using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Worker.Infrastructure.Persistence;

/// <summary>
/// Limpa estados inválidos das sagas no banco de dados
/// </summary>
public static class SagaStateCleanup
{
    public static async Task CleanupInvalidStatesAsync(OrchestratorDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            // Estados válidos (Initial é fornecido automaticamente pelo MassTransit)
            var validStates = new[] { "Initial", "Received", "Validating", "Processing", "Succeeded", "Failed" };
            
            // Encontrar sagas com estados inválidos (vazio, null, ou não definido)
            var invalidSagas = await dbContext.SagaStates
                .Where(s => string.IsNullOrEmpty(s.CurrentState) || !validStates.Contains(s.CurrentState))
                .ToListAsync(cancellationToken);

            if (invalidSagas.Any())
            {
                logger.LogWarning("Found {Count} sagas with invalid states. Cleaning up...", invalidSagas.Count);
                
                foreach (var saga in invalidSagas)
                {
                    logger.LogInformation("Resetting saga {CorrelationId} from invalid state '{State}' to 'Initial'", 
                        saga.CorrelationId, saga.CurrentState ?? "(null)");
                    
                    saga.CurrentState = "Initial";
                }
                
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Cleaned up {Count} invalid saga states", invalidSagas.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up invalid saga states");
        }
    }
}

