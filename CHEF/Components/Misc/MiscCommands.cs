using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CHEF.Components.Polls;
using Discord;

namespace CHEF.Components.Misc
{
    public class MiscCommands(DiscordSocketClient client) : Component(client)
    {
        public override async Task SetupAsync()
        {
            var cmd = await Client.CreateGlobalApplicationCommandAsync(new SlashCommandBuilder().WithName("chika-say")
                                                                                .WithDescription("Become a skinwalker and make chika say something")
                                                                                .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel to speak in")
                                                                                .AddOption("text", ApplicationCommandOptionType.String, "What to say")
                                                                                .WithDefaultMemberPermissions(GuildPermission.Administrator)
                                                                                .WithContextTypes(InteractionContextType.Guild)
                                                                                .Build());

            Client.ListenToSlashCommand<ITextChannel ,string>(cmd, Callback);
        }

        private async Task Callback(SocketSlashCommand arg, ITextChannel channel, string arg3)
        {
        }

        private async Task ClientOnSlashCommandExecuted(SocketSlashCommand arg)
        {
            // log who called which command
            var user = arg.User.GlobalName;
            Logger.Log($"{user} called command {arg.CommandName} with args {string.Join(' ', arg.Data.Options.Select(x => $"{x.Name}=[{x.Value}]"))}");

            if (arg.CommandName == "chika-say")
            {
                var commandData = arg.Data;
                var options = commandData.Options;
                var channel = options.FirstOrDefault(x => x.Name == "channel")?.Value as SocketTextChannel;
                var text = options.FirstOrDefault(x => x.Name == "text")?.Value?.ToString();
                if (channel != null && !string.IsNullOrWhiteSpace(text))
                {
                    try
                    {
                        _ = channel.SendMessageAsync(text);
                        await arg.RespondAsync($"Chika will say \"{text}\" in {channel.Mention}", ephemeral: true);
                    }
                    catch (Exception e)
                    {
                        await arg.RespondAsync("Failed to send message: " + e.Message, ephemeral: true);
                    }
                }
                else
                {
                    await arg.RespondAsync("Invalid channel or empty text", ephemeral: true);
                }
            }
        }
    }
}
