using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Reagent;

public static partial class SolutionHelpers
{
    #region Split

    /// <inheritdoc cref="Split(Solution)"/>
    /// <param name="solution">This Solution</param>
    /// <param name="fraction">The percentage we're removing from this solution.</param>
    public static Solution Split(this Solution solution, float fraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fraction);
        return solution.Split(solution.Volume * fraction);
    }

    /// <inheritdoc cref="Split(Solution)"/>
    /// <param name="solution">This Solution</param>
    /// <param name="volume">Volume we're removing from this solution.</param>
    public static Solution Split(this Solution solution, FixedPoint2 volume)
    {
        // If we're trying to take the entire solution, just return the entire solution.
        if (volume >= solution.Volume)
            return solution.Split();

        var effVol = solution.Volume.Value;
        var remaining = (long)volume.Value;
        var newSolution = new Solution(solution.Contents.Count);
        for (var i = solution.Contents.Count - 1; i >= 0; i--)
        {
            var (reagent, quantity) = solution.Contents[i];
            var split = remaining * quantity.Value / effVol;

            if (split <= 0)
            {
                effVol -= quantity.Value;
                DebugTools.Assert(split == 0, "Negative solution quantity while splitting? Long/int overflow?");
                continue;
            }

            var splitQuantity = FixedPoint2.FromCents((int)split);
            var newQuantity = quantity - splitQuantity;

            DebugTools.Assert(newQuantity >= 0);

            newSolution.AdjustSolutionHeat(reagent, splitQuantity, null); // TODO: Pass Protoman or cache the heat!
            newSolution.Contents.Add(new ReagentQuantity(reagent, splitQuantity));

            if (newQuantity > FixedPoint2.Zero)
                solution.Contents[i] = new ReagentQuantity(reagent, newQuantity);
            else
                solution.Contents.RemoveSwap(i);

            remaining -= split;
            effVol -= quantity.Value;
        }

        newSolution.Volume = volume;
        solution.Volume -= volume;
        solution.Heat.HeatCapacity -= newSolution.HeatCapacity;

        newSolution.ValidateSolution();
        solution.ValidateSolution();
        return newSolution;
    }

    /// <summary>
    /// Removes the contents of this solution and transfers it to another.
    /// </summary>
    /// <param name="solution">This solution.</param>
    /// <returns>A new solution removed from this one.</returns>
    private static Solution Split(this Solution solution)
    {
        var sol = solution;
        solution.Clear();
        return sol;
    }


    /// <inheritdoc cref="SplitWhitelist(Solution,IPrototypeManager?,ReagentId[])"/>
    public static Solution SplitWhitelist(this Solution solution, FixedPoint2 volume, IPrototypeManager? protoMan, params ReagentId[] whitelist)
    {
        var whitelistSol = solution.SplitWhitelist(protoMan, whitelist);

        var newSolution = whitelistSol.Split(volume);

        if (whitelistSol.Volume > 0)
            solution.Add(whitelistSol);

        return newSolution;
    }

    /// <summary>
    /// Splits a solution removing all listed reagents.
    /// </summary>
    /// <param name="solution">This Solution</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="whitelist">List of reagents we're removing.</param>
    /// <returns>The new solution with the whitelisted reagents removed.</returns>
    public static Solution SplitWhitelist(this Solution solution, IPrototypeManager? protoMan, params ReagentId[] whitelist)
    {
        var whitelistSol = new Solution(whitelist.Length);
        for (var i = solution.Contents.Count - 1; i >= 0; i--)
        {
            var quantity = solution.Contents[i];
            if (!whitelist.Contains(quantity.Reagent))
                continue;

            whitelistSol.Contents.Add(quantity);
            solution.Contents.RemoveSwap(i);
        }

        return whitelistSol.Split();
    }

    /// <summary>
    /// Splits a solution leaving only the listed reagents.
    /// </summary>
    /// <param name="solution">This solution.</param>
    /// <param name="volume">Max amount we're removing from this solution.</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="blacklist">List of reagents we're keeping.</param>
    /// <returns>The new solution with all reagents except the blacklisted removed.</returns>
    public static Solution SplitBlacklist(this Solution solution, FixedPoint2 volume, IPrototypeManager? protoMan, params ReagentId[] blacklist)
    {
        var blacklistSol = solution.SplitWhitelist(protoMan, blacklist);

        var newSolution = solution.Split(volume);
        solution.Add(blacklistSol);

        return newSolution;
    }

    /// <summary>
    /// Splits a solution leaving only the listed reagents.
    /// </summary>
    /// <param name="solution">This solution.</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="blacklist">List of reagents we're keeping.</param>
    /// <returns>The new solution with all reagents except the blacklisted removed.</returns>
    public static Solution SplitBlacklist(this Solution solution, IPrototypeManager? protoMan, params ReagentId[] blacklist)
    {
        var blacklistSol = solution.SplitWhitelist(protoMan, blacklist);

        var newSolution = solution.Split();
        solution.Add(blacklistSol);

        return newSolution;
    }

    #endregion

    #region Merge

    /// <summary>
    /// Adds the entirety of one solution to this one.
    /// </summary>
    /// <param name="solution">This solution</param>
    /// <param name="otherSolution">Solution we're adding to this solution</param>
    public static void Merge(this Solution solution, Solution otherSolution)
    {
        solution.Add(otherSolution);
        otherSolution.Clear();
    }

    /// <summary>
    /// Splits a solution and adds it to ourselves
    /// </summary>
    /// <param name="solution">This solution</param>
    /// <param name="otherSolution">Solution we're adding to this solution</param>
    /// <param name="volume">How much of the solution we're adding</param>
    public static void Merge(this Solution solution, Solution otherSolution, FixedPoint2 volume)
    {
        // If we're trying to take more than is possible, just do a full merge
        if (volume >= otherSolution.Volume)
        {
            solution.Merge(otherSolution);
            return;
        }

        solution.Merge(otherSolution.Split(volume));
    }

    #endregion
}
