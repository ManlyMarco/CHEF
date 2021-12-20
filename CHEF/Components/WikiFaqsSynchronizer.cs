using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
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
            Client.MessageReceived += msg =>
            {
                try
                {
                    return MsgWatcherAsync(msg);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to handle message {msg.GetJumpUrl()}\n{e}");
                    return Task.CompletedTask;
                }
            };

            await Task.CompletedTask;
        }

        private async Task MsgWatcherAsync(SocketMessage smsg)
        {
            var msg = smsg as SocketUserMessage;
            var channel = msg?.Channel as SocketTextChannel;
            if (channel == null) return;
            if (msg.Author.IsBot || msg.Author.IsWebhook) return;

            var parts = msg.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0] == "syncfaq")
            {
                if (channel.Guild.Id == 560859356439117844UL && channel.Id == 730477794865184789UL ||
                    channel.Guild.Id == 447114928785063977UL && channel.Id == 448958364764864522UL)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var whatif = parts.Length >= 4 && parts[3].Trim().ToLowerInvariant() == "whatif";
                            await SyncFaqChannel(msg, ulong.Parse(parts[1].Trim('#')), new Uri(parts[2].Trim('<', '>', ' ')), whatif);
                        }
                        catch (Exception ex)
                        {
                            await msg.ReplyAsync("Sync failed: " + ex.Message);
                        }
                    });
                }
                else
                {
                    await msg.ReplyAsync("https://tenor.com/view/no-no-no-way-never-nuh-uh-gif-14500720");
                }
            }
        }

        private async Task SyncFaqChannel(SocketUserMessage msg, ulong targetId, Uri sourceUrl, bool whatif)
        {
            if (whatif)
                await msg.ReplyAsync($"Running with whatif flag - Simulating the task, nothing will actually get changed in the target channel.");

            var targetChannel = (SocketTextChannel)((SocketTextChannel)msg.Channel).Guild.GetChannel(targetId);
            if (targetChannel is SocketThreadChannel stc && stc.IsArchived)
            {
                await msg.ReplyAsync($"Thread <#{targetId}> is archived, aborting. Unarchive the thread and try again.");
                return;
            }

            var maxMessageCount = 200;
            var oldMessages = (await targetChannel.GetMessagesAsync(maxMessageCount).FlattenAsync()).ToList();
            if (oldMessages.Count == maxMessageCount)
            {
                await msg.ReplyAsync($"Over {maxMessageCount} messages found in channel <#{targetId}>, something doesn't seem right. Aborting.");
                return;
            }

            foreach (var message in oldMessages)
            {
                if (message.Author.Id != Client.CurrentUser.Id)
                {
                    await msg.ReplyAsync(
                        $"Could not run command - channel <#{targetId}> contains a message that doesn't belong to me: " +
                        message.GetJumpUrl());
                    return;
                }
            }

            var docNode = await LoadHtml(sourceUrl);
            var rootNode = docNode.SelectSingleNode("//div[@id='mw-content-text']");
            var converter = new Html2Markdown.Converter();
            var newMessageContents = new List<string>();
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

            if (newMessageContents.Count < 5)
            {
                await msg.ReplyAsync($"Too few QA lines found in the URL ({newMessageContents.Count}). Aborting.");
                return;
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
                var sanitizedMessageContent = Regex.Replace(messageContent, @"\[.+?\]\((\S+)\)", "<$1>");

                if (!whatif)
                    await targetChannel.SendMessageAsync(sanitizedMessageContent);
            }

            if (!whatif)
                await targetChannel.SendMessageAsync($"This is a read-only copy of <{sourceUrl}> pulled at {DateTime.Now:yyy/MM/dd HH:mm:ss}.");

            await msg.ReplyAsync($"Finished syncing channel <#{targetId}>!{(targetChannel is SocketThreadChannel ? " Remember to archive the thread!" : "")}");
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
