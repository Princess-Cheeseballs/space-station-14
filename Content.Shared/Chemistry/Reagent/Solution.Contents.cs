using System.Diagnostics.CodeAnalysis;
using Content.Shared.Atmos;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Temperature.HeatContainer;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Reagent;

public partial struct Solution
{
    #region Transfers

    #region Split

    /// <summary>
    /// Creates a new solution by removing some of this solution.
    /// </summary>
    /// <param name="volume">Volume we're removing from this solution.</param>
    /// <returns>A new solution removed from this one.</returns>
    public Solution Split(FixedPoint2 volume)
    {
        return Split((float)(volume / Volume));
    }

    /// <inheritdoc cref="Split(FixedPoint2)"/>
    /// <param name="fraction">The percentage we're removing from this solution.</param>
    public Solution Split(float fraction)
    {
        if (fraction <= 0 || fraction >= 1)
            throw new ArgumentOutOfRangeException(nameof(fraction), "Param must be between 0 and 1");

        var newSolution = new Solution(this, fraction);
        newSolution.ValidateSolution();
        Remove(newSolution);
        return newSolution;
    }

    /// <summary>
    /// Splits a solution removing all listed reagents.
    /// </summary>
    /// <param name="volume">Max amount we're removing from this solution.</param>
    /// <param name="whitelist">List of reagents we're removing.</param>
    /// <returns>The new solution with the whitelisted reagents removed.</returns>
    public Solution SplitWhitelist(FixedPoint2 volume, IEnumerable<ReagentId> whitelist)
    {

    }

    /// <summary>
    /// Splits a solution leaving only the listed reagents.
    /// </summary>
    /// <param name="volume">Max amount we're removing from this solution.</param>
    /// <param name="blacklist">List of reagents we're keeping.</param>
    /// <returns>The new solution with all reagents except the blacklisted removed.</returns>
    public Solution SplitBlacklist(FixedPoint2 volume, IEnumerable<ReagentId> blacklist)
    {

    }

    #endregion

    #region Merge

    public void Merge(Solution solution)
    {
        Add(solution);
        solution.Destroy();
    }

    #endregion

    #endregion Transfers

    #region Add

    /// <summary>
    /// Adds a specified solution to another solution.
    /// This method is non-destructive and does not modify the otherSolution
    /// Use <see cref="To do"/> if you want to merge solutions
    /// </summary>
    /// <param name="otherSolution">Solution which we're adding</param>
    public void Add(Solution otherSolution)
    {
        if (otherSolution.Volume <= FixedPoint2.Zero)
            return;

        Volume += otherSolution.Volume;
        _heat.Merge(otherSolution._heat);

        foreach (var reagentQuantity in otherSolution.Contents)
        {
            AddReagent(reagentQuantity);
        }

        ValidateSolution();
    }


    /// <summary>
    /// Adds a specified reagent quantity to this solution.
    /// This method is non-destructive and does not modify the quantity being added.
    /// Use <see cref="To do"/> if you want to transfer the quantity rather than copy.
    /// </summary>
    /// <param name="reagentQuantity">Quantity we're adding</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="temperature">Temperature of the volume we're adding</param>
    public void Add(ReagentQuantity reagentQuantity, IPrototypeManager? protoMan, float temperature = Atmospherics.T20C)
    {
        if (reagentQuantity.Quantity <= FixedPoint2.Zero)
            return;

        IoCManager.Resolve(ref protoMan);
        if (!protoMan.Resolve<ReagentPrototype>(reagentQuantity.Reagent.Prototype, out var reagent))
            return;

        AddReagent(reagentQuantity);
        Volume += reagentQuantity.Quantity;
        var heat = new HeatContainer((float)(reagent.SpecificHeat * reagentQuantity.Quantity), temperature);
        _heat.Merge(heat);
        ValidateSolution();
    }

    /// <inheritdoc cref="Add(ReagentQuantity,IPrototypeManager?,float)"/>
    public void Add(ReagentId reagent, FixedPoint2 quantity, IPrototypeManager? protoMan, float temperature = Atmospherics.T20C)
    {
        Add(new ReagentQuantity(reagent, quantity), protoMan);
    }

    /// <inheritdoc cref="Add(ReagentQuantity,IPrototypeManager?,float)"/>
    public void Add(ReagentPrototype reagent, FixedPoint2 quantity, List<ReagentData>? data = null, float temperature = Atmospherics.T20C)
    {
        if (quantity <= FixedPoint2.Zero)
            return;

        var reagentQuantity = new ReagentQuantity(reagent, quantity, data);
        AddReagent(reagentQuantity);
        Volume += reagentQuantity.Quantity;
        var heat = new HeatContainer((float)(reagent.SpecificHeat * quantity), temperature);
        _heat.Merge(heat);
        ValidateSolution();
    }

    /// <inheritdoc cref="Add(ReagentQuantity,IPrototypeManager?,float)"/>
    public void Add([ForbidLiteral] string reagent, FixedPoint2 quantity, List<ReagentData>? data, IPrototypeManager? protoMan, float temperature = Atmospherics.T20C)
    {
        Add(new ReagentQuantity(reagent, quantity, data), protoMan);
    }

    /// <summary>
    /// Adds a ReagentQuantity to this solution without validation or updating the heat container.
    /// This should only be used in conjunction with other methods and never on its own.
    /// </summary>
    /// <param name="reagentQuantity">quantity we're adding to this solution.</param>
    private void AddReagent(ReagentQuantity reagentQuantity)
    {
        for (var i = 0; i < Contents.Count; i++)
        {
            if (Contents[i].Reagent != reagentQuantity.Reagent)
                continue;

            Contents[i].Add(reagentQuantity);
            return;
        }

        Contents.Add(reagentQuantity);
    }

    /// <inheritdoc cref="AddReagent(ReagentQuantity)"/>
    private void AddReagent(ReagentId reagent, FixedPoint2 quantity)
    {
        AddReagent(new ReagentQuantity(reagent, quantity));
    }

    /// <inheritdoc cref="AddReagent(ReagentQuantity)"/>
    private void AddReagent([ForbidLiteral] ReagentPrototype reagent, FixedPoint2 quantity)
    {
        AddReagent(new ReagentQuantity(reagent, quantity));
    }

    /// <inheritdoc cref="AddReagent(ReagentQuantity)"/>
    private void AddReagent([ForbidLiteral] string reagent, FixedPoint2 quantity)
    {
        AddReagent(new ReagentQuantity(reagent, quantity));
    }

    #endregion

    #region Remove

    /// <summary>
    /// Removes a solution from this solution.
    /// </summary>
    /// <param name="otherSolution">Solution we're trying to remove</param>
    public void Remove(Solution otherSolution)
    {
        if (otherSolution.Volume <= FixedPoint2.Zero)
            return;

        Volume -= FixedPoint2.Min(otherSolution.Volume, Volume);

        _heat.Subtract(otherSolution._heat.HeatCapacity);

        foreach (var reagentQuantity in otherSolution.Contents)
        {
            RemoveReagent(reagentQuantity);
        }

        ValidateSolution();
    }

    /// <summary>
    /// Removes a quantity of Reagents from the solution.
    /// </summary>
    /// <param name="reagentQuantity">The quantity of reagents we're removing</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    public void Remove(ReagentQuantity reagentQuantity, IPrototypeManager? protoMan)
    {
        if (reagentQuantity.Quantity <= FixedPoint2.Zero)
            return;

        IoCManager.Resolve(ref protoMan);
        if (!protoMan.Resolve<ReagentPrototype>(reagentQuantity.Reagent.Prototype, out var reagent))
            return;

        AddReagent(reagentQuantity);
        Volume += reagentQuantity.Quantity;
        _heat.Subtract((float)(reagent.SpecificHeat * reagentQuantity.Quantity));
        ValidateSolution();
    }

    /// <inheritdoc cref="Remove(ReagentQuantity,IPrototypeManager?)"/>
    public void Remove(ReagentId reagent, FixedPoint2 quantity, IPrototypeManager? protoMan)
    {
        Remove(new ReagentQuantity(reagent, quantity), protoMan);
    }

    /// <inheritdoc cref="Remove(ReagentQuantity,IPrototypeManager?)"/>
    public void Remove([ForbidLiteral] string reagent, FixedPoint2 quantity, List<ReagentData>? data, IPrototypeManager? protoMan)
    {
        Remove(new ReagentQuantity(reagent, quantity, data), protoMan);
    }

    /// <inheritdoc cref="Remove(ReagentQuantity,IPrototypeManager?)"/>
    public void Remove(ReagentPrototype reagent, FixedPoint2 quantity, List<ReagentData>? data = null)
    {
        if (quantity <= FixedPoint2.Zero)
            return;

        var reagentQuantity = new ReagentQuantity(reagent, quantity, data);
        RemoveReagent(reagentQuantity);
        Volume -= reagentQuantity.Quantity;
        _heat.Subtract((float)(reagent.SpecificHeat * quantity));
        ValidateSolution();
    }

    /// <summary>
    /// Removes a ReagentQuantity to this solution without validation or updating the heat container.
    /// This should only be used in conjunction with other methods and never on its own.
    /// </summary>
    /// <param name="reagentQuantity">quantity we're removing from this solution.</param>
    private void RemoveReagent(ReagentQuantity reagentQuantity)
    {
        for (var i = 0; i < Contents.Count; i++)
        {
            var quantity = Contents[i];
            if (quantity.Reagent != reagentQuantity.Reagent)
                continue;

            if (quantity.Quantity > reagentQuantity.Quantity)
                Contents[i].Remove(reagentQuantity.Quantity);
            else
                Contents.RemoveSwap(i);

            return;
        }
    }

    /// <inheritdoc cref="RemoveReagent(ReagentQuantity)"/>
    private void RemoveReagent(ReagentId reagent, FixedPoint2 quantity)
    {
        RemoveReagent(new ReagentQuantity(reagent, quantity));
    }

    /// <inheritdoc cref="RemoveReagent(ReagentQuantity)"/>
    private void RemoveReagent([ForbidLiteral] string reagent, FixedPoint2 quantity)
    {
        RemoveReagent(new ReagentQuantity(reagent, quantity));
    }

    /// <inheritdoc cref="RemoveReagent(ReagentQuantity)"/>
    private void RemoveReagent([ForbidLiteral] ReagentPrototype reagent, FixedPoint2 quantity)
    {
        RemoveReagent(new ReagentQuantity(reagent, quantity));
    }

    #endregion

    #region Get

    public bool TryGet(ReagentId id, out ReagentQuantity quantity)
    {
        quantity = Get(id);
        return quantity.Quantity > 0;
    }

    public ReagentQuantity Get(ReagentId id)
    {
        foreach (var quantity in Contents)
        {
            if (quantity.Reagent == id)
                return quantity;
        }

        return new ReagentQuantity(id, FixedPoint2.Zero);
    }

    public bool TryGet(ProtoId<ReagentPrototype> id, out ReagentQuantity quantity)
    {
        quantity = Get(id);
        return quantity.Quantity > 0;
    }

    public ReagentQuantity Get(ProtoId<ReagentPrototype> id)
    {
        var reagent = new ReagentQuantity(id, FixedPoint2.Zero);

        foreach (var quantity in Contents)
        {
            if (quantity.Reagent.Prototype == id)
                reagent.Add(quantity.Quantity);
        }

        return reagent;
    }

    public bool TryGet(ReagentPrototype id, out ReagentQuantity quantity)
    {
        return TryGet(id.ID, out quantity);
    }

    public ReagentQuantity Get(ReagentPrototype id)
    {
        return Get(id.ID);
    }

    #endregion
}
