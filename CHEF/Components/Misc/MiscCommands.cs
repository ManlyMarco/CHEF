using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using CHEF.Extensions;
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

            Client.ListenToSlashCommand<ITextChannel, string>(cmd, Callback);

        }

        private static async Task Callback(SocketSlashCommand arg, ITextChannel channel, string text)
        {
            if (channel != null && !string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    await arg.RespondAsync($"Chika will say \"{text}\" in {channel.Mention}", ephemeral: true);
                    await channel.SendMessageAsync(text);
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
