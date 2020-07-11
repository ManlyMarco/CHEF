using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Discord;
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
        }

        internal static void Log(string msg)
        {
            var log = $"{LogPrefix} {msg}";

            Console.WriteLine(log);

            if (_reportTo == null || msg.Contains(" Gateway ", StringComparison.Ordinal)) return;

            try
            {
                if (log.Contains("Exception", StringComparison.OrdinalIgnoreCase)) log = $"```as\n{log}```";
                Task.Run(async () => { await _reportTo.SendMessageAsync(log); });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send log to logging channel - " + ex);
            }
        }
    }
}
