using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace CHEF.Components.Polls;

public static class Extensions
{
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
