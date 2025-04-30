using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CHEF.Extensions;
using Discord;
using Discord.WebSocket;
using Html2Markdown;
using HtmlAgilityPack;

namespace CHEF.Components
{
    public class WikiFaqsSynchronizer : Component
    {
        private const string SyncWikiToFaq = "sync-wiki-to-faq";

        public WikiFaqsSynchronizer(DiscordSocketClient client) : base(client)
        {
        }

        public override async Task SetupAsync()
        {
            var cmd = await Client.CreateGlobalApplicationCommandAsync(new SlashCommandBuilder().WithName(SyncWikiToFaq)
                                                                                                .WithDescription("Post all Q/A from wiki to a channel, remove old Q/A posts in channel if any")
                                                                                                .AddOption("channel", ApplicationCommandOptionType.Channel, "FAQ channel")
                                                                                                .AddOption("url", ApplicationCommandOptionType.String, "URL to the wiki page")
                                                                                                .AddOption("simulate", ApplicationCommandOptionType.Boolean, "If set to 'true', the command will simulate the task without making any changes")
                                                                                                .WithDefaultMemberPermissions(GuildPermission.Administrator)
                                                                                                .WithContextTypes(InteractionContextType.Guild)
                                                                                                .Build());
            Client.ListenToSlashCommand(cmd, SyncFaqChannel);
        }

        private async Task SyncFaqChannel(SocketSlashCommand msg, ITextChannel channel, string url, bool simulate)
        {
            var sw = Stopwatch.StartNew();
            if (simulate)
                await msg.RespondAsync($"Running with simulate flag - Simulating the task, nothing will actually get changed in the target channel.", ephemeral: true);

            Uri.TryCreate(url?.Trim('<', '>', ' '), UriKind.Absolute, out var sourceUrl);
            if (sourceUrl == null)
                throw new Exception($"Invalid URL: {url}");

            if (channel is null)
                throw new Exception($"Could not find channel. If it's a thread, make sure it is not archived.");

            if (channel is SocketThreadChannel stc && stc.IsArchived)
                throw new Exception($"Thread <#{channel.Id}> is archived, aborting. Unarchive the thread and try again.");

            var myPerms = (await channel.Guild.GetCurrentUserAsync()).GetPermissions(channel);
            if (!myPerms.Has(ChannelPermission.ManageMessages))
                throw new Exception($"I don't have ManageMessages permission in the <#{channel.Id}> channel.");

            var maxMessageCount = 200;
            var oldMessages = (await channel.GetMessagesAsync(maxMessageCount).FlattenAsync()).ToList();
            if (oldMessages.Count == maxMessageCount)
            {
                throw new Exception($"Over {maxMessageCount} messages found in channel <#{channel.Id}>, something doesn't seem right. Aborting.");
            }

            foreach (var message in oldMessages)
            {
                if (message.Author.Id != Client.CurrentUser.Id)
                {
                    throw new Exception($"Channel <#{channel.Id}> contains a message that doesn't belong to me: {message.GetJumpUrl()}");
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

            await msg.RespondAsync($"Deleting {oldMessages.Count} of my old messages in channel <#{channel.Id}>", ephemeral: true);
            foreach (var message in oldMessages)
            {
                if (!simulate)
                {
                    await message.DeleteAsync();
                    await Task.Delay(500);
                }
            }

            await msg.RespondAsync($"Spawning {newMessageContents.Count} new messages in channel <#{channel.Id}>", ephemeral: true);
            foreach (var messageContent in newMessageContents)
            {
                var sanitizedMessageContent = Regex.Replace(Regex.Replace(messageContent, @"\[.+?\]\((\S+)\)", "<$1>"), @"(\r?\n *)+", "\r\n");

                if (!simulate)
                {
                    var sent = await channel.SendMessageAsync(sanitizedMessageContent);
                    if (sanitizedMessageContent.StartsWith("```####"))
                        await sent.PinAsync();
                }
            }



            if (!simulate)
            {
                // Remove the "message was pinned" notifications to clean things up
                await foreach (var pinMsg in channel.GetMessagesAsync(maxMessageCount).Flatten().Where(x => x.Type == MessageType.ChannelPinnedMessage))
                    await pinMsg.DeleteAsync();

                await channel.SendMessageAsync($"*This is a read-only copy of <{sourceUrl}> pulled at {DateTime.Now:yyy/MM/dd HH:mm:ss}. Notify a moderator if you updated the wiki and want it synced.*");
            }

            await msg.RespondAsync($"Successfully finished syncing channel <#{channel.Id}> in {sw.Elapsed:hh\\:mm\\:ss}!{(channel is SocketThreadChannel ? "\nRemember to archive the thread!" : "")}", ephemeral: true);
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
