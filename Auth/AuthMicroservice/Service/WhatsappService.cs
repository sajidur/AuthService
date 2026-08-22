using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface IWhatsappService
    {
        Task<bool> SendMessageAsync(string accessToken, string phoneNumberId, string toPhoneNumber, string message, string templateName = null, string[] templateParams = null);
    }

    // Calls the WhatsApp Cloud API. Free-form text only works within the 24-hour customer-service
    // window since the user's last message; business-initiated messages outside that window must
    // use a pre-approved message template (pass templateName/templateParams), per WhatsApp policy.
    public class WhatsappService : IWhatsappService
    {
        private const string GraphApiVersion = "v19.0";
        private readonly IHttpClientFactory _httpClientFactory;

        public WhatsappService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<bool> SendMessageAsync(string accessToken, string phoneNumberId, string toPhoneNumber, string message, string templateName = null, string[] templateParams = null)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(toPhoneNumber))
                return false;

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var url = $"https://graph.facebook.com/{GraphApiVersion}/{phoneNumberId}/messages";

                object payload;
                if (!string.IsNullOrWhiteSpace(templateName))
                {
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        to = toPhoneNumber,
                        type = "template",
                        template = new
                        {
                            name = templateName,
                            language = new { code = "en_US" },
                            components = (templateParams == null || templateParams.Length == 0)
                                ? null
                                : new object[]
                                {
                                    new
                                    {
                                        type = "body",
                                        parameters = Array.ConvertAll(templateParams, p => (object)new { type = "text", text = p }),
                                    },
                                },
                        },
                    };
                }
                else
                {
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        to = toPhoneNumber,
                        type = "text",
                        text = new { body = message },
                    };
                }

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
