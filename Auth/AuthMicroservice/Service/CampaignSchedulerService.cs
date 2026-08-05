using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public class CampaignSchedulerService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CampaignSchedulerService> _logger;

        public CampaignSchedulerService(IServiceScopeFactory scopeFactory, ILogger<CampaignSchedulerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await SendDueCampaignsAsync(stoppingToken);

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected during shutdown.
                }
            }
        }

        private async Task SendDueCampaignsAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var campaignService = scope.ServiceProvider.GetRequiredService<ICampaignService>();

                var dueCampaigns = await campaignService.GetDueScheduledCampaignsAsync();
                foreach (var campaign in dueCampaigns)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        await campaignService.SendCampaignAsync(campaign.Id.ToString(), campaign.ApplicationId);
                        _logger.LogInformation("Scheduled campaign {CampaignId} ({Name}) sent.", campaign.Id, campaign.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send scheduled campaign {CampaignId}.", campaign.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking for due scheduled campaigns.");
            }
        }
    }
}
