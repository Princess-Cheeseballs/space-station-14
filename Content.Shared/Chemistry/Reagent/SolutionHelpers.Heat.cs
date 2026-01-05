using Content.Shared.FixedPoint;
using Content.Shared.Temperature.HeatContainer;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Reagent;

public static partial class SolutionHelpers
{
    /// <summary>
    /// Completely reloads a solution, refreshing volume and heat capacity.
    /// </summary>
    /// <param name="solution">This solution</param>
    /// <param name="protoMan">Prototype Manager</param>
    public static void Update(this Solution solution, IPrototypeManager? protoMan)
    {
        IoCManager.Resolve(ref protoMan);

        var capacity = 0f;
        foreach (var (reagent, quantity) in solution)
        {
            solution.Volume += quantity;

            if (!protoMan.Resolve(reagent.Prototype, out var proto))
                continue;

            capacity += (float)(proto.SpecificHeat * quantity);
        }

        solution.Heat = new HeatContainer(capacity, solution.Temperature);
    }

    /// <summary>
    /// Completely reloads the heat capacity on this entity. Useful for debugging.
    /// </summary>
    /// <param name="solution">This solution</param>
    /// <param name="protoMan">Prototype Manager</param>
    public static void UpdateHeatCapacity(this Solution solution, IPrototypeManager? protoMan)
    {
        IoCManager.Resolve(ref protoMan);

        var capacity = 0f;
        foreach (var (reagent, quantity) in solution)
        {
            if (!protoMan.Resolve(reagent.Prototype, out var proto))
                continue;

            capacity += (float)(proto.SpecificHeat * quantity);
        }

        solution.Heat = new HeatContainer(capacity, solution.Temperature);
    }

    private static float AdjustSolutionHeat(this Solution solution, ReagentPrototype reagent, FixedPoint2 quantity)
    {
        var heat = (float)(reagent.SpecificHeat * quantity);
        solution.Heat.HeatCapacity += heat;
        return heat;
    }

    private static float AdjustSolutionHeat(this Solution solution, ProtoId<ReagentPrototype> reagent, FixedPoint2 quantity, IPrototypeManager? protoMan)
    {
        IoCManager.Resolve(ref protoMan);

        return !protoMan.Resolve(reagent, out var proto) ? 0f : solution.AdjustSolutionHeat(proto, quantity);
    }

    private static float AdjustSolutionHeat(this Solution solution, ReagentQuantity quantity, IPrototypeManager? protoMan)
    {
        return solution.AdjustSolutionHeat(quantity.Reagent.Prototype, quantity.Quantity, protoMan);
    }

    private static float AdjustSolutionHeat(this Solution solution, ReagentId reagent, FixedPoint2 quantity, IPrototypeManager? protoMan)
    {
        return solution.AdjustSolutionHeat(reagent.Prototype, quantity, protoMan);
    }
}
