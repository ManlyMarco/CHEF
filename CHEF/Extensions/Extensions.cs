using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;

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

        public static void ListenToSlashCommand(this DiscordSocketClient client, SocketApplicationCommand command, Delegate handler)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (handler.Method == null) throw new ArgumentNullException(nameof(handler.Method));

            var mparams = handler.Method.GetParameters();
            var options = command.Options.ToDictionary(x => !string.IsNullOrWhiteSpace(x.Name) ? x.Name : throw new ArgumentException("Option has no name (subcommands are not supported)"), x => x);

            if (mparams.Length != options.Count + 1)
                throw new ArgumentException($"handler had {mparams.Length} arguments but must have {options.Count + 1} - SocketSlashCommand + every option that the command has");

            // Ensure all params match the option names and types (order may be different)
            foreach (var mparam in mparams)
            {
                if (mparam.ParameterType == typeof(SocketSlashCommand))
                    continue;

                options.TryGetValue(mparam.Name ?? "", out var option);
                if (option == null)
                    throw new ArgumentException($"Command option {mparam.Name} not found in command options");

                switch (option.Type)
                {
                    case ApplicationCommandOptionType.String:
                        if (mparam.ParameterType != typeof(string))
                            throw new ArgumentException($"Command option {mparam.Name} is of type {mparam.ParameterType} but must be of type {option.Type}");
                        break;
                    case ApplicationCommandOptionType.Boolean:
                        if (mparam.ParameterType != typeof(bool))
                            throw new ArgumentException($"Command option {mparam.Name} is of type {mparam.ParameterType} but must be of type {option.Type}");
                        break;

                    case ApplicationCommandOptionType.Number:
                        if (mparam.ParameterType != typeof(double) && mparam.ParameterType != typeof(float))
                            throw new ArgumentException($"Command option {mparam.Name} is of type {mparam.ParameterType} but must be of type {option.Type}");
                        break;
                    case ApplicationCommandOptionType.Integer:
                        if (mparam.ParameterType != typeof(long) && mparam.ParameterType != typeof(int))
                            throw new ArgumentException($"Command option {mparam.Name} is of type {mparam.ParameterType} but must be of type {option.Type}");
                        break;

                    case ApplicationCommandOptionType.User:
                        if (!mparam.ParameterType.IsAssignableTo(typeof(IUser)))
                            throw new ArgumentException($"Command option {mparam.Name} is of type {mparam.ParameterType} but must be of type {option.Type}");
                        break;
                    case ApplicationCommandOptionType.Channel:
                        if (!mparam.ParameterType.IsAssignableTo(typeof(IChannel)))
                            throw new ArgumentException($"Command option {mparam.Name} is of type {mparam.ParameterType} but must be of type {option.Type}");
                        break;
                    case ApplicationCommandOptionType.Role:
                        if (!mparam.ParameterType.IsAssignableTo(typeof(IRole)))
                            throw new ArgumentException($"Command option {mparam.Name} is of type {mparam.ParameterType} but must be of type {option.Type}");
                        break;
                    case ApplicationCommandOptionType.Attachment:
                        if (!mparam.ParameterType.IsAssignableTo(typeof(IAttachment)))
                            throw new ArgumentException($"Command option {mparam.Name} is of type {mparam.ParameterType} but must be of type {option.Type}");
                        break;
                    case ApplicationCommandOptionType.Mentionable:
                        if (!mparam.ParameterType.IsAssignableTo(typeof(IMentionable)))
                            throw new ArgumentException($"Command option {mparam.Name} is of type {mparam.ParameterType} but must be of type {option.Type}");
                        break;

                    default:
                        throw new ArgumentException($"Command option {mparam.Name} has unsupported type {mparam.ParameterType}");
                }
            }

            client.SlashCommandExecuted += async eventArgs =>
            {
                if (eventArgs.CommandName == command.Name)
                {
                    try
                    {
                        var incoming = eventArgs.Data.Options.ToDictionary(x => x.Name, x => x);

                        var args = new object[mparams.Length];
                        for (var i = 0; i < mparams.Length; i++)
                        {
                            var mparam = mparams[i];

                            if (mparam.ParameterType == typeof(SocketSlashCommand))
                            {
                                args[i] = eventArgs;
                            }
                            else
                            {
                                Debug.Assert(mparam.Name != null, "mparam.Name != null");
                                if (incoming.TryGetValue(mparam.Name, out var option))
                                {
                                    switch (option.Type)
                                    {
                                        case ApplicationCommandOptionType.Integer:
                                        case ApplicationCommandOptionType.Number:
                                            args[i] = Convert.ChangeType(option.Value ?? 0, mparam.ParameterType);
                                            break;

                                        default:
                                            args[i] = option.Value;
                                            break;
                                    }
                                }
                                else
                                {
                                    // BUG: handle nullable?
                                    if (!mparam.ParameterType.IsClass && !mparam.ParameterType.IsInterface)
                                        args[i] = Convert.ChangeType(0, mparam.ParameterType);
                                }
                            }
                        }

                        var result = handler.DynamicInvoke(args);
                        if (result is Task t)
                            await t;
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"FAILED TO RUN COMMAND {eventArgs.CommandName} in <#{eventArgs.ChannelId}> - {e}");
                        await eventArgs.RespondAsync($"Failed to run command {eventArgs.CommandName} - {e.Message}", ephemeral: true);
                    }
                }
            };
        }
    }
}