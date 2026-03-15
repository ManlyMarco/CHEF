using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace CHEF.Components.Autoban;

public class Autoban : Component
{
    public Autoban(DiscordSocketClient client) : base(client) { }
    public override Task SetupAsync()
    {
        Client.MessageReceived += async msg =>
        {
            try
            {
                if (msg is SocketUserMessage userMsg &&
                    msg.Channel is SocketTextChannel textChannel &&
                    !msg.Author.IsBot && !msg.Author.IsWebhook)
                {
                    var guildId = textChannel.Guild.Id;
                    var channelId = textChannel.Id;
                    if (AutobanSettingsManager.IsAutobanEnabled(guildId, channelId))
                    {
                        var guildUser = textChannel.Guild.GetUser(msg.Author.Id);
                        if (guildUser != null && !guildUser.GuildPermissions.ModerateMembers) // ModerateMembers - Timeout permission
                        {
                            await guildUser.BanAsync(reason: $"Autoban: Posted in #{textChannel.Name}", pruneDays: 1);

                            var alertChannelId = AutobanSettingsManager.GetAlertChannel(guildId);
                            if (alertChannelId.HasValue)
                            {
                                var alertChannel = textChannel.Guild.GetTextChannel(alertChannelId.Value);
                                if (alertChannel != null)
                                {
                                    var embed = new EmbedBuilder();
                                    embed.Description = $":rotating_light: User {guildUser.GlobalName} ({guildUser.Id}) was autobanned for posting in {textChannel.Mention}.";
                                    embed.AddField("Message content:", $"```\n{userMsg.Content}\n```");
                                    if (userMsg.Attachments.Any())
                                        embed.AddField("Attachments:", string.Join(", ", userMsg.Attachments.Select(x => $"[{x.Filename}]({x.Url})")));
                                    embed.Color = new Color(242, 54, 54);
                                    
                                    await alertChannel.SendMessageAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Autoban: Failed to handle message {msg.GetJumpUrl()}\n{e}");
            }
        };

        return Task.CompletedTask;
    }
}
