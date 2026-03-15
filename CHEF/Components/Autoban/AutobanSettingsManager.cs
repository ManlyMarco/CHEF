using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CHEF.Components.Autoban;

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

    public static IReadOnlyList<ulong> GetAutobanChannels(ulong guildId)
    {
        _settings.GuildAutobanChannels.TryGetValue(guildId, out var result);
        return result;
    }
}