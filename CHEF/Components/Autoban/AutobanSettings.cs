using System.Collections.Generic;

namespace CHEF.Components.Autoban;

public class AutobanSettings
{
    public Dictionary<ulong, List<ulong>> GuildAutobanChannels { get; set; } = new(); // guildId -> list of channelIds
    public Dictionary<ulong, ulong> GuildAlertChannels { get; set; } = new(); // guildId -> alertChannelId
}