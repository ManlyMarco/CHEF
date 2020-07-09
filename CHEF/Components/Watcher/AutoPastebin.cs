using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace CHEF.Components.Watcher
{
    public class AutoPastebin
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private readonly string _siteUrl;
        private readonly string _postUrl;

        public AutoPastebin(string siteUrl = "https://hastebin.com/")
        {
            if (!siteUrl.EndsWith("/"))
            {
                siteUrl += "/";
            }
            _siteUrl = siteUrl;
            _postUrl = siteUrl + "documents/";
        }

        internal async Task<string> Try(string fileContent)
        {
            if (fileContent.Length >= 400000) return string.Empty;

            var pasteResult = await PostBin(fileContent);

            if (!pasteResult.IsSuccess)
                throw new IOException($"HTTP Error code {pasteResult.StatusCode}");

            return pasteResult.FullUrl;
        }

    private async Task<HasteBinResult> PostBin(string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_postUrl))
        {
            Content = new StringContent(content)
        };
        HttpResponseMessage result = await HttpClient.SendAsync(request);

        if (result.IsSuccessStatusCode)
        {
            string json = await result.Content.ReadAsStringAsync();
            var hasteBinResult = JsonConvert.DeserializeObject<HasteBinResult>(json);

            if (hasteBinResult?.Key != null)
            {
                hasteBinResult.FullUrl = $"{_siteUrl}{hasteBinResult.Key}";
                hasteBinResult.IsSuccess = true;
                hasteBinResult.StatusCode = 200;
                return hasteBinResult;
            }
        }

        return new HasteBinResult
        {
            FullUrl = _siteUrl,
            IsSuccess = false,
            StatusCode = (int)result.StatusCode
        };
    }
}

public class HasteBinResult
{
    public string Key { get; set; }
    public string FullUrl { get; set; }
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
}
}
