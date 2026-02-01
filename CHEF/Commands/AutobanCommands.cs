using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CHEF.Commands;

public class AutobanSettings
{
    public Dictionary<ulong, List<ulong>> GuildAutobanChannels { get; set; } = new(); // guildId -> list of channelIds
    public Dictionary<ulong, ulong> GuildAlertChannels { get; set; } = new(); // guildId -> alertChannelId
}

public static class AutobanSettingsManager
{
    private static readonly string SettingsFile = "autoban_settings.json";
    private static AutobanSettings _settings;

    static AutobanSettingsManager()
    {
        Load();
    }

    public static void Load()
    {
        if (File.Exists(SettingsFile))
        {
            try
            {

                var json = File.ReadAllText(SettingsFile);
                _settings = JsonSerializer.Deserialize<AutobanSettings>(json) ?? new AutobanSettings();
            }
            catch (Exception e)
            {
                _settings = new AutobanSettings();
                Console.WriteLine(e);
            }
        }
        else
        {
            _settings = new AutobanSettings();
        }
    }

    public static void Save()
    {
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }

    public static bool IsAutobanEnabled(ulong guildId, ulong channelId)
    {
        return _settings.GuildAutobanChannels.TryGetValue(guildId, out var channels) && channels.Contains(channelId);
    }

    public static void SetAutobanChannel(ulong guildId, ulong channelId, bool enable)
    {
        if (!_settings.GuildAutobanChannels.TryGetValue(guildId, out var channels))
        {
            channels = new List<ulong>();
            _settings.GuildAutobanChannels[guildId] = channels;
        }
        if (enable)
        {
            if (!channels.Contains(channelId)) channels.Add(channelId);
        }
        else
        {
            channels.Remove(channelId);
        }
        Save();
    }

    public static ulong? GetAlertChannel(ulong guildId)
    {
        return _settings.GuildAlertChannels.TryGetValue(guildId, out var channelId) ? channelId : null;
    }

    public static void SetAlertChannel(ulong guildId, ulong channelId)
    {
        _settings.GuildAlertChannels[guildId] = channelId;
        Save();
    }
}

public class AutobanCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("autoban-set-ban-channel", "Enable or disable autoban for a channel.")]
    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.BanMembers), RequireUserPermission(GuildPermission.BanMembers)]
    public async Task SetAutobanChannel([Summary(description: "Channel to autoban in.")] ITextChannel channel,
                                        [Summary(description: "Enable autoban.")] bool enable)
    {
        AutobanSettingsManager.SetAutobanChannel(Context.Guild.Id, channel.Id, enable);
        await RespondAsync($":white_check_mark: Autoban has been {(enable ? "enabled" : "disabled")} for {channel.Mention}", ephemeral: true);
    }

    [SlashCommand("autoban-set-alert-channel", "Set the channel for autoban alerts.")]
    [CommandContextType(InteractionContextType.Guild), RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.BanMembers), RequireUserPermission(GuildPermission.BanMembers)]
    public async Task SetAutobanAlertChannel([Summary(description: "Channel for ban alerts.")] ITextChannel channel)
    {
        AutobanSettingsManager.SetAlertChannel(Context.Guild.Id, channel.Id);
        await RespondAsync($":white_check_mark: Autoban alert channel set to {channel.Mention}", ephemeral: true);
    }
}
