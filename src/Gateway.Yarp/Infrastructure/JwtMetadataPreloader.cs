using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Serilog;

namespace Gateway.Yarp.Infrastructure;

/// <summary>
/// Classe responsável por pré-carregar metadados JWT em desenvolvimento
/// </summary>
public static class JwtMetadataPreloader
{
    /// <summary>
    /// Pré-carrega metadados JWT do provedor OIDC configurado
    /// </summary>
    public static void PreloadJwtMetadata(IServiceProvider services, string? jwtAuthority)
    {
        if (string.IsNullOrEmpty(jwtAuthority))
        {
            Log.Information("JWT Authority não configurado - pulando pré-carregamento de metadados");
            return;
        }

        try
        {
            Log.Information("Pré-carregando metadados JWT do provedor OIDC...");
            var jwtBearerOptions = services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
            var options = jwtBearerOptions.Get(JwtBearerDefaults.AuthenticationScheme);

            if (options?.ConfigurationManager != null)
            {
                // Forçar carregamento síncrono dos metadados
                var configTask = options.ConfigurationManager.GetConfigurationAsync(CancellationToken.None);
                var config = configTask.GetAwaiter().GetResult(); // Bloquear até carregar

                Log.Information("Metadados JWT carregados com sucesso. Issuer: {Issuer}, Chaves JWKS: {KeyCount}",
                    config.Issuer, config.SigningKeys?.Count ?? 0);

                if (config.SigningKeys == null || config.SigningKeys.Count == 0)
                {
                    Log.Warning("ATENÇÃO: Nenhuma chave JWKS foi carregada!");
                }
            }
            else
            {
                Log.Information("Autenticação JWT não configurada - Gateway funcionará sem validação de token (apenas rate limiting)");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Não foi possível pré-carregar metadados JWT. O Gateway continuará funcionando.");
        }
    }
}

