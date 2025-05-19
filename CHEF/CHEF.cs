using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CHEF.Components;
using Discord;
using Discord.Interactions;
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
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
                LogLevel = LogSeverity.Info
            };
            _client = new DiscordSocketClient(config);
            _client.Log += Log;
            
            _client.Ready += InitOnClientReady;
            _client.Ready += UniqueInitOnClientReady;

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await _client.SetCustomStatusAsync("DM me your log file for a private reply!");

            await Task.Delay(-1);
        }

        private async Task InitOnClientReady()
        {
            Logger.Init(_client);

            //await DeleteAllCommands();

            var interactionService = new InteractionService(_client);
            interactionService.Log += Log;
            await interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), null);
            await interactionService.RegisterCommandsGloballyAsync();
            _client.InteractionCreated += async interaction =>
            {
                var ctx = new SocketInteractionContext(_client, interaction);
                var result = await interactionService.ExecuteCommandAsync(ctx, null);
                if(!result.IsSuccess)
                    await ctx.Interaction.RespondAsync($":x: Error: {result.Error} - {result.ErrorReason}", ephemeral: true);
            };
        }

        private async Task DeleteAllCommands()
        {
            IEnumerable<SocketApplicationCommand> cmds = await _client.GetGlobalApplicationCommandsAsync();
            foreach (var guild in _client.Guilds)
                cmds = cmds.Concat(await guild.GetApplicationCommandsAsync());

            foreach (var cmd in cmds)
                await cmd.DeleteAsync();
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