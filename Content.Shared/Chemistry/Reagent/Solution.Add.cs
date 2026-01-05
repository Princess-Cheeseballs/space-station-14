using Content.Shared.Atmos;
using Content.Shared.FixedPoint;
using Content.Shared.Temperature.HeatContainer;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Reagent;

public partial struct Solution
{
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
    public void Add(ReagentId reagent,
        FixedPoint2 quantity,
        IPrototypeManager? protoMan,
        float temperature = Atmospherics.T20C)
    {
        Add(new ReagentQuantity(reagent, quantity), protoMan);
    }

    /// <inheritdoc cref="Add(ReagentQuantity,IPrototypeManager?,float)"/>
    public void Add(ReagentPrototype reagent,
        FixedPoint2 quantity,
        List<ReagentData>? data = null,
        float temperature = Atmospherics.T20C)
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
    public void Add([ForbidLiteral] string reagent,
        FixedPoint2 quantity,
        List<ReagentData>? data,
        IPrototypeManager? protoMan,
        float temperature = Atmospherics.T20C)
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
}
