using Discord;
using Discord.WebSocket;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CHEF
{
    internal static class Logger
    {
        private const string LogPrefix = "[Chikarin]";

        private static DiscordSocketClient _client;
        private static SocketTextChannel _reportTo;

        private static readonly string LogChannelsPath = Path.GetFullPath("LogChannels.json");
        private static readonly ConcurrentDictionary<ulong, ulong> _guildLogChannels = new();

        internal static void SetLogChannel(ulong guildId, ulong channelId)
        {
            _guildLogChannels[guildId] = channelId;
            File.WriteAllText(LogChannelsPath, JsonSerializer.Serialize(_guildLogChannels.ToDictionary(x => x.Key, x => x.Value)));
        }

        internal static void Init(DiscordSocketClient client)
        {
            const long serverId = 560859356439117844;
            const long channelId = 730477794865184789;

            _client = client;

            if (File.Exists(LogChannelsPath))
            {
                try
                {
                    var saved = JsonSerializer.Deserialize<Dictionary<ulong, ulong>>(File.ReadAllText(LogChannelsPath));
                    if (saved != null)
                        foreach (var pair in saved)
                            _guildLogChannels[pair.Key] = pair.Value;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to read log channels file - " + e);
                }
            }
            try
            {
                var guild = client.GetGuild(serverId) ?? throw new InvalidDataException("Could not get guild from id");
                _reportTo = guild.GetTextChannel(channelId) ?? throw new InvalidDataException("Could not get channel from id");
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to connect to remote log channel - " + e);
            }

            // log who called which command
            client.SlashCommandExecuted += arg =>
            {
                try
                {
                    var user = arg.User.GlobalName;
                    var sb = new StringBuilder().Append(user).Append(" called command `").Append(arg.CommandName);

                    PrintOptions(arg.Data.Options);
                    void PrintOptions(IReadOnlyCollection<SocketSlashCommandDataOption> socketSlashCommandDataOptions)
                    {
                        foreach (var option in socketSlashCommandDataOptions)
                        {
                            sb.Append($" {option.Name}=[{option.Value}]");
                            if (option.Options.Count > 0)
                                PrintOptions(option.Options);
                        }
                    }

                    sb.Append('`');

                    Log(sb.ToString());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                try
                {
                    if (arg.GuildId is ulong guildId && _guildLogChannels.TryGetValue(guildId, out var logChannelId) &&
                        _client.GetChannel(logChannelId) is IMessageChannel logChannel)
                    {
                        var embed = new EmbedBuilder()
                            .WithAuthor(arg.User.GlobalName, arg.User.GetAvatarUrl())
                            .WithDescription($"**/{arg.CommandName}** was used by {arg.User.Mention} in <#{arg.ChannelId}>");

                        // Do not log the options for the contest-vote command, as it contains sensitive information
                        if (arg.CommandName != "contest-vote")
                        {
                            AddOptionFields(arg.Data.Options);

                            void AddOptionFields(
                                IReadOnlyCollection<SocketSlashCommandDataOption> socketSlashCommandDataOptions)
                            {
                                foreach (var option in socketSlashCommandDataOptions)
                                {
                                    if (option.Value != null)
                                        embed.AddField(option.Name,
                                            option.Value switch
                                            {
                                                IUser u => u.Mention, IGuildChannel c => "<#" + c.Id + ">",
                                                _ => option.Value.ToString()
                                            }, true);
                                    if (option.Options.Count > 0)
                                        AddOptionFields(option.Options);
                                }
                            }
                        }

                        Task.Run(async () => { await logChannel.SendMessageAsync(embed: embed.Build()); });
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                return Task.CompletedTask;
            };
        }

        internal static void Log(string msg)
        {
            var log = $"{LogPrefix} {msg}";

            Console.WriteLine(log);

            if (_reportTo == null || msg.Contains(" Gateway ", StringComparison.Ordinal)) return;

            try
            {
                if (log.Contains("Exception", StringComparison.OrdinalIgnoreCase)) log = $"```as\n{log}```";
                Task.Run(async () => { await _reportTo.SendMessageAsync(log.Length > 1500 ? log.Substring(0, 1500) : log); });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send log to logging channel - " + ex);
            }
        }
    }
}
