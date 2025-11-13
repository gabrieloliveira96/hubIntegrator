using System.Net.Http;
using Polly;
using Shared.Policies;

namespace Orchestrator.Worker.Policies;

// Re-export for backward compatibility
public static class ResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => Shared.Policies.ResiliencePolicies.GetRetryPolicy();
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() => Shared.Policies.ResiliencePolicies.GetCircuitBreakerPolicy();
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy() => Shared.Policies.ResiliencePolicies.GetTimeoutPolicy();
    public static IAsyncPolicy<HttpResponseMessage> GetBulkheadPolicy(int maxParallelization = 10) => Shared.Policies.ResiliencePolicies.GetBulkheadPolicy(maxParallelization);
    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy() => Shared.Policies.ResiliencePolicies.GetCombinedPolicy();
}

