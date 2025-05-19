using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CHEF.Extensions;
using Discord;
using Discord.Interactions;

namespace CHEF.Components.Polls;

[Group("contest-edit", "Start/end/list active polls.")]
[CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.CreateEvents), RequireUserPermission(GuildPermission.CreateEvents)]
public class ContestEditCommand : InteractionModuleBase<SocketInteractionContext>
{
    static ContestEditCommand()
    {
        PollDataStorage.InitDataStorage().Wait();
    }

    [SlashCommand("start", "Start a new poll. If a poll already exists, its entry count is updated but results stay.")]
    public async Task StartContestPoll([Summary(description: "Name of the poll"), Autocomplete(typeof(PollNameAutocompleteHandler))] string pollName, long entryCount, bool staffOnly)
    {
        if (entryCount < 2)
        {
            await RespondAsync($":x: entryCount has to be 2 or higher.");
            return;
        }

        if (PollDataStorage.Polls.TryGetValue(pollName, out var pollData) && !pollData.Ended)
        {
            pollData.SetEntryCount(entryCount);
            PollDataStorage.TriggerDataStore();
            await RespondAsync($":warning: Updated the poll named `{pollName}` to allow {entryCount} entries. If you wish to restart the poll, end it first.");
            return;
        }

        PollDataStorage.Polls[pollName] = new PollData(pollName, entryCount, staffOnly);
        PollDataStorage.TriggerDataStore();
        await RespondAsync($":white_check_mark: Started a poll named `{pollName}` with {entryCount} entries.");
    }

    [SlashCommand("stats", "Get results of a single poll.")]
    public async Task GetContestPollStats([Summary(description: "Name of the poll"), Autocomplete(typeof(PollNameAutocompleteHandler))] string pollName)
    {
        if (PollDataStorage.Polls.TryGetValue(pollName, out var pollData))
        {
            var sb = new StringBuilder();
            sb.AppendLine($":information_source: Poll named `{pollName}` with {pollData.EntryCount} entries was started at {pollData.StartTime.ToTimestampString()} and ended at {(pollData.Ended ? pollData.EndTime.ToTimestampString() : "NOT ENDED YET")}. There were {pollData.Entries.Count} votes in total.");
            // Count up votes for each entry and sort them from most to least votes
            pollData.Entries.GroupBy(x => x.Vote)
                    .Select(g => new { Entry = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList()
                    .ForEach(x => sb.AppendLine($"- Entry {x.Entry} has {x.Count} votes"));

            //sb.AppendLine("Votees:");
            //foreach (var entry in pollData.Entries)
            //    sb.AppendLine($"User {entry.UserId} voted for entry {entry.Vote}");

            await RespondAsync(sb.ToString());
        }
        else
        {
            await RespondAsync($":x: No active or ended poll named `{pollName}` was found.");
        }
    }

    [SlashCommand("end", "End a poll. Prevents voting while keeping results.")]
    public async Task EndContestPoll([Summary(description: "Name of the poll"), Autocomplete(typeof(PollNameAutocompleteHandler))] string pollName)
    {
        if (PollDataStorage.Polls.TryGetValue(pollName, out var pollData))
        {
            pollData.End();
            PollDataStorage.TriggerDataStore();
            await RespondAsync($":white_check_mark: Ended the poll named `{pollName}`. The stats will be saved until a new poll is started in that channel.");
        }
        else
        {
            await RespondAsync($":x: No active or ended poll named `{pollName}` was found.");
        }
    }

    [SlashCommand("delete", "Delete a poll, including its results.")]
    public async Task DeleteContestPoll([Summary(description: "Name of the poll"), Autocomplete(typeof(PollNameAutocompleteHandler))] string pollName)
    {
        // See if there`s a poll in the channel
        if (PollDataStorage.Polls.Remove(pollName, out _))
            await RespondAsync($":white_check_mark: Deleted the poll named `{pollName}` The stats were deleted and new messages will no longer be removed.");
        else
            await RespondAsync($":x: No active or ended poll named `{pollName}` was found.");
        PollDataStorage.TriggerDataStore();
    }

    [SlashCommand("list", "Get a list of all polls, open and closed.")]
    public async Task ListContestPolls()
    {
        var sb = new StringBuilder();

        if (PollDataStorage.Polls.Count > 0)
        {
            var all = PollDataStorage.Polls.Values.OrderByDescending(x => x.StartTime).ToArray();
            var contents = new string[7, all.Length + 1];
            contents[0, 0] = "PollName";
            contents[1, 0] = "Status";
            contents[2, 0] = "StartTime";
            contents[3, 0] = "EndTime";
            contents[4, 0] = "EntryCount";
            contents[5, 0] = "VoteCount";
            contents[6, 0] = "Staffonly";
            for (int i = 0; i < all.Length; i++)
            {
                var pollData = all[i];
                contents[0, i + 1] = pollData.PollId;
                contents[1, i + 1] = pollData.Ended ? "ENDED" : "ACTIVE";
                contents[2, i + 1] = TimeAgo(pollData.StartTime);
                contents[3, i + 1] = pollData.Ended ? TimeAgo(pollData.EndTime) : "NotEndedYet";
                contents[4, i + 1] = pollData.EntryCount.ToString();
                contents[5, i + 1] = pollData.Entries.Count.ToString();
                contents[6, i + 1] = pollData.StaffOnly.ToString();
            }
            await RespondAsync(TableCreator.CreateMarkdownTable(contents, true));
        }
        else
            await RespondAsync(":x: There are no active or ended polls.");

    }
    public static string TimeAgo(DateTimeOffset dateTimeOffset)
    {
        TimeSpan timeSpan = DateTimeOffset.Now - dateTimeOffset;
        if (timeSpan.TotalDays > 7)
            return $"on {dateTimeOffset.ToUniversalTime():yyyy-MM-dd}";
        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.Days} days ago";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours} hours ago";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes} minutes ago";
        return "just now";
    }
}

public static class TableCreator
{
    public static string CreateMarkdownTable(string[,] contents, bool firstIsHeader)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```");

        // ╔═══╤═══╤═══╗
        // ║ 1 │ 2 │ 3 ║
        // ╠═══╪═══╪═══╣
        // ║ 4 │ 5 │ 6 ║
        // ╟───┼───┼───╢
        // ║ 7 │ 8 │ 9 ║
        // ╚═══╧═══╧═══╝

        var colWidths = new int[contents.GetLength(0)];
        for (var column = 0; column < contents.GetLength(0); column++)
        {
            var maxLen = 0;
            for (int row = 0; row < contents.GetLength(1); row++)
                maxLen = Math.Max(maxLen, contents[column, row].Length);

            colWidths[column] = maxLen;
        }

        DrawSeparator('╔', '═', '╤', '╗');

        for (int row = 0; row < contents.GetLength(1); row++)
        {
            for (int column = 0; column < contents.GetLength(0); column++)
            {
                sb.Append(column == 0 ? '║' : '│');
                sb.Append(' ');
                sb.Append(contents[column, row].PadLeft(colWidths[column]));
                sb.Append(' ');
                if (column + 1 == contents.GetLength(0))
                    sb.Append('║');
            }
            sb.AppendLine();

            if (firstIsHeader && row == 0)
                DrawSeparator('╠', '═', '╪', '╣');
            else if (row + 1 == contents.GetLength(1))
                DrawSeparator('╚', '═', '╧', '╝');
            else
                DrawSeparator('╟', '─', '┼', '╢');
        }

        sb.AppendLine("```");
        return sb.ToString();

        void DrawSeparator(char left, char mid, char t, char right)
        {
            sb.Append(left);
            for (var column = 0; column < contents.GetLength(0); column++)
            {
                sb.Append(mid, colWidths[column] + 2);
                var last = column + 1 == contents.GetLength(0);
                sb.Append(last ? right : t);
            }

            sb.AppendLine();
        }
    }
}