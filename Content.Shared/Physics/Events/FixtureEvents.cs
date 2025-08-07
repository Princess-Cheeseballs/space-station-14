using System.Linq;

namespace Content.Shared.Physics.Events;

/// <summary>
/// This handles content level fixture events.
/// </summary>
[ByRefEvent]
public record struct ReEnableFixturesEvent(Dictionary<string, int> FixDict)
{
    /// <summary>
    /// Takes in fixtures being removed by one component and filters them out of the components we're trying to add.
    /// </summary>
    /// <param name="dict2">The second dictionary we're filtering.</param>
    public void Filter(Dictionary<string, int> dict2)
    {
        var matches = FixDict.Where(dict => dict2.ContainsKey(dict.Key));
    }
}
