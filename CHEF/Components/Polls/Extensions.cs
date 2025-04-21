using System;
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
}
