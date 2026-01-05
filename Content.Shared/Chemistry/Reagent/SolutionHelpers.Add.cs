using Content.Shared.Atmos;
using Content.Shared.FixedPoint;
using Content.Shared.Temperature.HeatContainer;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Reagent;

public static partial class SolutionHelpers
{
    public static void Add(this Solution solution, FixedPoint2 volume)
    {
        var effVol = solution.Volume.Value;
        var remaining = (long)volume.Value;
        for (var i = 0 ; i < solution.Contents.Count; i++)
        {
            var (reagent, quantity) = solution.Contents[i];
            var split = remaining * quantity.Value / effVol;

            var splitQuantity = FixedPoint2.FromCents((int)split);
            var newQuantity = quantity + splitQuantity;

            solution.Contents[i] = new ReagentQuantity(reagent, newQuantity);
            solution.AdjustSolutionHeat(reagent, splitQuantity, null); // TODO: Pass Protoman or cache the heat!

            remaining -= split;
            effVol -= quantity.Value;
        }

        solution.Volume += volume;
        solution.ValidateSolution();
    }

    public static void Add(this Solution solution, Solution otherSolution, FixedPoint2 volume)
    {
        var effVol = solution.Volume.Value;
        var remaining = (long)volume.Value;
        foreach (var (reagent, quantity) in otherSolution.Contents)
        {
            var split = remaining * quantity.Value / effVol;
            var splitQuantity = FixedPoint2.FromCents((int)split);

            solution.AddReagent(new ReagentQuantity(reagent, splitQuantity)); // TODO: Pass Protoman or cache the heat!
            solution.AdjustSolutionHeat(reagent, splitQuantity, null);

            remaining -= split;
            effVol -= quantity.Value;
        }

        solution.Volume += volume;
        solution.ValidateSolution();
    }

    /// <summary>
    /// Adds a specified solution to another solution.
    /// This method is non-destructive and does not modify the otherSolution
    /// Use <see cref="Merge(Solution,Solution)"/> if you want to merge solutions
    /// </summary>
    /// <param name="solution">This solution.</param>
    /// <param name="otherSolution">Solution which we're adding to this solution.</param>
    public static void Add(this Solution solution, Solution otherSolution)
    {
        if (otherSolution.Volume <= FixedPoint2.Zero)
            return;

        solution.Volume += otherSolution.Volume;
        solution.Heat.Merge(otherSolution.Heat);

        foreach (var reagentQuantity in otherSolution.Contents)
        {
            solution.AddReagent(reagentQuantity);
        }

        solution.ValidateSolution();
    }

    /// <inheritdoc cref="Add(Solution,ReagentQuantity,IPrototypeManager?,float)"/>
    public static void Add(this Solution solution, IEnumerable<ReagentQuantity> reagents, IPrototypeManager? protoMan, float temperature = Atmospherics.T20C)
    {
        foreach (var reagentQuantity in reagents)
        {
            solution.Add(reagentQuantity, protoMan, temperature);
        }

        solution.ValidateSolution();
    }

    /// <summary>
    /// Adds a specified reagent quantity to this solution.
    /// This method is non-destructive and does not modify the quantity being added.
    /// </summary>
    /// <param name="solution">This solution.</param>
    /// <param name="reagentQuantity">Quantity we're adding</param>
    /// <param name="protoMan">PrototypeManager for heat conservation.</param>
    /// <param name="temperature">Temperature of the volume we're adding</param>
    public static void Add(this Solution solution, ReagentQuantity reagentQuantity, IPrototypeManager? protoMan, float temperature = Atmospherics.T20C)
    {
        if (reagentQuantity.Quantity <= FixedPoint2.Zero)
            return;

        IoCManager.Resolve(ref protoMan);
        if (!protoMan.Resolve(reagentQuantity.Reagent.Prototype, out var reagent))
            return;

        solution.AddReagent(reagentQuantity);
        solution.Volume += reagentQuantity.Quantity;
        var heat = new HeatContainer((float)(reagent.SpecificHeat * reagentQuantity.Quantity), temperature);
        solution.Heat.Merge(heat);
        solution.ValidateSolution();
    }

    /// <inheritdoc cref="Add(Solution,ReagentQuantity,IPrototypeManager?,float)"/>
    public static void Add(this Solution solution,
        ReagentId reagent,
        FixedPoint2 quantity,
        IPrototypeManager? protoMan,
        float temperature = Atmospherics.T20C)
    {
        solution.Add(new ReagentQuantity(reagent, quantity), protoMan);
    }

    /// <inheritdoc cref="Add(Solution,ReagentQuantity,IPrototypeManager?,float)"/>
    public static void Add(this Solution solution,
        ReagentPrototype reagent,
        FixedPoint2 quantity,
        List<ReagentData>? data = null,
        float temperature = Atmospherics.T20C)
    {
        if (quantity <= FixedPoint2.Zero)
            return;

        var reagentQuantity = new ReagentQuantity(reagent, quantity, data);
        solution.AddReagent(reagentQuantity);
        solution.Volume += reagentQuantity.Quantity;
        var heat = new HeatContainer((float)(reagent.SpecificHeat * quantity), temperature);
        solution.Heat.Merge(heat);
        solution.ValidateSolution();
    }

    /// <inheritdoc cref="Add(Solution,ReagentQuantity,IPrototypeManager?,float)"/>
    public static void Add(this Solution solution,
        [ForbidLiteral] string reagent,
        FixedPoint2 quantity,
        List<ReagentData>? data,
        IPrototypeManager? protoMan,
        float temperature = Atmospherics.T20C)
    {
        solution.Add(new ReagentQuantity(reagent, quantity, data), protoMan);
    }

    /// <summary>
    /// Adds a ReagentQuantity to this solution without validation or updating the heat container.
    /// This should only be used in conjunction with other methods and never on its own.
    /// </summary>
    /// <param name="solution">This solution</param>
    /// <param name="reagentQuantity">quantity we're adding to this solution.</param>
    private static void AddReagent(this Solution solution, ReagentQuantity reagentQuantity)
    {
        for (var i = 0; i < solution.Contents.Count; i++)
        {
            if (solution.Contents[i].Reagent != reagentQuantity.Reagent)
                continue;

            solution.Contents[i].Add(reagentQuantity);
            return;
        }

        solution.Contents.Add(reagentQuantity);
    }
}
