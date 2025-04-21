using System;
using System.Text.Json.Serialization;

namespace CHEF.Components.Polls;

/// <summary>
/// Holds user id and their vote
/// </summary>
public class PollEntry
{
    public PollEntry(ulong userId, ulong vote)
    {
        ArgumentOutOfRangeException.ThrowIfZero(userId);
        ArgumentOutOfRangeException.ThrowIfZero(vote);
        UserId = userId;
        Vote = vote;
    }

    [JsonInclude] public ulong UserId { get; init; }
    [JsonInclude] public ulong Vote { get; init; }
}