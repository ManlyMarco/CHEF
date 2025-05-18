using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CHEF.Extensions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CHEF.Components.Polls;


public class PollModule : InteractionModuleBase<SocketInteractionContext>
{
    // todo staff vote
    [SlashCommand("contest-vote", "Vote for an entry in a contest.")]
    [CommandContextType(InteractionContextType.Guild)]
    public async Task Vote([Summary(description: "Name of the poll, see the event announcement")] string poll,
                           [Summary(description: "Number of the entry to vote for")] ulong entry)
    {
        if (!PollDataStorage.Polls.TryGetValue(poll, out var pollData))
        {
            await RespondAsync($":x: There is no poll named '{poll}'.\nCurrently running polls: {string.Join(", ", PollDataStorage.Polls.Values.Where(x => !x.Ended).Select(x => x.PollId))}", ephemeral: true);
            return;
        }

        if (pollData.Ended)
        {
            await RespondAsync($":x: The poll named '{poll}' has ended on {pollData.EndTime.ToTimestampString()}. You cannot vote in it anymore.", ephemeral: true);
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
        await RespondAsync($":white_check_mark: Your vote of {entry} was saved in poll named '{poll}'. Thank you for voting!", ephemeral: true);
    }


    [SlashCommand("contest-poll-start", "")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task StartContestPoll(string pollName, ulong entryCount)
    {
        if (PollDataStorage.Polls.TryGetValue(pollName, out var pollData) && !pollData.Ended)
        {
            pollData.SetEntryCount(entryCount);
            await RespondAsync($":warning: Updated the poll named '{pollName}' to allow {entryCount} entries. If you wish to restart the poll, end it first.");
            return;
        }

        PollDataStorage.Polls[pollName] = new PollData(pollName, entryCount);
        await RespondAsync($":white_check_mark: Started a poll named \n{pollName}\n with {entryCount} entries.{(pollData?.Ended == true ? " Old poll results were discarded." : "")}");
    }

    [SlashCommand("contest-poll-stats", "")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task GetContestPollStats(string pollName)
    {
        if (PollDataStorage.Polls.TryGetValue(pollName, out var pollData))
        {
            var sb = new StringBuilder();
            sb.AppendLine($":information_source: Poll named '{pollName}' with {pollData.EntryCount} entries was started at {pollData.StartTime.ToTimestampString()} and ended at {(pollData.Ended ? pollData.EndTime.ToTimestampString() : "NOT ENDED YET")}. There were {pollData.Entries.Count} votes in total.");
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
            await RespondAsync($":x: No active or ended poll named '{pollName}' was found.");
        }
    }

    [SlashCommand("contest-poll-end", "")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task EndContestPoll(string pollName)
    {
        if (PollDataStorage.Polls.TryGetValue(pollName, out var pollData))
        {
            pollData.End();
            await RespondAsync($":white_check_mark: Ended the poll named '{pollName}'. The stats will be saved until a new poll is started in that channel.");
        }
        else
        {
            await RespondAsync($":x: No active or ended poll named '{pollName}' was found.");
        }
    }

    [SlashCommand("contest-poll-delete", "")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task DeleteContestPoll(string pollName)
    {
        // See if there's a poll in the channel
        if (PollDataStorage.Polls.Remove(pollName, out _))
            await RespondAsync($":white_check_mark: Deleted the poll named '{pollName}' The stats were deleted and new messages will no longer be removed.");
        else
            await RespondAsync($":x: No active or ended poll named '{pollName}' was found.");
    }

    [SlashCommand("contest-poll-list", "")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task ListContestPolls()
    {
        var sb = new StringBuilder();

        if (PollDataStorage.Polls.Count > 0)
            foreach (var pollData in PollDataStorage.Polls.Values.OrderByDescending(x => x.StartTime))
                sb.AppendLine($"`{pollData.PollId}` > \t{(pollData.Ended ? "ENDED" : "ACTIVE")} \tStartTime:{pollData.StartTime.ToTimestampString()} \tEndTime:{(pollData.Ended ? pollData.EndTime.ToTimestampString() : "NotEndedYet")} \tEntryCount:{pollData.EntryCount} \tVoteCount:{pollData.Entries.Count}");
        else
            sb.AppendLine(":x: There are no active or ended polls.");

        await RespondAsync(sb.ToString());
    }
}


/*
public class Poll(DiscordSocketClient client) : Component(client)
{
    // slash commands for
    // - initiating vote in specific channel, with given number of entries
    // - fetching stats for the last vote in a given channel
    // - ending vote (stats remains until new vote is made in the channel)
    // - deleting vote (stats are deleted, new messages will no longer be removed)
    // - list all active polls
    private const string CmdNameStart = "contest-poll-start";
    private const string CmdNameStats = "contest-poll-stats";
    private const string CmdNameEnd = "contest-poll-end";
    private const string CmdNameDelete = "contest-poll-delete";
    private const string CmdNameList = "contest-poll-list";

    private const string OptNameChannel = "channel";
    private const string OptNameCount = "entrycount";
    private const string CmdNameVote = "contest-vote";

    public override async Task SetupAsync()
    {
        try
        {
            var startCmd = new SlashCommandBuilder().WithName(CmdNameStart).WithDescription("Start contest poll in channel. All new messages will be removed, numbers are saved as votes.")
                                                    .AddOption(OptNameChannel, ApplicationCommandOptionType.Channel, "The channel to start the poll in", true)
                                                    .AddOption(OptNameCount, ApplicationCommandOptionType.Integer, "The number of poll entries that can be voted on (1 to entrycount inclusively)", true)
                                                    .WithDefaultMemberPermissions(GuildPermission.CreateEvents | GuildPermission.ManageEvents | GuildPermission.UseApplicationCommands)
                                                    .WithContextTypes(InteractionContextType.Guild)
                                                    .Build();
            var statsCmd = new SlashCommandBuilder().WithName(CmdNameStats).WithDescription("Get stats of the last contest poll in a channel.")
                                                    .AddOption(OptNameChannel, ApplicationCommandOptionType.Channel, "The channel to get the stats of the last poll in", true)
                                                    .WithDefaultMemberPermissions(GuildPermission.CreateEvents | GuildPermission.ManageEvents | GuildPermission.UseApplicationCommands)
                                                    .WithContextTypes(InteractionContextType.Guild)
                                                    .Build();
            var endCmd = new SlashCommandBuilder().WithName(CmdNameEnd).WithDescription("End contest poll in channel. The stats will be saved until a new poll is started.")
                                                  .AddOption(OptNameChannel, ApplicationCommandOptionType.Channel, "The channel to end the poll in", true)
                                                  .WithDefaultMemberPermissions(GuildPermission.CreateEvents | GuildPermission.ManageEvents | GuildPermission.UseApplicationCommands)
                                                  .WithContextTypes(InteractionContextType.Guild)
                                                  .Build();
            var deleteCmd = new SlashCommandBuilder().WithName(CmdNameDelete).WithDescription("Delete contest poll in channel. Stats will be deleted and new messages will stop being removed.")
                                                     .AddOption(OptNameChannel, ApplicationCommandOptionType.Channel, "The channel to delete the poll in", true)
                                                     .WithDefaultMemberPermissions(GuildPermission.CreateEvents | GuildPermission.ManageEvents | GuildPermission.UseApplicationCommands)
                                                     .WithContextTypes(InteractionContextType.Guild)
                                                     .Build();
            var listCmd = new SlashCommandBuilder().WithName(CmdNameList).WithDescription("List all active and ended polls.")
                                                   .WithDefaultMemberPermissions(GuildPermission.CreateEvents | GuildPermission.ManageEvents | GuildPermission.UseApplicationCommands)
                                                   .WithContextTypes(InteractionContextType.Guild)
                                                   .Build();
            var voteCmd = new SlashCommandBuilder().WithName(CmdNameVote).WithDescription("Vote for a contest poll entry.")
                                                  .AddOption("entry", ApplicationCommandOptionType.Integer, "The entry to vote for", true)
                                                  .WithDefaultMemberPermissions(GuildPermission.ViewChannel)
                                                  .WithContextTypes(InteractionContextType.Guild)
                                                  .Build();

            await Client.CreateGlobalApplicationCommandAsync(startCmd);
            await Client.CreateGlobalApplicationCommandAsync(statsCmd);
            await Client.CreateGlobalApplicationCommandAsync(endCmd);
            await Client.CreateGlobalApplicationCommandAsync(deleteCmd);
            await Client.CreateGlobalApplicationCommandAsync(listCmd);
            await Client.CreateGlobalApplicationCommandAsync(voteCmd);
        }
        catch (Exception e)
        {
            Logger.Log("POLLS > FAILED TO SET UP POLLS - " + e);
            return;
        }

        Client.SlashCommandExecuted += ClientOnSlashCommandExecuted;
        Client.MessageReceived += Client_MessageReceived;

        await PollDataStorage.InitDataStorage();
    }

    private static async Task ClientOnSlashCommandExecuted(SocketSlashCommand cmd)
    {
        try
        {
            switch (cmd.CommandName)
            {
                case CmdNameStart:
                    await StartContestPoll(cmd);
                    PollDataStorage.TriggerDataStore();
                    break;
                case CmdNameStats:
                    await GetContestPollStats(cmd);
                    PollDataStorage.TriggerDataStore();
                    break;
                case CmdNameEnd:
                    await EndContestPoll(cmd);
                    PollDataStorage.TriggerDataStore();
                    break;
                case CmdNameDelete:
                    await DeleteContestPoll(cmd);
                    PollDataStorage.TriggerDataStore();
                    break;
                case CmdNameList:
                    await ListContestPolls(cmd);
                    break;
                case CmdNameVote:
                    if (await Vote(cmd))
                        PollDataStorage.TriggerDataStore();
                    break;
            }
        }
        catch (Exception e)
        {
            Logger.Log($"POLLS > CRASH TRYING TO EXECUTE POLL COMMAND - {e}");
            await cmd.RespondAsync($"Command crashed - {e.Message}");
        }
    }

    private static async Task StartContestPoll(SocketSlashCommand cmd)
    {
        // Get the channel and entry count from the command
        var channel = (IGuildChannel)cmd.Data.Options.First(x => x.Name == OptNameChannel).Value;
        var entryCount = (long)cmd.Data.Options.First(x => x.Name == OptNameCount).Value;

        // See if there's a poll already in the channel, if so update its entry count
        if (PollDataStorage.Polls.TryGetValue(channel.Id, out var pollData) && !pollData.Ended)
        {
            pollData.SetEntryCount((ulong)entryCount);
            await cmd.RespondAsync($"Updated the poll in <#{channel.Id}> to allow {entryCount} entries. If you wish to restart the poll, end it first.");
        }

        PollDataStorage.Polls[channel.Id] = new PollData(channel.Id, (ulong)entryCount);
        await cmd.RespondAsync(
            $"Started a poll in <#{channel.Id}> with {entryCount} entries. All messages posted in the channel will be removed. If the message is a number, it will be saved as the poster's vote.{(pollData?.Ended == true ? " Old poll results were discarded." : "")}");
    }

    private static async Task GetContestPollStats(SocketSlashCommand cmd)
    {
        // Get the channel from the command
        var channel = (IGuildChannel)cmd.Data.Options.First(x => x.Name == OptNameChannel).Value;
        // See if there's a poll in the channel
        if (PollDataStorage.Polls.TryGetValue(channel.Id, out var pollData))
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Poll in <#{channel.Id}> with {pollData.EntryCount} entries was started at {pollData.StartTime.ToTimestampString()} and ended at {(pollData.Ended ? pollData.EndTime.ToTimestampString() : "NOT ENDED YET")}. There were {pollData.Entries.Count} votes in total.");
            // Count up votes for each entry and sort them from most to least votes
            pollData.Entries.GroupBy(x => x.Vote)
                    .Select(g => new { Entry = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList()
                    .ForEach(x => sb.AppendLine($"- Entry {x.Entry} has {x.Count} votes"));

            //sb.AppendLine("Votees:");
            //foreach (var entry in pollData.Entries)
            //    sb.AppendLine($"User {entry.UserId} voted for entry {entry.Vote}");

            await cmd.RespondAsync(sb.ToString());
        }
        else
        {
            await cmd.RespondAsync($"No active or ended poll found in <#{channel.Id}>");
        }
    }

    private static async Task EndContestPoll(SocketSlashCommand cmd)
    {
        // Get the channel from the command
        var channel = (IGuildChannel)cmd.Data.Options.First(x => x.Name == OptNameChannel).Value;
        // See if there's a poll in the channel
        if (PollDataStorage.Polls.TryGetValue(channel.Id, out var pollData))
        {
            pollData.End();
            await cmd.RespondAsync($"Ended the poll in <#{channel.Id}>. The stats will be saved until a new poll is started in that channel.");
        }
        else
        {
            await cmd.RespondAsync($"No active or ended poll found in <#{channel.Id}>");
        }
    }

    private static async Task DeleteContestPoll(SocketSlashCommand cmd)
    {
        // Get the channel from the command
        var channel = (IGuildChannel)cmd.Data.Options.First(x => x.Name == OptNameChannel).Value;
        // See if there's a poll in the channel
        if (PollDataStorage.Polls.Remove(channel.Id, out _))
            await cmd.RespondAsync($"Deleted the poll in <#{channel.Id}>. The stats were deleted and new messages will no longer be removed.");
        else
            await cmd.RespondAsync($"No active or ended poll found in <#{channel.Id}>");
    }

    private static async Task ListContestPolls(SocketSlashCommand cmd)
    {
        var sb = new StringBuilder();

        if (PollDataStorage.Polls.Count > 0)
            foreach (var pollData in PollDataStorage.Polls.Values.OrderByDescending(x => x.StartTime))
                sb.AppendLine(
                    $"<#{pollData.PollId}> > \t{(pollData.Ended ? "ENDED" : "ACTIVE")} \tStartTime:{pollData.StartTime.ToTimestampString()} \tEndTime:{(pollData.Ended ? pollData.EndTime.ToTimestampString() : "NotEndedYet")} \tEntryCount:{pollData.EntryCount} \tVoteCount:{pollData.Entries.Count}");
        else
            sb.AppendLine("There are no active or ended polls.");

        await cmd.RespondAsync(sb.ToString());
    }

    private static async Task<bool> Vote(SocketSlashCommand msg)
    {
        if (!PollDataStorage.Polls.TryGetValue(msg.Channel.Id, out var pollData))
        {
            await msg.RespondAsync("There is no poll running in this channel", ephemeral: true);
        }
        else
        {
            if (pollData.Ended)
            {
                await msg.RespondAsync($"The poll in this channel has ended on {pollData.EndTime.ToTimestampString()}.", ephemeral: true);
            }
            else if (pollData.Entries.Any(x => x.UserId == msg.User.Id))
            {
                await msg.RespondAsync($"You have already voted for entry {pollData.Entries.First(x => x.UserId == msg.User.Id).Vote}. You cannot change your vote.", ephemeral: true);
            }
            else
            {
                var vote = (ulong)Convert.ChangeType(msg.Data.Options.Single().Value, typeof(ulong))!;
                if (vote > pollData.EntryCount || vote <= 0)
                {
                    await msg.RespondAsync($"Your vote of {vote} is invalid. Please try again with a number between 1 and {pollData.EntryCount}.", ephemeral: true);
                }
                else
                {
                    pollData.Entries.Add(new PollEntry(msg.User.Id, vote));
                    await msg.RespondAsync($"Your vote of {vote} was saved. Thank you for voting!", ephemeral: true);
                    return true;
                }
            }
        }

        return false;
    }

    private static async Task Client_MessageReceived(SocketMessage arg)
    {
        // Check if a poll was started in the channel, if so handle the message and delete it
        if (arg is not SocketUserMessage msg) return;
        if (msg.Author.IsBot || msg.Author.IsWebhook) return;
        if (PollDataStorage.Polls.TryGetValue(msg.Channel.Id, out var pollData))
        {
            try
            {
                await msg.ReplyInDm($"If you wish to vote in the contest, please go to <#{msg.Channel.Id}> and use the `/{CmdNameVote}` command with a number in the range of 1 to {pollData.EntryCount}.");
            }
            catch (Exception e)
            {
                Logger.Log($"POLLS > CRASH ON POLL REPLY msg:{msg} exception:{e}");
            }
            finally
            {
                await msg.DeleteAsync();
            }
        }
    }
}*/