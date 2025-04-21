using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CHEF.Components.Polls;

/// <summary>
/// Holds poll data for a channel
/// </summary>
public class PollData
{
    public PollData(ulong channelId, ulong entryCount)
    {
        ArgumentOutOfRangeException.ThrowIfZero(channelId);
        ChannelId = channelId;
        StartTime = DateTimeOffset.UtcNow;
        SetEntryCount(entryCount);
    }

    [JsonInclude] public ulong ChannelId { get; init; }
    [JsonInclude] public ulong EntryCount { get; private set; }

    [JsonInclude] public List<PollEntry> Entries { get; init; } = new();
    [JsonInclude] public DateTimeOffset StartTime { get; init; }
    [JsonInclude] public DateTimeOffset EndTime { get; private set; }
    [JsonIgnore] public bool Ended => EndTime != default;

    public void SetEntryCount(ulong entryCount)
    {
        ArgumentOutOfRangeException.ThrowIfZero(entryCount);
        EntryCount = entryCount;
    }

    public void End()
    {
        EndTime = DateTimeOffset.UtcNow;
    }
}