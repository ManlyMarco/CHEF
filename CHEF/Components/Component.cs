using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace CHEF.Components
{
    /// <summary>
    /// A Component is a class that you want to call at start
    /// for either initializing stuff or registering events to the DiscordSocketClient object.
    /// </summary>
    public abstract class Component : IDisposable
    {
        protected readonly DiscordSocketClient Client;

        protected Component(DiscordSocketClient client)
        {
            Client = client;
        }

        /// <summary>
        /// This method should have the async keyword when being implemented
        /// </summary>
        /// <returns></returns>
        public abstract Task SetupAsync();

        internal void WatchForCommandInControlChannel(string command, Func<SocketUserMessage, Task> callback, bool awaitCallback)
        {
            Client.MessageReceived += msg =>
            {
                try
                {
                    return WatchForCommandInControlChannel(msg, command, callback, awaitCallback);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to handle message {msg.GetJumpUrl()}\n{e}");
                    return Task.CompletedTask;
                }
            };
        }

        private static async Task WatchForCommandInControlChannel(SocketMessage smsg, string command, Func<SocketUserMessage, Task> callback, bool awaitCallback)
        {
            var msg = smsg as SocketUserMessage;
            var channel = msg?.Channel as SocketTextChannel;
            if (channel == null) return;
            if (msg.Author.IsBot || msg.Author.IsWebhook) return;

            var parts = msg.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0] == command)
            {
                if (channel.Guild.Id == 560859356439117844UL && channel.Id == 730477794865184789UL ||
                    channel.Guild.Id == 447114928785063977UL && channel.Id == 448958364764864522UL)
                {
                    if (awaitCallback)
                        await callback(msg);
                    else
                        _ = callback(msg);
                }
                else
                {
                    await msg.ReplyAsync("https://tenor.com/view/no-no-no-way-never-nuh-uh-gif-14500720");
                }
            }
        }

        public virtual void Dispose()
        {
            IsDisposed = true;
        }

        public bool IsDisposed { get; set; }
    }
}
