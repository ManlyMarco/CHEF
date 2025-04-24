using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CHEF.Extensions
{
    public static class Extensions
    {
        public static string TrimmedValue(this Capture c)
        {
            return c.Value.Trim();
        }

        public static IEnumerable<Y> Attempt<T, Y>(this IEnumerable<T> source, Func<T, Y> action)
        {
            foreach (var c in source)
            {
                Y result;
                try
                {
                    result = action(c);
                }
                catch (Exception e)
                {
                    Logger.Log(e.ToString());
                    continue;
                }
                yield return result;
            }
        }

        /// <summary>
        /// Returns true if the task finished in time, false if the task timed out.
        /// </summary>
        public static Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ContinueWith(resultTask =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (resultTask != task) throw new TimeoutException("Timeout while executing task");
                return task.Result;
            }, cancellationToken);
        }

        public static async Task ReplyInDm(this SocketUserMessage msg, string str)
        {
            var dm = await msg.Author.CreateDMChannelAsync();
            await dm.SendMessageAsync(str);
        }

        public static string ToTimestampString(this DateTimeOffset dt)
        {
            var t = dt.ToUnixTimeSeconds();
            return $"<t:{t}>";
        }

        public static void ListenToSlashCommand<T1>(this DiscordSocketClient client, SocketApplicationCommand command, Func<SocketSlashCommand, T1, Task> callback) =>
            ListenToSlashCommand(client, command, 1, callback == null ? null : async (slashCommand, options) => await callback(slashCommand, (T1)options[0].Value));
        public static void ListenToSlashCommand<T1, T2>(this DiscordSocketClient client, SocketApplicationCommand command, Func<SocketSlashCommand, T1, T2, Task> callback) =>
            ListenToSlashCommand(client, command, 2, callback == null ? null : async (slashCommand, options) => await callback(slashCommand, (T1)options[0].Value, (T2)options[1].Value));
        public static void ListenToSlashCommand<T1, T2, T3>(this DiscordSocketClient client, SocketApplicationCommand command, Func<SocketSlashCommand, T1, T2, T3, Task> callback) =>
            ListenToSlashCommand(client, command, 3, callback == null ? null : async (slashCommand, options) => await callback(slashCommand, (T1)options[0].Value, (T2)options[1].Value, (T3)options[2].Value));
        private static void ListenToSlashCommand(DiscordSocketClient client, SocketApplicationCommand command, int optionCount, Func<SocketSlashCommand, SocketSlashCommandDataOption[], Task> callback)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.Options.Count != optionCount)
                throw new ArgumentException($"Command must have exactly {optionCount} options");

            client.SlashCommandExecuted += ClientOnSlashCommandExecuted;

            async Task ClientOnSlashCommandExecuted(SocketSlashCommand arg)
            {
                if (arg.CommandName == command.Name)
                {
                    try
                    {
                        var options = arg.Data.Options.ToArray();
                        if (options.Length != optionCount)
                            throw new ArgumentException($"Command had {options.Length} options but must have exactly {optionCount} options");
                        await callback(arg, options);
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"FAILED TO RUN COMMAND {arg.CommandName} in <#{arg.ChannelId}> - {e}");
                        await arg.RespondAsync($"Failed to run command {arg.CommandName} - {e.Message}", ephemeral: true);
                    }
                }
            }
        }
    }
}