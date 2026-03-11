using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace CHEF.Components
{
    internal class ModForumUpdater : Component
    {
        private Timer _timer;

        private bool _running;
        private static Regex _gitRegex = new(@"(https://(github\.com|gitgoon\.dev)/\w+/\w+/releases(/tag/[\w\.\-,\d]+)?)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // check for updates if the post has one of these tags
        private static string[] _watchedTags = { "plugins", "Tools", "Mods/Plugin pack" };

        public ModForumUpdater(DiscordSocketClient client) : base(client) { }

        public override Task SetupAsync()
        {
            _timer = new Timer(state => _ = UpdateModForumTask(null), null, DateTime.Today.AddDays(1).AddHours(2) - DateTime.Now, TimeSpan.FromDays(1)); // Run at 2AM every day

            WatchForCommandInControlChannel("syncforum", ProcessSyncCommand, false);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _timer.Dispose();
        }

        private async Task ProcessSyncCommand(SocketUserMessage msg)
        {
            if (_running)
            {
                await msg.ReplyAsync("Forum sync when it is already running, please wait for it to finish");
            }
            else
            {
                try
                {
                    await UpdateModForumTask(msg);
                }
                catch (Exception ex)
                {
                    await msg.ReplyAsync("Sync failed: " + ex.Message);
                }
            }
        }

        private async Task UpdateModForumTask(SocketUserMessage replyToMessage)
        {
            async Task Log(string msg)
            {
                Logger.Log(msg);
                if (replyToMessage != null)
                {
                    try
                    {
                        await replyToMessage.ReplyAsync(msg.Length >= 2000 ? msg.Substring(0, 2000) : msg);
                    }
                    catch (Exception e)
                    {
                        Logger.Log("Failed to send log message: " + e);
                    }
                }
            }

            if (_running)
            {
                await Log("[ModForumUpdater] WARN: Tried to run update before previous update finished");
                return;
            }

            _running = true;
            try
            {
                var sw = Stopwatch.StartNew();

                foreach (var channel in Client.Guilds.SelectMany(g => g.Channels)
                                              .OfType<SocketForumChannel>()
                                              .Where(c => c.Name.Contains("mod-release") && !c.Name.Contains("archived")))
                {
                    var watchedTagIds = channel.Tags
                        .Where(tag => _watchedTags.Contains(tag.Name))
                        .Select(tag => tag.Id);
                    var modForumThreads = (await GetAllThreads(channel))
                                          // For some reason all threads on the server get returned, not only threads in this channel, so they need to be filtered
                                          .Where(t => !t.IsLocked && t.ParentChannelId == channel.Id)
                                          .Where(t => watchedTagIds.Intersect(t.AppliedTags).Any())
                                          .ToList();

                    await Log($"[ModForumUpdater] Running update in channel [{channel.Name}], {modForumThreads.Count} forum threads to process");

                    foreach (var thread in modForumThreads)
                    {
                        // Avoid spamming endpoints
                        await Task.Delay(100);
                        var allReleaseLinks = await thread.GetMessagesAsync(5)
                                                          .SelectMany(x => x.ToAsyncEnumerable())
                                                          .Select(m => _gitRegex.Match(m.Content))
                                                          .Where(x => x.Success)
                                                          .Select(x => x.Groups[1].Value)
                                                          .ToListAsync();

                        // No hub or goon links, nothing to do
                        if (allReleaseLinks.Count == 0) continue;

                        var mostRecentlyPostedReleaseLink = allReleaseLinks.First();
                        // If there's only one link, be more lax and allow /releases link, otherwise only run if there is a specific /releases/tag/ link
                        var i = mostRecentlyPostedReleaseLink.LastIndexOf(allReleaseLinks.Count > 1 ? "/releases/tag/" : "/releases", StringComparison.OrdinalIgnoreCase);

                        // No release links, probably only a repo link
                        if (i < 0 || mostRecentlyPostedReleaseLink.Contains("KoikatuGameplayMods")) continue;

                        string MakeRelaseLink(string s, string org1, string repo1, string tag)
                        {
                            return "https://" + s + "/" + org1 + "/" + repo1 + "/releases/" + tag;
                        }

                        async Task<(string tag, string link)> GetLatestTag(string website, string org, string repo)
                        {
                            var isGithub = allReleaseLinks.Any(x => x.Contains(website));
                            if (!isGithub) return (null, null);

                            var latestLink = MakeRelaseLink(website, org, repo, "latest");
                            var actualLatestLink = await GetFinalRedirect(latestLink);
                            if (!LinkPointsToReleaseTag(actualLatestLink))
                            {
                                await Log($"[ModForumUpdater] Failed to get redirect for <{latestLink}>");
                                return (null, null);
                            }

                            var match = Regex.Match(actualLatestLink, @"/releases/tag/([\w\.\-,\d]+)$", RegexOptions.IgnoreCase);
                            var latestTag = match.Success ? match.Groups[1].Value : null;
                            if (string.IsNullOrWhiteSpace(latestTag))
                                return (null, null);

                            return (latestTag, actualLatestLink);
                        }

                        // string[6] { "github.com", "IllusionMods", "KKManager", "releases", "tag", "v1.8" }
                        var linkParts = mostRecentlyPostedReleaseLink.Replace("http://", "").Replace("https://", "").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                        var latestGithubTag = await GetLatestTag("github.com", linkParts[1], linkParts[2]);
                        var latestGitgoonTag = await GetLatestTag("gitgoon.dev", linkParts[1], linkParts[2]);

                        if (latestGithubTag.link == null && latestGitgoonTag.link == null)
                            continue;

                        static (string tag, string link) ChooseLatestLink((string tag, string link) tag1, (string tag, string link) tag2)
                        {
                            if (tag1.link == null)
                                return tag2;

                            if (tag2.link == null)
                                return tag1;

                            // If both links are present, compare which one is newer and pick that one

                            // Attempt a proper version comparison first, but fall back to string comparison if the tags aren't in a standard format (e.g., date-based tags)
                            var ghTrim = tag1.tag.Trim(' ', '\'', '"', '\r', '\n').TrimStart('v', 'r');
                            if (!ghTrim.Contains('.')) ghTrim += ".0"; // Append .0 to single number tags to make them parseable as versions
                            var ggTrim = tag2.tag.Trim(' ', '\'', '"', '\r', '\n').TrimStart('v', 'r');
                            if (!ggTrim.Contains('.')) ggTrim += ".0";

                            if (Version.TryParse(ghTrim, out var githubVersion) &&
                                Version.TryParse(ggTrim, out var goonVersion))
                            {
                                return githubVersion >= goonVersion ? tag1 : tag2;
                            }
                            else
                            {
                                // Fallback to string comparison if version parsing fails (works for repos using date)
                                return string.Compare(tag1.tag, tag2.tag, StringComparison.OrdinalIgnoreCase) >= 0
                                    ? tag1
                                    : tag2;
                            }
                        }

                        var actualLatestLink = ChooseLatestLink(latestGithubTag, latestGitgoonTag);

                        if (!string.IsNullOrEmpty(actualLatestLink.link) &&
                            // Make sure it's not already posted
                            !allReleaseLinks.Any(x => x.EndsWith("/" + actualLatestLink.tag, StringComparison.OrdinalIgnoreCase)))
                        {
                            await Log($"[ModForumUpdater] Posting new release link in thread [{thread.Name}] -> <{actualLatestLink.link}>");
                            await thread.SendMessageAsync(actualLatestLink.link);
                        }
                    }
                }

                await Log($"[ModForumUpdater] Finished sync in {sw.ElapsedMilliseconds}ms!");
            }
            catch (Exception e)
            {
                await Log("[ModForumUpdater] ERROR: " + e);
            }
            finally
            {
                _running = false;
            }
        }

        private static bool LinkPointsToReleaseTag(string actualLatestGoonLink)
        {
            return !string.IsNullOrWhiteSpace(actualLatestGoonLink) && !actualLatestGoonLink.EndsWith("/latest");
        }

        private static async Task<List<RestThreadChannel>> GetAllThreads(SocketForumChannel channel)
        {
            var threads = (await channel.GetActiveThreadsAsync()).ToList();
            // get the last 100 archived threads
            var inactiveBatch = await channel
                .GetPublicArchivedThreadsAsync(limit: 100, before: DateTimeOffset.UtcNow);
            while (inactiveBatch.Count > 0)
            {
                threads.AddRange(inactiveBatch);
                // get the last 100 threads archived before the oldest one in the previous batch
                var lastArchiveTime = inactiveBatch.Min(thread => thread.ArchiveTimestamp);
                inactiveBatch = await channel
                    .GetPublicArchivedThreadsAsync(limit: 100, before: lastArchiveTime);
            }
            return threads;
        }

        // https://stackoverflow.com/a/28424940
        private static async Task<string> GetFinalRedirect(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            int maxRedirCount = 8; // prevent infinite loops
            string newUrl = url;
            do
            {
                WebResponse resp = null;
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "HEAD";
                    req.AllowAutoRedirect = false;
                    try
                    {
                        var httpresp = (HttpWebResponse)await req.GetResponseAsync();

                        switch (httpresp.StatusCode)
                        {
                            case HttpStatusCode.OK:
                                return newUrl;
                            case HttpStatusCode.Redirect:
                            case HttpStatusCode.MovedPermanently:
                            case HttpStatusCode.RedirectKeepVerb:
                            case HttpStatusCode.RedirectMethod:
                                // Handle redirects below
                                break;
                            default:
                                return newUrl;
                        }

                        resp = httpresp;
                    }
                    catch (WebException ex)
                    {
                        // Handle redirects below. Needed for .NET Core because for some reason it throws on success
                        if (ex.Message.Contains("301") || ex.Message.Contains("302"))
                            resp = ex.Response;
                        else throw;
                    }

                    newUrl = resp.Headers["Location"];
                    if (newUrl == null)
                        return url;

                    if (newUrl.IndexOf("://", StringComparison.Ordinal) == -1)
                    {
                        // Doesn't have a URL Schema, meaning it's a relative or absolute URL
                        Uri u = new Uri(new Uri(url), newUrl);
                        newUrl = u.ToString();
                    }

                    url = newUrl;
                }
                catch (WebException)
                {
                    // Return the last known good URL
                    return newUrl;
                }
                catch (Exception)
                {
                    return null;
                }
                finally
                {
                    if (resp != null)
                        resp.Close();
                }
            } while (maxRedirCount-- > 0);

            return newUrl;
        }
    }
}
