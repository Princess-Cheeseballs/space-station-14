using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Atmos;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Temperature.HeatContainer;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Reagent;

public partial struct Solution
{
    #region Split

    /// <inheritdoc cref="Split()"/>
    /// <param name="volume">Volume we're removing from this solution.</param>
    public Solution Split(FixedPoint2 volume)
    {
        return Split((float)(volume / Volume));
    }

    /// <inheritdoc cref="Split()"/>
    /// <param name="fraction">The percentage we're removing from this solution.</param>
    public Solution Split(float fraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fraction);

        // If we're trying to take the entire solution, just return the entire solution.
        if (fraction >= 1f)
            return Split();

        var newSolution = new Solution(Contents.Count);
        for (var i = 0; i < Contents.Count; i++)
        {
            var quantity = Contents[i];
            quantity.Quantity *= fraction;
            newSolution.Contents.Add(quantity);
            Contents[i] = new ReagentQuantity(quantity.Reagent, Contents[i].Quantity - quantity.Quantity);
        }

        newSolution.Volume = Volume * fraction;
        Volume -= newSolution.Volume;
        newSolution._heat = _heat;
        newSolution._heat.HeatCapacity *= fraction;
        _heat.HeatCapacity -= newSolution.HeatCapacity;

        newSolution.ValidateSolution();
        ValidateSolution();
        return newSolution;
    }

    /// <summary>
    /// Creates a new solution by removing some of this solution.
    /// </summary>
    /// <returns>A new solution removed from this one.</returns>
    public Solution Split()
    {
        var solution = new Solution(this);
        Destroy();
        return solution;
    }


    /// <summary>
    /// Splits a solution removing all listed reagents.
    /// </summary>
    /// <param name="volume">Max amount we're removing from this solution.</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="whitelist">List of reagents we're removing.</param>
    /// <returns>The new solution with the whitelisted reagents removed.</returns>
    public Solution SplitWhitelist(FixedPoint2 volume, IPrototypeManager? protoMan, params ReagentId[] whitelist)
    {
        var sol = new Solution(whitelist.Length);
        for (var i = Contents.Count - 1; i >= 0; i--)
        {
            var quantity = Contents[i];
            if (!whitelist.Contains(quantity.Reagent))
                continue;

            sol.Contents.Add(quantity);

        }
        foreach (var quantity in Contents)
        {
            if (!whitelist.Contains(quantity.Reagent))
                continue;

            sol.Add(quantity, protoMan);
            Remove(quantity, protoMan);
        }
    }

    /// <summary>
    /// Splits a solution leaving only the listed reagents.
    /// </summary>
    /// <param name="volume">Max amount we're removing from this solution.</param>
    /// <param name="blacklist">List of reagents we're keeping.</param>
    /// <returns>The new solution with all reagents except the blacklisted removed.</returns>
    public Solution SplitBlacklist(FixedPoint2 volume, params ReagentId[] blacklist)
    {

    }

    #endregion

    #region Merge

    /// <summary>
    /// Adds the entirety of one solution to this one.
    /// </summary>
    /// <param name="solution">Solution we're adding to ourselves</param>
    public void Merge(Solution solution)
    {
        Add(solution);
        solution.Destroy();
    }

    /// <summary>
    /// Splits a solution and adds it to ourselves
    /// </summary>
    /// <param name="solution">Solution we're adding to ourselves</param>
    /// <param name="volume">How much of the solution we're adding</param>
    public void Merge(Solution solution, FixedPoint2 volume)
    {
        var split = solution.Split(volume);
        Merge(split);
    }

    #endregion
}
