using System.Linq;
using System.Text.Json;

namespace Gateway.Yarp.Middleware;

/// <summary>
/// Modelos de resposta de erro para autenticação
/// </summary>
internal static class ErrorResponses
{
    public static async Task WriteMissingTokenError(HttpContext context, string path, string jwtAuthority)
    {
        if (context.Response.HasStarted)
        {
            Serilog.Log.Warning("Response already started, cannot write custom error message");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = "Unauthorized",
            message = "Authorization header is missing",
            details = new
            {
                required = "Authorization header with Bearer token",
                example = "Authorization: Bearer <your-jwt-token>",
                tokenEndpoint = $"{jwtAuthority}/api/token/obter",
                howToGetToken = new
                {
                    method = "POST",
                    url = $"{jwtAuthority}/api/token/obter",
                    body = new
                    {
                        clientId = "hub-client",
                        clientSecret = "hub-secret",
                        scopes = new[] { "hub.api.write", "hub.api.read" }
                    }
                }
            }
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        Serilog.Log.Information("Returning 401 - Missing Authorization header for path: {Path}", path);
        await context.Response.WriteAsync(jsonResponse);
    }

    public static async Task WriteInvalidTokenError(
        HttpContext context, 
        Microsoft.AspNetCore.Authentication.AuthenticateResult authResult, 
        string jwtAuthority)
    {
        if (context.Response.HasStarted)
        {
            Serilog.Log.Warning("Response already started, cannot write custom error message");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";

        var failureMessage = authResult.Failure?.Message ?? "Token validation failed";
        var innerException = authResult.Failure?.InnerException?.Message;
        var fullException = authResult.Failure?.ToString();

        var errorResponse = new
        {
            error = "Unauthorized",
            message = "Invalid or expired JWT token",
            details = new
            {
                received = "Token was provided but validation failed",
                failureReason = failureMessage,
                innerException = innerException,
                fullException = fullException,
                possibleReasons = new[]
                {
                    "Token is expired",
                    "Token signature is invalid",
                    "Token issuer does not match",
                    "Token audience does not match",
                    "IdentityServer may not be running or accessible",
                    "Token format is incorrect"
                },
                tokenEndpoint = $"{jwtAuthority}/api/token/obter",
                identityServerHealth = $"{jwtAuthority}/healthz",
                identityServerDiscovery = $"{jwtAuthority}/.well-known/openid-configuration",
                howToGetNewToken = new
                {
                    method = "POST",
                    url = $"{jwtAuthority}/api/token/obter",
                    body = new
                    {
                        clientId = "hub-client",
                        clientSecret = "hub-secret",
                        scopes = new[] { "hub.api.write", "hub.api.read" }
                    }
                }
            }
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        Serilog.Log.Information("Returning 401 - Invalid token for path: {Path}, Failure: {Failure}",
            context.Request.Path, failureMessage);
        await context.Response.WriteAsync(jsonResponse);
    }

    public static async Task WriteMissingScopeError(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            Serilog.Log.Warning("Response already started, cannot write custom error message");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = 403;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = "Forbidden",
            message = "Required scope not found in token",
            details = new
            {
                required = new[] { "hub.api.write", "hub.api.read" },
                found = context.User.Claims
                    .Where(c => c.Type == "scope")
                    .Select(c => c.Value)
                    .ToArray(),
                howToFix = "Request a token with the required scopes: hub.api.write or hub.api.read"
            }
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        Serilog.Log.Information("Returning 403 - Missing required scope for path: {Path}", context.Request.Path);
        await context.Response.WriteAsync(jsonResponse);
    }
}

