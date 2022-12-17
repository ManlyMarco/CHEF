using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Html2Markdown;
using HtmlAgilityPack;

namespace CHEF.Components
{
    public class WikiFaqsSynchronizer : Component
    {
        public WikiFaqsSynchronizer(DiscordSocketClient client) : base(client)
        {
        }

        public override async Task SetupAsync()
        {
            WatchForCommandInControlChannel("syncfaq", ProcessSyncCommand, false);

            await Task.CompletedTask;
        }

        private async Task ProcessSyncCommand(SocketUserMessage msg)
        {
            try
            {
                var parts = msg.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var whatif = parts.Length >= 4 && parts[3].Trim().ToLowerInvariant() == "whatif";
                await SyncFaqChannel(msg, ulong.Parse(parts[1].Trim('#')), new Uri(parts[2].Trim('<', '>', ' ')), whatif);
            }
            catch (Exception ex)
            {
                await msg.ReplyAsync("Sync failed: " + ex.Message);
            }
        }

        private async Task SyncFaqChannel(SocketUserMessage msg, ulong targetId, Uri sourceUrl, bool whatif)
        {
            var sw = Stopwatch.StartNew();
            if (whatif)
                await msg.ReplyAsync($"Running with whatif flag - Simulating the task, nothing will actually get changed in the target channel.");

            var targetChannel = (SocketTextChannel)((SocketTextChannel)msg.Channel).Guild.GetChannel(targetId);
            if (targetChannel is null)
                throw new Exception($"Could not find channel <#{targetId}>. If it's a thread, make sure it is not archived.");

            if (targetChannel is SocketThreadChannel stc && stc.IsArchived)
                throw new Exception($"Thread <#{targetId}> is archived, aborting. Unarchive the thread and try again.");

            var myPerms = targetChannel.Guild.CurrentUser.GetPermissions(targetChannel);
            if (!myPerms.Has(ChannelPermission.ManageMessages))
                throw new Exception($"I don't have ManageMessages permission in the <#{targetId}> channel.");

            var maxMessageCount = 200;
            var oldMessages = (await targetChannel.GetMessagesAsync(maxMessageCount).FlattenAsync()).ToList();
            if (oldMessages.Count == maxMessageCount)
            {
                throw new Exception($"Over {maxMessageCount} messages found in channel <#{targetId}>, something doesn't seem right. Aborting.");
            }

            foreach (var message in oldMessages)
            {
                if (message.Author.Id != Client.CurrentUser.Id)
                {
                    throw new Exception($"Channel <#{targetId}> contains a message that doesn't belong to me: {message.GetJumpUrl()}");
                }
            }

            var newMessageContents = new List<string>();
            try
            {
                var docNode = await LoadHtml(sourceUrl);
                var rootNode = docNode.SelectSingleNode("//div[@id='mw-content-text']");
                var converter = new Converter();
                foreach (var childNode in rootNode.ChildNodes)
                {
                    if (childNode.Name == "h1")
                    {
                        var markdown = converter.Convert(childNode.FirstChild.InnerHtml).Trim();
                        if (markdown.Length > 0)
                            newMessageContents.Add($"```#### {markdown} ####```");
                    }
                    else if (childNode.Name == "ul")
                    {
                        foreach (var listNode in childNode.ChildNodes)
                        {
                            var markdown = converter.Convert(listNode.InnerHtml).Trim();
                            if (markdown.Length > 0)
                            {
                                // Add separators at the end so messages show up properly when searching the channel
                                markdown = markdown + "\r\n-----";
                                newMessageContents.Add(markdown);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to parse the wiki page at {sourceUrl}\nMake sure the URL is pointing at a FAQ page on the hgames wiki, and that the page is in correct format.\nError message: " + e.Message);
            }

            if (newMessageContents.Count < 5)
            {
                throw new Exception($"Too few QA lines found in {newMessageContents.Count}, aborting.\nMake sure the URL is pointing at a FAQ page on the hgames wiki, and that the page is in correct format.");
            }

            await msg.ReplyAsync($"Deleting {oldMessages.Count} of my old messages in channel <#{targetId}>");
            foreach (var message in oldMessages)
            {
                if (!whatif)
                {
                    await message.DeleteAsync();
                    await Task.Delay(500);
                }
            }

            await msg.ReplyAsync($"Spawning {newMessageContents.Count} new messages in channel <#{targetId}>");
            foreach (var messageContent in newMessageContents)
            {
                var sanitizedMessageContent = Regex.Replace(Regex.Replace(messageContent, @"\[.+?\]\((\S+)\)", "<$1>"), @"(\r?\n *)+", "\r\n");

                if (!whatif)
                {
                    var sent = await targetChannel.SendMessageAsync(sanitizedMessageContent);
                    if (sanitizedMessageContent.StartsWith("```####"))
                        await sent.PinAsync();
                }
            }



            if (!whatif)
            {
                // Remove the "message was pinned" notifications to clean things up
                await foreach (var pinMsg in targetChannel.GetMessagesAsync(maxMessageCount).Flatten().Where(x => x.Type == MessageType.ChannelPinnedMessage))
                    await pinMsg.DeleteAsync();

                await targetChannel.SendMessageAsync($"*This is a read-only copy of <{sourceUrl}> pulled at {DateTime.Now:yyy/MM/dd HH:mm:ss}. Notify a moderator if you updated the wiki and want it synced.*");
            }

            await msg.ReplyAsync($"Successfully finished syncing channel <#{targetId}> in {sw.Elapsed:hh\\:mm\\:ss}!{(targetChannel is SocketThreadChannel ? "\nRemember to archive the thread!" : "")}");
        }

        public static async Task<HtmlNode> LoadHtml(Uri sourceUrl)
        {
            var web = new HtmlWeb();
            var htmlDoc = await web.LoadFromWebAsync(sourceUrl.ToString());
            var docNode = htmlDoc.DocumentNode;
            return docNode;
        }
    }
}
