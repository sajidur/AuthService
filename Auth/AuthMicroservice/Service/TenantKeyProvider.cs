using AuthMicroservice.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace AuthMicroservice.Service
{
    // Each tenant signs its users' JWTs with its own Application.AppSecret (see
    // UserService.GetToken) rather than one global key, so validating a token requires
    // looking up the right tenant's secret per-request. JWT bearer's
    // IssuerSigningKeyResolver callback must run synchronously, so this caches the
    // AppKey -> Application lookup (Application rows change rarely) and only blocks on a
    // real async DB call on a cache miss. ASP.NET Core/Kestrel has no capturing
    // SynchronizationContext, so blocking here does not risk a deadlock.
    public interface ITenantKeyProvider
    {
        Application ResolveByAppKey(string appKey);
    }

    public class TenantKeyProvider : ITenantKeyProvider
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _cache;

        public TenantKeyProvider(IServiceScopeFactory scopeFactory, IMemoryCache cache)
        {
            _scopeFactory = scopeFactory;
            _cache = cache;
        }

        public Application ResolveByAppKey(string appKey)
        {
            if (string.IsNullOrWhiteSpace(appKey)) return null;

            return _cache.GetOrCreate($"tenant-appkey:{appKey}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;

                using var scope = _scopeFactory.CreateScope();
                var applicationService = scope.ServiceProvider.GetRequiredService<IApplicationService>();
                var applications = applicationService.GetApplicationsAsync(appKey).GetAwaiter().GetResult();
                return applications.FirstOrDefault(a => a.AppKey == appKey);
            });
        }
    }
}
