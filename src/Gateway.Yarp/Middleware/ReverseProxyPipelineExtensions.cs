using Serilog;
using Yarp.ReverseProxy.Forwarder;

namespace Gateway.Yarp.Middleware;

/// <summary>
/// Extensões para configurar o pipeline do YARP Reverse Proxy
/// </summary>
public static class ReverseProxyPipelineExtensions
{
    /// <summary>
    /// Adiciona middleware ao pipeline do YARP para forward de correlation ID e logging
    /// </summary>
    public static void UseCorrelationIdForwarding(this IReverseProxyApplicationBuilder pipeline)
    {
        pipeline.Use(async (context, next) =>
        {
            var correlationId = context.Items["CorrelationId"]?.ToString();
            if (!string.IsNullOrEmpty(correlationId))
            {
                context.Request.Headers["X-Correlation-Id"] = correlationId;
            }

            Log.Information("Proxying request to backend: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await next();

            Log.Information("Proxy response: {StatusCode}, ContentLength: {ContentLength}, HasStarted: {HasStarted}",
                context.Response.StatusCode,
                context.Response.ContentLength,
                context.Response.HasStarted);
        });
    }
}

