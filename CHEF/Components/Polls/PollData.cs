using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CHEF.Components.Polls;

/// <summary>
/// Holds poll data for a channel
/// </summary>
public class PollData
{
    public PollData(string pollId, long entryCount, bool staffOnly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pollId);
        PollId = pollId;
        StaffOnly = staffOnly;
        StartTime = DateTimeOffset.UtcNow;
        SetEntryCount(entryCount);
    }

    [JsonInclude] public string PollId { get; init; }
    [JsonInclude] public bool StaffOnly { get; init; }
    [JsonInclude] public long EntryCount { get; private set; }

    [JsonInclude] public List<PollEntry> Entries { get; init; } = new();
    [JsonInclude] public DateTimeOffset StartTime { get; init; }
    [JsonInclude] public DateTimeOffset EndTime { get; private set; }
    [JsonIgnore] public bool Ended => EndTime != default;

    public void SetEntryCount(long entryCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryCount);
        EntryCount = entryCount;
    }

    public void End()
    {
        EndTime = DateTimeOffset.UtcNow;
    }
}