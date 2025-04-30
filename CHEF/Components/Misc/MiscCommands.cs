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
                                                                                .AddOption("text", ApplicationCommandOptionType.String, "What to say", true)
                                                                                .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel to speak in")
                                                                                .WithDefaultMemberPermissions(GuildPermission.ManageChannels)
                                                                                .WithContextTypes(InteractionContextType.Guild)
                                                                                .Build());

            Client.ListenToSlashCommand(cmd, Callback);
        }

        private static async Task Callback(SocketSlashCommand eventArgs, ITextChannel channel, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    if (channel == null) channel = (ITextChannel)eventArgs.Channel;

                    await eventArgs.RespondAsync($"Chika will say \"{text}\" in {channel.Mention}", ephemeral: true);
                    await channel.SendMessageAsync(text);
                }
                catch (Exception e)
                {
                    await eventArgs.RespondAsync("Failed to send message: " + e.Message, ephemeral: true);
                }
            }
            else
            {
                await eventArgs.RespondAsync("Failed to send message: Empty text", ephemeral: true);
            }
        }
    }
}
