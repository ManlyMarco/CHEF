using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace CHEF.Components.Autoban;

public class AutobanCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("autoban-set-ban-channel", "Enable or disable autoban for a channel.")]
    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.BanMembers), RequireUserPermission(GuildPermission.BanMembers)]
    public async Task SetAutobanChannel([Summary(description: "Channel to autoban in.")] ITextChannel channel,
                                        [Summary(description: "Enable autoban.")] bool enable)
    {
        AutobanSettingsManager.SetAutobanChannel(Context.Guild.Id, channel.Id, enable);
        await RespondAsync($":white_check_mark: Autoban has been {(enable ? "enabled" : "disabled")} for {channel.Mention}", ephemeral: true);
    }

    [SlashCommand("autoban-set-alert-channel", "Set the channel for autoban alerts.")]
    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.BanMembers), RequireUserPermission(GuildPermission.BanMembers)]
    public async Task SetAutobanAlertChannel([Summary(description: "Channel for ban alerts.")] ITextChannel channel)
    {
        AutobanSettingsManager.SetAlertChannel(Context.Guild.Id, channel.Id);
        await RespondAsync($":white_check_mark: Autoban alert channel set to {channel.Mention}", ephemeral: true);
    }

    [SlashCommand("autoban-status", "Check status of the autoban feature.")]
    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.BanMembers), RequireUserPermission(GuildPermission.BanMembers)]
    public async Task GetAutobanStatus()
    {
        await RespondAsync($":white_check_mark: Autoban is enabled in the following channels: {string.Join(", ", AutobanSettingsManager.GetAutobanChannels(Context.Guild.Id).Select(MentionUtils.MentionChannel))}\n" +
                           $"Autoban alerts are sent to: {MentionUtils.MentionChannel(AutobanSettingsManager.GetAlertChannel(Context.Guild.Id) ?? 0)}", ephemeral: true);
    }
}
