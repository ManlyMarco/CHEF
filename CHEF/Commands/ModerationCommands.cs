using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace CHEF.Commands;

public class ModerationCommands : InteractionModuleBase<SocketInteractionContext>
{
    private const ulong KohaiRoleId = 516891239526826006;
    private const ulong ModposterRoleId = 631527993465896983;

    [SlashCommand("verify-kohai", "Give a user the kohai role.")]
    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.BanMembers), RequireUserPermission(GuildPermission.SendMessages)]
    public async Task VerifyKohai([Summary(description: "User to give the role to.")] IGuildUser user)
    {
        try
        {
            await user.AddRoleAsync(KohaiRoleId);
            await RespondAsync($":white_check_mark: Gave kohai role to {user.Mention}", ephemeral: true);
        }
        catch (Exception e)
        {
            await RespondAsync(":x: Failed to give role: " + e.Message, ephemeral: true);
        }
    }

    [SlashCommand("verify-modposter", "Give a user the modposter role.")]
    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.BanMembers), RequireUserPermission(GuildPermission.SendMessages)]
    public async Task VerifyModposter([Summary(description: "User to give the role to.")] IGuildUser user)
    {
        try
        {
            await user.AddRoleAsync(ModposterRoleId);
            await RespondAsync($":white_check_mark: Gave modposter role to {user.Mention}", ephemeral: true);
        }
        catch (Exception e)
        {
            await RespondAsync(":x: Failed to give role: " + e.Message, ephemeral: true);
        }
    }

    [SlashCommand("verify-remove", "Remove the kohai and modposter roles from a user.")]
    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.BanMembers), RequireUserPermission(GuildPermission.SendMessages)]
    public async Task Unverify([Summary(description: "User to remove the roles from.")] IGuildUser user)
    {
        try
        {
            await user.RemoveRolesAsync(new[] { KohaiRoleId, ModposterRoleId });
            await RespondAsync($":white_check_mark: Removed kohai and modposter roles from {user.Mention}", ephemeral: true);
        }
        catch (Exception e)
        {
            await RespondAsync(":x: Failed to remove roles: " + e.Message, ephemeral: true);
        }
    }
}
