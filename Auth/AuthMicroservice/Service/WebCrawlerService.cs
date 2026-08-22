using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace AuthMicroservice.Service
{
    public class CrawledPage
    {
        public string Url { get; set; }
        public string Text { get; set; }
    }

    public interface IWebCrawlerService
    {
        Task<(bool success, string error, List<CrawledPage> pages)> CrawlAsync(string startUrl, int maxPages);
    }

    // Fetches a start URL plus a handful of same-domain "contact us"/"about"/"team"-looking
    // pages and returns their visible text, for ContactExtractionService to run ML.NET
    // classification over. Since this takes an arbitrary tenant-supplied URL, every hop
    // (including redirects) is re-validated to reject private/loopback/link-local addresses
    // (an authenticated tenant could otherwise use this as an SSRF probe into internal
    // infrastructure or the cloud metadata endpoint at 169.254.169.254).
    public class WebCrawlerService : IWebCrawlerService
    {
        public const string HttpClientName = "WebCrawler";

        private const int MaxPageBytes = 3_000_000;
        private const int MaxTextPerPage = 200_000;
        private const int DefaultMaxPages = 5;
        private const int MaxPagesLimit = 10;
        private const int MaxRedirectsPerPage = 5;

        private static readonly string[] RelevantLinkKeywords =
        {
            "contact", "about", "team", "staff", "people", "leadership", "management", "directory"
        };

        private readonly IHttpClientFactory _httpClientFactory;

        public WebCrawlerService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<(bool success, string error, List<CrawledPage> pages)> CrawlAsync(string startUrl, int maxPages)
        {
            if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var startUri) || !IsHttpScheme(startUri))
                return (false, "A valid http(s) URL is required.", null);

            maxPages = Math.Clamp(maxPages <= 0 ? DefaultMaxPages : maxPages, 1, MaxPagesLimit);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startUri.AbsoluteUri };
            var queue = new Queue<Uri>();
            queue.Enqueue(startUri);

            var pages = new List<CrawledPage>();
            string firstError = null;

            while (queue.Count > 0 && pages.Count < maxPages)
            {
                var current = queue.Dequeue();
                var (ok, html, error) = await FetchSafeAsync(client, current);
                if (!ok)
                {
                    firstError ??= error;
                    continue;
                }

                var (text, links) = ParseHtml(html, current);
                if (!string.IsNullOrWhiteSpace(text))
                    pages.Add(new CrawledPage { Url = current.AbsoluteUri, Text = text });

                // Only branch out from the start page itself, to same-host links that look
                // like they'd contain contact info — this is a shallow, targeted crawl, not
                // a general-purpose spider.
                if (current == startUri)
                {
                    var candidates = links
                        .Where(l => Uri.Compare(l, startUri, UriComponents.Host, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0)
                        .Where(l => RelevantLinkKeywords.Any(k => l.AbsolutePath.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        .Distinct()
                        .Take(maxPages - 1);

                    foreach (var link in candidates)
                    {
                        if (visited.Add(link.AbsoluteUri)) queue.Enqueue(link);
                    }
                }
            }

            if (pages.Count == 0)
                return (false, firstError ?? "Could not fetch any content from that URL.", null);

            return (true, null, pages);
        }

        private static bool IsHttpScheme(Uri uri) =>
            uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

        private async Task<(bool ok, string html, string error)> FetchSafeAsync(HttpClient client, Uri startingUri)
        {
            var current = startingUri;

            for (var hop = 0; hop <= MaxRedirectsPerPage; hop++)
            {
                if (!IsHttpScheme(current))
                    return (false, null, "Only http/https URLs are allowed.");

                if (!await IsPublicHostAsync(current.Host))
                    return (false, null, $"{current.Host} resolves to a private or internal address, which is not allowed.");

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.UserAgent.ParseAdd("NewsletterContactCrawler/1.0");
                request.Headers.Accept.ParseAdd("text/html");

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                }
                catch
                {
                    return (false, null, $"Could not reach {current}.");
                }

                using (response)
                {
                    var status = (int)response.StatusCode;
                    if (status is 301 or 302 or 303 or 307 or 308)
                    {
                        var location = response.Headers.Location;
                        if (location == null) return (false, null, $"{current} redirected without a Location header.");
                        current = location.IsAbsoluteUri ? location : new Uri(current, location);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                        return (false, null, $"Received status {status} from {current}.");

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                    if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
                        !contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
                        return (false, null, $"{current} did not return an HTML page.");

                    var html = await ReadCappedAsync(response, MaxPageBytes);
                    return (true, html, null);
                }
            }

            return (false, null, $"Too many redirects fetching {startingUri}.");
        }

        private static async Task<string> ReadCappedAsync(HttpResponseMessage response, int maxBytes)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                var remaining = maxBytes - (int)buffer.Length;
                if (remaining <= 0) break;
                await buffer.WriteAsync(chunk.AsMemory(0, Math.Min(read, remaining)));
                if (read > remaining) break;
            }

            var encoding = response.Content.Headers.ContentType?.CharSet is string charset
                ? TryGetEncoding(charset)
                : Encoding.UTF8;
            return encoding.GetString(buffer.ToArray());
        }

        private static Encoding TryGetEncoding(string charset)
        {
            try { return Encoding.GetEncoding(charset); }
            catch { return Encoding.UTF8; }
        }

        private static async Task<bool> IsPublicHostAsync(string host)
        {
            IPAddress[] addresses;
            if (IPAddress.TryParse(host, out var literal))
            {
                addresses = new[] { literal };
            }
            else
            {
                try
                {
                    addresses = await Dns.GetHostAddressesAsync(host);
                }
                catch
                {
                    return false;
                }
            }

            return addresses.Length > 0 && addresses.All(a => !IsBlockedAddress(a));
        }

        private static bool IsBlockedAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

            if (IPAddress.IsLoopback(address)) return true;

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();
                if (b[0] == 10) return true;                                  // 10.0.0.0/8
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;      // 172.16.0.0/12
                if (b[0] == 192 && b[1] == 168) return true;                  // 192.168.0.0/16
                if (b[0] == 169 && b[1] == 254) return true;                  // 169.254.0.0/16 (incl. cloud metadata)
                if (b[0] == 0) return true;                                   // 0.0.0.0/8
                if (b[0] >= 224) return true;                                 // multicast/reserved
            }
            else if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return true;
                var b = address.GetAddressBytes();
                if ((b[0] & 0xfe) == 0xfc) return true;                       // fc00::/7 unique local
            }

            return false;
        }

        private static (string text, List<Uri> links) ParseHtml(string html, Uri baseUri)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            doc.DocumentNode.SelectNodes("//script|//style|//noscript|//svg")?.ToList()
                .ForEach(n => n.Remove());

            var text = WebUtility.HtmlDecode(doc.DocumentNode.InnerText ?? "");
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n\s*\n\s*", "\n\n").Trim();
            if (text.Length > MaxTextPerPage) text = text.Substring(0, MaxTextPerPage);

            var links = new List<Uri>();
            var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchors != null)
            {
                foreach (var a in anchors)
                {
                    var href = a.GetAttributeValue("href", "");
                    if (string.IsNullOrWhiteSpace(href)) continue;
                    if (href.StartsWith('#') || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                        href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                        href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (Uri.TryCreate(baseUri, href, out var resolved) && IsHttpScheme(resolved))
                        links.Add(resolved);
                }
            }

            return (text, links);
        }
    }
}
