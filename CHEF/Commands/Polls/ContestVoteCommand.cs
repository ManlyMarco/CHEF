using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CHEF.Extensions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CHEF.Commands.Polls;

public class ContestVoteCommand : InteractionModuleBase<SocketInteractionContext>
{
    public static bool UserIsStaff(IInteractionContext context) => context.User is SocketGuildUser user && user.GuildPermissions.ManageRoles;
    public static IEnumerable<string> GetActivePollList(bool canSeeStaffOnly) => PollDataStorage.Polls.Values.Where(x => !x.Ended && (canSeeStaffOnly || !x.StaffOnly)).Select(x => x.PollId);

    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [SlashCommand("contest-vote", "Vote for an entry in a contest.")]
    public async Task Vote([Summary(description: "Name of the poll"), Autocomplete(typeof(PollNameAutocompleteHandler))] string pollName,
                           [Summary(description: "Number of the entry to vote for")] long entry)
    {
        var isStaff = UserIsStaff(Context);

        if (!PollDataStorage.Polls.TryGetValue(pollName, out var pollData))
        {
            await RespondAsync($":x: There is no poll named `{pollName}`.\nCurrently running polls: {string.Join(", ", GetActivePollList(isStaff))}", ephemeral: true);
            return;
        }

        if (pollData.Ended)
        {
            await RespondAsync($":x: The poll named `{pollName}` has ended on {pollData.EndTime.ToTimestampString()}. You cannot vote in it anymore.", ephemeral: true);
            return;
        }

        if (!isStaff && pollData.StaffOnly)
        {
            await RespondAsync($":x: The poll named `{pollName}` is limited to staff only. You cannot vote in it.", ephemeral: true);
            return;
        }

        var userId = Context.User.Id;
        var existingEntry = pollData.Entries.FirstOrDefault(x => x.UserId == userId);
        if (existingEntry != null)
        {
            await RespondAsync($":x: You have already voted for entry {existingEntry.Vote}. You cannot change your vote.", ephemeral: true);
            return;
        }

        if (entry > pollData.EntryCount || entry <= 0)
        {
            await RespondAsync($":x: Your vote of {entry} is invalid. Please try again with a number between 1 and {pollData.EntryCount}.", ephemeral: true);
            return;
        }

        pollData.Entries.Add(new PollEntry(userId, entry));
        PollDataStorage.TriggerDataStore();
        await RespondAsync($":white_check_mark: Your vote of {entry} was saved in poll named `{pollName}`. Thank you for voting!", ephemeral: true);
    }
}