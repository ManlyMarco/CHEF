using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace CHEF
{
    internal static class Logger
    {
        private const string LogPrefix = "[Chikarin]";

        private static DiscordSocketClient _client;
        private static SocketTextChannel _reportTo;

        internal static void Init(DiscordSocketClient client)
        {
            const long serverId = 560859356439117844;
            const long channelId = 730477794865184789;

            _client = client;
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
                            if(option.Options.Count > 0)
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
