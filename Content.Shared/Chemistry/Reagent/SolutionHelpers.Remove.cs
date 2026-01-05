using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Reagent;

public static partial class SolutionHelpers
{
    /// <summary>
    /// A variant of <see cref="Split(Solution,FixedPoint2)"/> that doesn't create a new solution.
    /// </summary>
    /// <param name="solution">This Solution.</param>
    /// <param name="volume">The amount we're trying to remove.</param>
    public static void Remove(this Solution solution, FixedPoint2 volume)
    {
        if (volume >= solution.Volume)
        {
            solution.Clear();
            return;
        }

        var effVol = solution.Volume.Value;
        var remaining = (long)volume.Value;
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

            solution.AdjustSolutionHeat(reagent, -splitQuantity, null); // TODO: Pass Protoman or cache the heat!

            if (newQuantity > FixedPoint2.Zero)
                solution.Contents[i] = new ReagentQuantity(reagent, newQuantity);
            else
                solution.Contents.RemoveSwap(i);

            remaining -= split;
            effVol -= quantity.Value;
        }

        solution.Volume -= volume;
        solution.ValidateSolution();
    }

    /// <summary>
    /// Tries to remove one solution from another solution.
    /// </summary>
    /// <param name="solution">This solution</param>
    /// <param name="otherSolution">Solution we're trying to remove</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="volume">The volume we were able to remove</param>
    /// <returns>If we were able to remove any volume</returns>
    public static bool TryRemove(this Solution solution, Solution otherSolution, IPrototypeManager? protoMan, out FixedPoint2 volume)
    {
        volume = solution.Remove(otherSolution, protoMan);
        return volume > FixedPoint2.Zero;
    }

    /// <summary>
    /// Removes a solution from this solution.
    /// </summary>
    /// <param name="solution">This solution</param>
    /// <param name="otherSolution">Solution we're trying to remove</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <returns>Volume removed</returns>
    public static FixedPoint2 Remove(this Solution solution, Solution otherSolution, IPrototypeManager? protoMan)
    {
        var removed = FixedPoint2.Zero;

        // iterate backwards because of remove swap.
        foreach (var reagent in otherSolution.Contents)
        {
            removed += solution.Remove(reagent, protoMan);
        }

        solution.ValidateSolution();
        return removed;
    }

    /// <summary>
    /// Tries to remove one solution from another solution.
    /// </summary>
    /// <param name="solution">This Solution</param>
    /// <param name="reagentQuantity">The quantity of reagents we're removing</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="volume"></param>
    /// <returns>If we were able to remove any volume</returns>
    public static bool TryRemove(this Solution solution, ReagentQuantity reagentQuantity, IPrototypeManager? protoMan, out FixedPoint2 volume)
    {
        volume = solution.Remove(reagentQuantity, protoMan);
        return volume > FixedPoint2.Zero;
    }

    /// <summary>
    /// Removes a quantity of Reagents from the solution.
    /// </summary>
    /// <param name="solution">This Solution</param>
    /// <param name="reagentQuantity">The quantity of reagents we're removing</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    public static FixedPoint2 Remove(this Solution solution, ReagentQuantity reagentQuantity, IPrototypeManager? protoMan)
    {
        if (reagentQuantity.Quantity <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        var removed = solution.QuantityRemove(reagentQuantity);
        solution.Volume -= removed;
        solution.AdjustSolutionHeat(reagentQuantity.Reagent.Prototype, removed, protoMan);
        solution.ValidateSolution();
        return removed;
    }

    /// <inheritdoc cref="TryRemove(Solution,ReagentQuantity,IPrototypeManager?,out FixedPoint2)"/>
    public static bool TryRemove(this Solution solution, ReagentId reagent, FixedPoint2 quantity, IPrototypeManager? protoMan, out FixedPoint2 volume)
    {
        return solution.TryRemove(new ReagentQuantity(reagent, quantity), protoMan, out volume);
    }

    /// <inheritdoc cref="Remove(Solution,ReagentQuantity,IPrototypeManager?)"/>
    public static FixedPoint2 Remove(this Solution solution, ReagentId reagent, FixedPoint2 quantity, IPrototypeManager? protoMan)
    {
        return solution.Remove(new ReagentQuantity(reagent, quantity), protoMan);
    }

    /// <inheritdoc cref="TryRemove(Solution,ReagentQuantity,IPrototypeManager?,out FixedPoint2)"/>
    public static bool TryRemove(this Solution solution, [ForbidLiteral] string reagent, FixedPoint2 quantity, IPrototypeManager? protoMan, out FixedPoint2 volume)
    {
        return solution.TryRemove(new ReagentQuantity(reagent, quantity), protoMan, out volume);
    }

    /// <inheritdoc cref="Remove(Solution,ReagentQuantity,IPrototypeManager?)"/>
    public static FixedPoint2 Remove(this Solution solution, [ForbidLiteral] string reagent, FixedPoint2 quantity, List<ReagentData>? data, IPrototypeManager? protoMan)
    {
        return solution.Remove(new ReagentQuantity(reagent, quantity, data), protoMan);
    }

    /// <inheritdoc cref="TryRemove(Solution,ReagentQuantity,IPrototypeManager?,out FixedPoint2)"/>
    public static bool TryRemove(this Solution solution, ReagentPrototype reagent, FixedPoint2 quantity, List<ReagentData>? data, out FixedPoint2 volume)
    {
        volume = solution.Remove(reagent, quantity, data);
        return volume > FixedPoint2.Zero;
    }

    /// <inheritdoc cref="Remove(Solution,ReagentQuantity,IPrototypeManager?)"/>
    public static FixedPoint2 Remove(this Solution solution, ReagentPrototype reagent, FixedPoint2 quantity, List<ReagentData>? data)
    {
        if (quantity <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        var reagentQuantity = new ReagentQuantity(reagent, quantity, data);
        var removed = solution.QuantityRemove(reagentQuantity);
        solution.Volume -= removed;
        solution.AdjustSolutionHeat(reagent, -removed);
        solution.ValidateSolution();
        return removed;
    }

    /// <summary>
    /// Tries to remove one solution from another solution.
    /// </summary>
    /// <param name="solution">This Solution</param>
    /// <param name="otherSolution">Solution we're trying to remove</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="volume">The volume we were able to remove</param>
    /// <returns>If we were able to remove any volume</returns>
    public static bool TryRemoveProto(this Solution solution, Solution otherSolution, IPrototypeManager? protoMan, out FixedPoint2 volume)
    {
        volume = solution.RemoveProto(otherSolution, protoMan);
        return volume > FixedPoint2.Zero;
    }

    /// <summary>
    /// Removes a solution from this solution.
    /// </summary>
    /// <param name="solution">This Solution</param>
    /// <param name="otherSolution">Solution we're trying to remove</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <returns>Volume removed</returns>
    public static FixedPoint2 RemoveProto(this Solution solution, Solution otherSolution, IPrototypeManager? protoMan)
    {
        var removed = FixedPoint2.Zero;
        // iterate backwards because of remove swap.
        foreach (var reagent in otherSolution)
        {
            removed += solution.RemoveProto(reagent, protoMan);
        }

        solution.ValidateSolution();
        return removed;
    }

    public static bool TryRemoveProto(this Solution solution, [ForbidLiteral] ProtoId<ReagentPrototype> reagent, FixedPoint2 quantity, IPrototypeManager? protoMan, out FixedPoint2 volume)
    {
        volume = solution.RemoveProto(reagent, quantity, protoMan);
        return volume > FixedPoint2.Zero;
    }

    public static FixedPoint2 RemoveProto(this Solution solution, [ForbidLiteral] ProtoId<ReagentPrototype> reagent, FixedPoint2 quantity, IPrototypeManager? protoMan)
    {
        if (quantity <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        var removed = solution.ProtoRemove(reagent, quantity);
        solution.Volume -= removed;
        solution.AdjustSolutionHeat(reagent, -removed, protoMan);
        solution.ValidateSolution();

        return removed;
    }

    public static FixedPoint2 RemoveProto(this Solution solution, [ForbidLiteral] ReagentPrototype reagent, FixedPoint2 quantity, IPrototypeManager? protoMan)
    {
        if (quantity <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        var removed = solution.ProtoRemove(reagent, quantity);
        solution.Volume -= removed;
        solution.AdjustSolutionHeat(reagent, -removed);
        solution.ValidateSolution();

        return removed;
    }

    public static bool TryRemoveProto(this Solution solution, ReagentQuantity quantity, IPrototypeManager? protoMan, out FixedPoint2 volume)
    {
        return solution.TryRemoveProto(quantity.Reagent, quantity.Quantity, protoMan, out volume);
    }

    public static FixedPoint2 RemoveProto(this Solution solution, ReagentQuantity quantity, IPrototypeManager? protoMan)
    {
        return solution.RemoveProto(quantity.Reagent, quantity.Quantity, protoMan);
    }

    public static bool TryRemoveProto(this Solution solution, ReagentId reagentId, FixedPoint2 quantity, IPrototypeManager? protoMan, out FixedPoint2 volume)
    {
        return solution.TryRemoveProto(reagentId.Prototype, quantity, protoMan, out volume);
    }

    public static FixedPoint2 RemoveProto(this Solution solution, ReagentId reagentId, FixedPoint2 quantity, IPrototypeManager? protoMan)
    {
        return solution.RemoveProto(reagentId.Prototype, quantity, protoMan);
    }

    /// <summary>
    /// Removes a ReagentQuantity to this solution without validation or updating the heat container.
    /// This should only be used in conjunction with other methods and never on its own.
    /// </summary>
    /// <param name="solution">This Solution</param>
    /// <param name="reagentQuantity">quantity we're removing from this solution.</param>
    [PublicAPI]
    private static FixedPoint2 QuantityRemove(this Solution solution, ReagentQuantity reagentQuantity)
    {
        for (var i = solution.Contents.Count - 1; i >= 0; i--)
        {
            var quantity = solution.Contents[i];
            if (quantity.Reagent != reagentQuantity.Reagent)
                continue;

            if (quantity.Quantity > reagentQuantity.Quantity)
            {
                solution.Contents[i].Remove(reagentQuantity.Quantity);
                return reagentQuantity.Quantity;
            }

            solution.Contents.RemoveSwap(i);
            return quantity.Quantity;
        }

        return FixedPoint2.Zero;
    }

    /// <summary>
    /// Removes a reagent prototype from the solution at a specified amount.
    /// This removal is not even, so if there's multiple prototypes, it will subtract from the largest first!
    /// </summary>
    /// <param name="solution">This Solution</param>
    /// <param name="reagent">ProtoId we're removing</param>
    /// <param name="quantity">Amount we're removing</param>
    /// <returns>Volume removed</returns>
    private static FixedPoint2 ProtoRemove(this Solution solution, [ForbidLiteral] ProtoId<ReagentPrototype> reagent, FixedPoint2 quantity)
    {
        var removed = FixedPoint2.Zero;

        // Main difference is we compare prototypes and don't return early since there could be multiple reagents with the same prototype!
        var i = solution.Contents.Count - 1;
        while (quantity > FixedPoint2.Zero && i >= 0)
        {
            var (id, amount) = solution.Contents[i];
            if (id.Prototype != reagent)
                continue;

            if (amount > quantity)
            {
                solution.Contents[i].Remove(quantity);
                removed += quantity;
                return removed;
            }

            solution.Contents.RemoveSwap(i);
            removed += amount;
            quantity -= amount;
            i--;
        }

        return removed;
    }

    /// <inheritdoc cref="ProtoRemove(Solution,ProtoId{ReagentPrototype},FixedPoint2)"/>
    private static FixedPoint2 ProtoRemove(this Solution solution, ReagentQuantity reagentQuantity)
    {
        return solution.ProtoRemove(reagentQuantity.Reagent.Prototype, reagentQuantity.Quantity);
    }
}
