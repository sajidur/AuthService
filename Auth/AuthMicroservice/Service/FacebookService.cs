using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface IFacebookService
    {
        Task<bool> SendMessageAsync(string pageAccessToken, string recipientPsid, string message);
    }

    // Calls the Meta Send API (Messenger Platform). Note: Meta only allows business-initiated
    // messages to a user within a 24-hour window since their last message to the Page, or under
    // a narrow set of approved message tags (e.g. ACCOUNT_UPDATE) — general marketing broadcasts
    // require Meta's separate Sponsored Messages product and policy approval. This sends a
    // standard reply-window message; it will fail outside that window until tagging/approval
    // is added on top of this.
    public class FacebookService : IFacebookService
    {
        private const string GraphApiVersion = "v19.0";
        private readonly IHttpClientFactory _httpClientFactory;

        public FacebookService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<bool> SendMessageAsync(string pageAccessToken, string recipientPsid, string message)
        {
            if (string.IsNullOrWhiteSpace(pageAccessToken) || string.IsNullOrWhiteSpace(recipientPsid) || string.IsNullOrWhiteSpace(message))
                return false;

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://graph.facebook.com/{GraphApiVersion}/me/messages?access_token={Uri.EscapeDataString(pageAccessToken)}";

                var payload = new
                {
                    recipient = new { id = recipientPsid },
                    message = new { text = message },
                    messaging_type = "RESPONSE",
                };

                var response = await client.PostAsJsonAsync(url, payload);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
