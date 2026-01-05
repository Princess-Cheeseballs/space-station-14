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

        // iterate backwards because of remove swap.
        for (var i = otherSolution.Contents.Count - 1; i >= 0; i--)
        {
            RemoveReagent(otherSolution.Contents[i]);
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
}
