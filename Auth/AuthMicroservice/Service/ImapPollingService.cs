using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public class ImapPollingService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ImapPollingService> _logger;

        public ImapPollingService(IServiceScopeFactory scopeFactory, ILogger<ImapPollingService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await PollAllMailboxesAsync(stoppingToken);

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

        private async Task PollAllMailboxesAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var smtpConfigRepository = scope.ServiceProvider.GetRequiredService<ISmtpConfigRepository>();

            var configs = (await smtpConfigRepository.GetAllAsync())
                .Where(c => c.ImapEnabled && !string.IsNullOrWhiteSpace(c.Host))
                .ToList();

            foreach (var config in configs)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await PollMailboxAsync(config, scope.ServiceProvider, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "IMAP poll failed for application {ApplicationId} ({Host}).", config.ApplicationId, config.Host);
                }
            }
        }

        private async Task PollMailboxAsync(SmtpConfig config, IServiceProvider services, CancellationToken stoppingToken)
        {
            var smtpConfigRepository = services.GetRequiredService<ISmtpConfigRepository>();
            var contactRepository = services.GetRequiredService<IContactRepository>();
            var contactActivityService = services.GetRequiredService<IContactActivityService>();

            using var client = new ImapClient { Timeout = 15000 };
            var socketOptions = config.ImapEnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(config.Host, config.ImapPort ?? 993, socketOptions, stoppingToken);
            await client.AuthenticateAsync(config.Username, config.Password, stoppingToken);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, stoppingToken);

            if (config.LastProcessedImapUid == null)
            {
                // First-ever poll for this mailbox: baseline to "now" instead of importing years of history.
                config.LastProcessedImapUid = inbox.UidNext.HasValue ? (long)inbox.UidNext.Value.Id - 1 : 0;
                await smtpConfigRepository.UpdateAsync(config);
                await client.DisconnectAsync(true, stoppingToken);
                return;
            }

            var fromUid = new UniqueId((uint)config.LastProcessedImapUid.Value + 1);
            var query = SearchQuery.Uids(new UniqueIdRange(fromUid, UniqueId.MaxValue));
            var uids = await inbox.SearchAsync(query, stoppingToken);

            long highestProcessedUid = config.LastProcessedImapUid.Value;
            var selfAddress = (config.FromAddress ?? config.Username)?.Trim().ToLowerInvariant();

            foreach (var uid in uids)
            {
                if (stoppingToken.IsCancellationRequested) break;
                if (uid.Id <= (uint)config.LastProcessedImapUid.Value) continue;

                try
                {
                    var message = await inbox.GetMessageAsync(uid, stoppingToken);
                    var senderAddress = message.From.Mailboxes.FirstOrDefault()?.Address?.Trim().ToLowerInvariant();

                    if (!string.IsNullOrWhiteSpace(senderAddress) && senderAddress != selfAddress)
                    {
                        var contact = await contactRepository.GetByEmailAndApplicationIdAsync(senderAddress, config.ApplicationId);
                        if (contact != null)
                        {
                            var body = message.TextBody ?? message.HtmlBody ?? string.Empty;
                            await contactActivityService.LogActivityForContactAsync(
                                config.ApplicationId,
                                contact.Id,
                                "Email",
                                message.Subject ?? "(no subject)",
                                body,
                                "Received");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process IMAP message UID {Uid} for application {ApplicationId}.", uid.Id, config.ApplicationId);
                }

                if (uid.Id > (uint)highestProcessedUid) highestProcessedUid = uid.Id;
            }

            if (highestProcessedUid != config.LastProcessedImapUid.Value)
            {
                config.LastProcessedImapUid = highestProcessedUid;
                await smtpConfigRepository.UpdateAsync(config);
            }

            await client.DisconnectAsync(true, stoppingToken);
        }
    }
}
