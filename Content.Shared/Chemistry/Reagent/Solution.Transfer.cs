using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Reagent;

public partial struct Solution
{
    #region Split

    /// <inheritdoc cref="Split()"/>
    /// <param name="fraction">The percentage we're removing from this solution.</param>
    public Solution Split(float fraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fraction);
        return Split(Volume * fraction);
    }

    /// <inheritdoc cref="Split()"/>
    /// <param name="volume">Volume we're removing from this solution.</param>
    public Solution Split(FixedPoint2 volume)
    {
        // If we're trying to take the entire solution, just return the entire solution.
        if (volume >= Volume)
            return Split();

        var effVol = Volume.Value;
        var remaining = (long)volume.Value;
        var newSolution = new Solution(Contents.Count);
        for (var i = 0; i < Contents.Count; i++)
        {
            var (reagent, quantity) = Contents[i];
            var split = remaining * quantity.Value / effVol;

            if (split <= 0)
            {
                effVol -= quantity.Value;
                DebugTools.Assert(split == 0, "Negative solution quantity while splitting? Long/int overflow?");
                continue;
            }

            // TODO: Also adjust heat here for better accuracy!

            var splitQuantity = FixedPoint2.FromCents((int)split);
            var newQuantity = quantity - splitQuantity;

            DebugTools.Assert(newQuantity >= 0);

            quantity -= split;

            newSolution.Contents.Add(new ReagentQuantity(reagent, quantity));

            if (newQuantity > FixedPoint2.Zero)
                Contents[i] = new ReagentQuantity(reagent, newQuantity);
            else
                Contents.RemoveSwap(i);

            newSolution.Volume += splitQuantity;
            remaining -= split;
            effVol -= quantity.Value;
        }

        // TODO: Do this within the for loop cause this will drift over time!
        newSolution._heat = _heat;
        newSolution._heat.HeatCapacity *= (float) newSolution.Volume / (float)Volume;
        Volume -= newSolution.Volume;
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
        Remove();
        return solution;
    }


    /// <inheritdoc cref="SplitWhitelist(IPrototypeManager?,ReagentId[])"/>
    public Solution SplitWhitelist(FixedPoint2 volume, IPrototypeManager? protoMan, params ReagentId[] whitelist)
    {
        var whitelistSol = SplitWhitelist(protoMan, whitelist);

        var newSolution = whitelistSol.Split(volume);

        if (whitelistSol.Volume > 0)
            Add(whitelistSol);

        return newSolution;
    }

    /// <summary>
    /// Splits a solution removing all listed reagents.
    /// </summary>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="whitelist">List of reagents we're removing.</param>
    /// <returns>The new solution with the whitelisted reagents removed.</returns>
    public Solution SplitWhitelist(IPrototypeManager? protoMan, params ReagentId[] whitelist)
    {
        var whitelistSol = new Solution(whitelist.Length);
        for (var i = Contents.Count - 1; i >= 0; i--)
        {
            var quantity = Contents[i];
            if (!whitelist.Contains(quantity.Reagent))
                continue;

            whitelistSol.Contents.Add(quantity);
            Contents.RemoveSwap(i);
        }

        return whitelistSol.Split();
    }

    /// <summary>
    /// Splits a solution leaving only the listed reagents.
    /// </summary>
    /// <param name="volume">Max amount we're removing from this solution.</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="blacklist">List of reagents we're keeping.</param>
    /// <returns>The new solution with all reagents except the blacklisted removed.</returns>
    public Solution SplitBlacklist(FixedPoint2 volume, IPrototypeManager? protoMan, params ReagentId[] blacklist)
    {
        var blacklistSol = SplitWhitelist(protoMan, blacklist);

        var newSolution = Split(volume);
        Add(blacklistSol);

        return newSolution;
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
        solution.Remove();
    }

    /// <summary>
    /// Splits a solution and adds it to ourselves
    /// </summary>
    /// <param name="solution">Solution we're adding to ourselves</param>
    /// <param name="volume">How much of the solution we're adding</param>
    public void Merge(Solution solution, FixedPoint2 volume)
    {
        // If we're trying to take more than is possible, just do a full merge
        if (volume >= solution.Volume)
        {
            Merge(solution);
            return;
        }

        var split = solution.Split(volume);
        Merge(split);
    }

    #endregion
}
