﻿using System.Threading.Tasks;
using Discord.WebSocket;

namespace CHEF.Components.Watcher
{
    public class Watcher : Component
    {
        private readonly AutoPastebin _autoPastebin;

        public Watcher(DiscordSocketClient client) : base(client)
        {
            _autoPastebin = new AutoPastebin();
        }

        public override async Task SetupAsync()
        {
            Client.MessageReceived += MsgWatcherAsync;

            await Task.CompletedTask;
        }

        private async Task MsgWatcherAsync(SocketMessage msg)
        {
            var pasteBinRes = await _autoPastebin.Try(msg);

            if (pasteBinRes.Length > 1)
                await msg.Channel.SendMessageAsync(pasteBinRes);

            if (msg.Content.ToLower().Contains("can i ask"))
            {
                await msg.Channel.SendMessageAsync($"{msg.Author.Mention} https://dontasktoask.com/");
            }
        }
    }
}