using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface ISmsService
    {
        Task<bool> SendSmsAsync(string phoneNumber, string message);
    }

    public class SmsService : ISmsService
    {
        private const string ApiKey = "zzTXYcqGRBpmZJCKKifM";
        private const string SenderId = "8809617624929";

        public Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(message))
                return Task.FromResult(false);

            try
            {
                var encodedMessage = Uri.EscapeDataString(message);
                var url = $"http://bulksmsbd.net/api/smsapi?api_key={ApiKey}&type=text&number={phoneNumber}&senderid={SenderId}&message={encodedMessage}";

                var webRequest = WebRequest.Create(url);
                using var response = (HttpWebResponse)webRequest.GetResponse();
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                reader.ReadToEnd();

                return Task.FromResult(response.StatusCode == HttpStatusCode.OK);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}
