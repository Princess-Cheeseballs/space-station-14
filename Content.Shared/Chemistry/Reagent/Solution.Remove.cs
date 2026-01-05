using Content.Shared.FixedPoint;
using Content.Shared.Temperature.HeatContainer;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Reagent;

public partial struct Solution
{
    // TODO: Try methods, public prototype (ignoring data) removal methods.

    /// <summary>
    /// Removes a solution from this solution.
    /// </summary>
    /// <param name="otherSolution">Solution we're trying to remove</param>
    public FixedPoint2 Remove(Solution otherSolution)
    {
        if (otherSolution.Volume <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        Volume -= FixedPoint2.Min(otherSolution.Volume, Volume);

        _heat.Subtract(otherSolution._heat.HeatCapacity);

        var removed = FixedPoint2.Zero;
        // iterate backwards because of remove swap.
        for (var i = otherSolution.Contents.Count - 1; i >= 0; i--)
        {
            removed += RemoveReagent(otherSolution.Contents[i]);
        }

        ValidateSolution();
        return removed;
    }

    /// <summary>
    /// Removes a quantity of Reagents from the solution.
    /// </summary>
    /// <param name="reagentQuantity">The quantity of reagents we're removing</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    public FixedPoint2 Remove(ReagentQuantity reagentQuantity, IPrototypeManager? protoMan)
    {
        if (reagentQuantity.Quantity <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        IoCManager.Resolve(ref protoMan);
        if (!protoMan.Resolve<ReagentPrototype>(reagentQuantity.Reagent.Prototype, out var reagent))
            return FixedPoint2.Zero;

        var removed = RemoveReagent(reagentQuantity);
        Volume -= removed;
        _heat.Subtract((float)(reagent.SpecificHeat * removed));
        ValidateSolution();
        return removed;
    }

    /// <inheritdoc cref="Remove(ReagentQuantity,IPrototypeManager?)"/>
    public FixedPoint2 Remove(ReagentId reagent, FixedPoint2 quantity, IPrototypeManager? protoMan)
    {
        return Remove(new ReagentQuantity(reagent, quantity), protoMan);
    }

    /// <inheritdoc cref="Remove(ReagentQuantity,IPrototypeManager?)"/>
    public FixedPoint2 Remove([ForbidLiteral] string reagent, FixedPoint2 quantity, List<ReagentData>? data, IPrototypeManager? protoMan)
    {
        return Remove(new ReagentQuantity(reagent, quantity, data), protoMan);
    }

    /// <inheritdoc cref="Remove(ReagentQuantity,IPrototypeManager?)"/>
    public FixedPoint2 Remove(ReagentPrototype reagent, FixedPoint2 quantity, List<ReagentData>? data = null)
    {
        if (quantity <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        var reagentQuantity = new ReagentQuantity(reagent, quantity, data);
        var removed = RemoveReagent(reagentQuantity);
        Volume -= removed;
        _heat.Subtract((float)(reagent.SpecificHeat * removed));
        ValidateSolution();
        return removed;
    }

    /// <summary>
    /// Removes a ReagentQuantity to this solution without validation or updating the heat container.
    /// Ignores reagent data when searching for reagents.
    /// This should only be used in conjunction with other methods and never on its own.
    /// </summary>
    /// <param name="reagentQuantity">quantity we're removing from this solution.</param>
    private FixedPoint2 RemovePrototype(ReagentQuantity reagentQuantity)
    {
        var removed = FixedPoint2.Zero;

        // Main difference is we compare prototypes and don't return early since there could be multiple reagents with the same prototype!
        for (var i = 0; i < Contents.Count; i++)
        {
            var quantity = Contents[i];
            if (quantity.Reagent.Prototype != reagentQuantity.Reagent.Prototype)
                continue;

            if (quantity.Quantity > reagentQuantity.Quantity)
            {
                Contents[i].Remove(reagentQuantity.Quantity);
                removed += reagentQuantity.Quantity;
            }
            else
            {
                Contents.RemoveSwap(i);
                removed += quantity.Quantity;
            }
        }

        return removed;
    }

    /// <summary>
    /// Removes a ReagentQuantity to this solution without validation or updating the heat container.
    /// This should only be used in conjunction with other methods and never on its own.
    /// </summary>
    /// <param name="reagentQuantity">quantity we're removing from this solution.</param>
    private FixedPoint2 RemoveReagent(ReagentQuantity reagentQuantity)
    {
        for (var i = 0; i < Contents.Count; i++)
        {
            var quantity = Contents[i];
            if (quantity.Reagent != reagentQuantity.Reagent)
                continue;

            if (quantity.Quantity > reagentQuantity.Quantity)
            {
                Contents[i].Remove(reagentQuantity.Quantity);
                return reagentQuantity.Quantity;
            }

            Contents.RemoveSwap(i);
            return quantity.Quantity;
        }

        return FixedPoint2.Zero;
    }

    /// <inheritdoc cref="RemoveReagent(ReagentQuantity)"/>
    private FixedPoint2 RemoveReagent(ReagentId reagent, FixedPoint2 quantity)
    {
        return RemoveReagent(new ReagentQuantity(reagent, quantity));
    }

    /// <inheritdoc cref="RemovePrototype(ReagentQuantity)"/>
    private FixedPoint2 RemovePrototype(ReagentId reagent, FixedPoint2 quantity)
    {
        return RemovePrototype(new ReagentQuantity(reagent, quantity));
    }

    /// <inheritdoc cref="RemovePrototype(ReagentQuantity)"/>
    private FixedPoint2 RemovePrototype([ForbidLiteral] string reagent, FixedPoint2 quantity)
    {
        return RemovePrototype(new ReagentQuantity(reagent, quantity));
    }

    /// <inheritdoc cref="RemovePrototype(ReagentQuantity)"/>
    private FixedPoint2 RemovePrototype([ForbidLiteral] ReagentPrototype reagent, FixedPoint2 quantity)
    {
        return RemovePrototype(new ReagentQuantity(reagent, quantity));
    }

    /// <summary>
    /// Destroys this solution and all its contents.
    /// </summary>
    private void Remove()
    {
        this = new Solution();
    }
}
