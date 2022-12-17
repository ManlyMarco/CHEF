using System;
using System.Linq;
using System.Threading.Tasks;
using CHEF.Components;
using Discord;
using Discord.WebSocket;

namespace CHEF
{
    public class CHEF
    {
        private DiscordSocketClient _client;

        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Error: No bot token was passed in arguments");
                return;
            }

            var token = args.Last();
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Error: No bot token was passed in arguments");
                return;
            }

            new CHEF().MainAsync(token).GetAwaiter().GetResult();
        }

        private async Task MainAsync(string token)
        {
            var config = new DiscordSocketConfig
            {
                MessageCacheSize = 100, 
                GatewayIntents = GatewayIntents.DirectMessageReactions | GatewayIntents.DirectMessageTyping | GatewayIntents.DirectMessages | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageTyping | GatewayIntents.GuildMessageReactions | GatewayIntents.Guilds | GatewayIntents.MessageContent,
                LogLevel = LogSeverity.Info
            };
            _client = new DiscordSocketClient(config);
            _client.Log += Log;

            _client.Ready += InitOnClientReady;
            _client.Ready += UniqueInitOnClientReady;

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task InitOnClientReady()
        {
            Logger.Init(_client);
            await Task.CompletedTask;
        }

        private async Task UniqueInitOnClientReady()
        {
            Database.Init();
            await ComponentHandler.Init(_client);
            _client.Ready -= UniqueInitOnClientReady;
        }

        private static Task Log(LogMessage msg)
        {
            Logger.Log(msg.ToString());
            return Task.CompletedTask;
        }
    }
}