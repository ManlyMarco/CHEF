using System;
using System.Linq;
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

    [SlashCommand("sync-category-permissions", "Synchronize permissions of all channels within a category to the category permissions.")]
    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.ManageChannels), RequireUserPermission(GuildPermission.ManageChannels)]
    public async Task SyncCategoryPermissions(
        [Summary(description: "Category that contains the channels to synchronize permissions of.")] ICategoryChannel category,
        [Summary(description: "If true, only logs what would be done without actually changing anything.")] bool whatIf = false)
    {

        try
        {
            if (category == null)
            {
                await RespondAsync(":x: Failed to synchronize permissions: Category not found", ephemeral: true);
                return;
            }

            await DeferAsync(true);

            var channels = await category.Guild.GetChannelsAsync();
            var categoryChannels = channels.OfType<ITextChannel>().Where(c => c.CategoryId == category.Id).ToList();

            var response = whatIf 
                ? $":information_source: Would sync permissions for channels:\n" 
                : $":white_check_mark: Synchronized permissions for {categoryChannels.Count} channels in category {category.Name}";
            foreach (var channel in categoryChannels)
            {
                if (whatIf)
                    response += $"- {channel.Name}\n";
                else
                    await channel.SyncPermissionsAsync();
            }

            await FollowupAsync(response, ephemeral: true);
        }
        catch (Exception e)
        {
            await FollowupAsync(":x: Failed to synchronize permissions: " + e.Message, ephemeral: true);
        }
    }
}