using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace CHEF.Commands;

public class MiscCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("chika-say", "Become a skinwalker and make chika say something.")]
    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.ManageChannels), RequireUserPermission(GuildPermission.ViewChannel)]
    public async Task ChikaSay([Summary(description: "What to say.")] string text,
                               [Summary(description: "Channel to speak in. Speaks in current channel by default.")] ITextChannel channel = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await RespondAsync(":x: Failed to send message: Empty text", ephemeral: true);
                return;
            }

            if (channel == null) channel = (ITextChannel)Context.Channel;

            await RespondAsync($":white_check_mark: Chika will say \"{text}\" in {channel.Mention}", ephemeral: true);

            await channel.SendMessageAsync(text);
        }
        catch (Exception e)
        {
            await RespondAsync(":x: Failed to send message: " + e.Message, ephemeral: true);
        }
    }
}