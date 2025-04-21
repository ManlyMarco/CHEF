using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace CHEF.Components.Polls;

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

    public override async Task SetupAsync()
    {
        try
        {
            //447114928785063977 kk
            //560859356439117844 test
            //var guild = Client.GetGuild(560859356439117844ul);
            //await AddCommands(guild);

            foreach (var guild in Client.Guilds)
                await AddCommands(guild);
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

    private static async Task AddCommands(SocketGuild guild)
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

        var sb = new StringBuilder();
        sb.AppendLine($"POLL > Adding commands to guild: {guild.Name}");
        foreach (var command in new[] { startCmd, statsCmd, endCmd, deleteCmd, listCmd })
        {
            try
            {
                var cmd = await guild.CreateApplicationCommandAsync(command);
                sb.AppendLine($"Created command {cmd.Name}");
            }
            catch (Exception e)
            {
                sb.AppendLine($"Failed to create command {command.Name}, aborting\n{e}");
                break;
            }
        }
        Logger.Log(sb.ToString());
    }

    private static async Task ClientOnSlashCommandExecuted(SocketSlashCommand cmd)
    {
        try
        {
            switch (cmd.CommandName)
            {
                case CmdNameStart:
                    await StartContestPoll(cmd);
                    break;
                case CmdNameStats:
                    await GetContestPollStats(cmd);
                    break;
                case CmdNameEnd:
                    await EndContestPoll(cmd);
                    break;
                case CmdNameDelete:
                    await DeleteContestPoll(cmd);
                    break;
                case CmdNameList:
                    await ListContestPolls(cmd);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cmd), $"Unknown command {cmd.CommandName}");
            }

            PollDataStorage.TriggerDataStore();
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
                    $"<#{pollData.ChannelId}> > \t{(pollData.Ended ? "ENDED" : "ACTIVE")} \tStartTime:{pollData.StartTime.ToTimestampString()} \tEndTime:{(pollData.Ended ? pollData.EndTime.ToTimestampString() : "NotEndedYet")} \tEntryCount:{pollData.EntryCount} \tVoteCount:{pollData.Entries.Count}");
        else
            sb.AppendLine("There are no active or ended polls.");

        await cmd.RespondAsync(sb.ToString());
    }

    private static async Task Client_MessageReceived(SocketMessage arg)
    {
        try
        {
            // Check if a poll was started in the channel, if so handle the message and delete it
            if (arg is not SocketUserMessage msg) return;
            if (msg.Author.IsBot || msg.Author.IsWebhook) return;
            if (PollDataStorage.Polls.TryGetValue(msg.Channel.Id, out var pollData))
                try
                {
                    if (pollData.Ended)
                    {
                        await msg.ReplyInDm($"<#{msg.Channel.Id}> > The poll in this channel has ended on {pollData.EndTime.ToTimestampString()}.");
                    }
                    else if (pollData.Entries.Any(x => x.UserId == msg.Author.Id))
                    {
                        await msg.ReplyInDm($"<#{msg.Channel.Id}> > You have already voted for entry {pollData.Entries.First(x => x.UserId == msg.Author.Id).Vote}. You cannot change your vote.");
                    }
                    else if (ulong.TryParse(msg.Content, NumberStyles.None, CultureInfo.InvariantCulture, out var vote))
                    {
                        if (vote > pollData.EntryCount || vote <= 0)
                        {
                            await msg.ReplyInDm($"<#{msg.Channel.Id}> > Your vote of {vote} is invalid. Please try again with a number between 1 and {pollData.EntryCount}.");
                        }
                        else
                        {
                            pollData.Entries.Add(new PollEntry(msg.Author.Id, vote));
                            PollDataStorage.TriggerDataStore();
                            await msg.ReplyInDm($"<#{msg.Channel.Id}> > Your vote of {vote} was saved. Thank you for voting!");
                        }
                    }
                    else
                    {
                        await msg.ReplyInDm($"<#{msg.Channel.Id}> > Your message was not a number, please send a number in the range from 1 to {pollData.EntryCount}.");
                    }
                }
                catch (Exception e)
                {
                    Logger.Log($"POLLS > CRASH ON POLL REPLY msg:{msg} exception:{e}");
                    await msg.ReplyInDm($"<#{msg.Channel.Id}> > Your vote could not be registered because of an error. Contact an administrator if this issue persists.");
                }
                finally
                {
                    await msg.DeleteAsync();
                }
        }
        catch (Exception e)
        {
            Logger.Log($"POLLS > CRASH IN POLL REPLY HANDLER arg:{arg} exception:{e}");
        }
    }
}