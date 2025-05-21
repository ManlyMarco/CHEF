using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace CHEF.Commands.Polls;

public class PollNameAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var isStaff = ContestVoteCommand.UserIsStaff(context);
        // max - 25 suggestions at a time (API limit)
        var results = ContestVoteCommand.GetActivePollList(isStaff).Take(25);

        return Task.FromResult(AutocompletionResult.FromSuccess(results.Select(x => new AutocompleteResult(x, x))));
    }
}
