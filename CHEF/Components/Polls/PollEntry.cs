using System;
using System.Text.Json.Serialization;

namespace CHEF.Components.Polls;

/// <summary>
/// Holds user id and their vote
/// </summary>
public class PollEntry
{
    public PollEntry(ulong userId, long vote)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(vote);
        UserId = userId;
        Vote = vote;
    }

    [JsonInclude] public ulong UserId { get; init; }
    [JsonInclude] public long Vote { get; init; }
}