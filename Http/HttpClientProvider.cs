using System.Net;

namespace Tester.Http;

public static class HttpClientProvider
{
    private static readonly Lazy<HttpClient> _lazy = new(() =>
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    });

    public static HttpClient Client => _lazy.Value;
}
