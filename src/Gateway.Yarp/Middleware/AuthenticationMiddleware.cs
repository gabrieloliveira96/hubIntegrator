using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Gateway.Yarp.Middleware;

/// <summary>
/// Middleware para validar autenticação JWT antes de rotear requisições via YARP
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public AuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";
        var requiresAuth = RequiresAuthentication(path);
        var jwtAuthority = _configuration["Jwt:Authority"];

        Log.Debug("Auth middleware - Path: {Path}, RequiresAuth: {RequiresAuth}, HasAuthority: {HasAuthority}",
            path, requiresAuth, !string.IsNullOrEmpty(jwtAuthority));

        if (!requiresAuth || string.IsNullOrEmpty(jwtAuthority))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].ToString();
        var hasAuthHeader = !string.IsNullOrEmpty(authHeader) && 
                           authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

        // Verificar se tem header de autorização
        if (!hasAuthHeader)
        {
            await ErrorResponses.WriteMissingTokenError(context, path, jwtAuthority);
            return;
        }

        // Autenticar token JWT
        var authResult = await AuthenticateTokenAsync(context);
        
        if (!authResult.Succeeded)
        {
            await ErrorResponses.WriteInvalidTokenError(context, authResult, jwtAuthority);
            return;
        }

        // Atualizar contexto do usuário
        if (authResult.Principal != null)
        {
            context.User = authResult.Principal;
        }

        // Verificar scopes
        if (!HasRequiredScopes(context.User))
        {
            await ErrorResponses.WriteMissingScopeError(context);
            return;
        }

        // Adicionar headers para o backend
        AddBackendHeaders(context);

        await _next(context);
    }

    private static bool RequiresAuthentication(string path)
    {
        return (path.StartsWith("/api/requests") || path.StartsWith("/requests")) &&
               !path.Contains("/swagger") &&
               !path.Contains("/healthz") &&
               !path.Contains("/readyz");
    }

    private async Task<AuthenticateResult> AuthenticateTokenAsync(HttpContext context)
    {
        try
        {
            return await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Erro ao autenticar token JWT: {Message}", ex.Message);
            return AuthenticateResult.Fail(ex);
        }
    }

    private static bool HasRequiredScopes(System.Security.Claims.ClaimsPrincipal user)
    {
        return user.HasClaim("scope", "hub.api.write") ||
               user.HasClaim("scope", "hub.api.read");
    }

    private void AddBackendHeaders(HttpContext context)
    {
        if (context.User.Identity?.Name != null)
        {
            context.Request.Headers["X-Authenticated-User"] = context.User.Identity.Name;
        }

        var partnerCode = context.User.FindFirst("partner_code")?.Value;
        if (!string.IsNullOrEmpty(partnerCode))
        {
            context.Request.Headers["X-Partner-Code"] = partnerCode;
        }
    }

}

/// <summary>
/// Extension methods para registrar o middleware de autenticação
/// </summary>
public static class AuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuthenticationMiddleware>();
    }
}

