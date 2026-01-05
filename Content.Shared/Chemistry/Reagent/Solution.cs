using System.Collections;
using System.Linq;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.FixedPoint;
using Content.Shared.Temperature.HeatContainer;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Reagent;

/// <summary>
///     A struct which stores reagents and energy (heat).
/// </summary>
[Serializable, NetSerializable]
[DataDefinition]
public partial struct Solution : IEnumerable<ReagentQuantity>, ISerializationHooks, IRobustCloneable<Solution>
{
    /// <summary>
    ///     The name of this solution, if it is contained in some <see cref="SolutionContainerManagerComponent"/>
    /// </summary>
    [DataField]
    public string? Name;

    // This is a list because it is actually faster to add and remove reagents from
    // a list than a dictionary, though contains-reagent checks are slightly slower,
    [DataField("reagents")]
    public List<ReagentQuantity> Contents;

    /// <summary>
    /// A heat container to store this solution's temperature and thermal energy.
    /// </summary>
    /// <remarks>This is private to ensure that modifications to temperature don't try to go over the solution.</remarks>
    [DataField]
    public HeatContainer Heat = new (0);

    /// <inheritdoc cref="HeatContainer.Temperature"/>
    [ViewVariables]
    public float Temperature => Heat.Temperature;

    /// <inheritdoc cref="HeatContainer.Temperature"/>
    [ViewVariables]
    public float TemperatureC => Heat.TemperatureC;

    /// <inheritdoc cref="HeatContainer.HeatCapacity"/>
    [ViewVariables]
    public float HeatCapacity => Heat.HeatCapacity;

    /// <inheritdoc cref="HeatContainer.InternalEnergy"/>
    [ViewVariables]
    public float ThermalEnergy => Heat.InternalEnergy;

    /// <summary>
    ///     The calculated total volume of all reagents in the solution (ex. Total volume of liquid in beaker).
    /// </summary>
    [ViewVariables]
    public FixedPoint2 Volume { get; set; }

    /// <summary>
    ///     If reactions will be checked for when adding reagents to the container.
    /// </summary>
    [DataField]
    public bool CanReact { get; set; } = true;

    /// <summary>
    ///     Constructs an empty solution (ex. an empty beaker).
    /// </summary>
    public Solution() : this(2) // Most objects on the station hold only 1 or 2 reagents.
    {
    }

    /// <summary>
    ///     Constructs an empty solution (ex. an empty beaker).
    /// </summary>
    public Solution(int capacity)
    {
        Contents = new(capacity);
    }

    /// <summary>
    ///     Constructs a solution containing 100% of a reagent (ex. A beaker of pure water).
    /// </summary>
    /// <param name="prototype">The prototype ID of the reagent to add.</param>
    /// <param name="quantity">The quantity in milli-units.</param>
    /// <param name="data"></param>
    public Solution([ForbidLiteral] ProtoId<ReagentPrototype> prototype, FixedPoint2 quantity, List<ReagentData>? data = null) : this(1)
    {
        this.Add(new ReagentId(prototype, data), quantity, null); // TODO: Force people to pass protoman?
    }

    public Solution(IEnumerable<ReagentQuantity> reagents, IPrototypeManager protoMan)
    {
        Contents = new(reagents);

        this.Update(protoMan);
        ValidateSolution();
    }

    public Solution(Solution solution)
    {
        Contents = solution.Contents;
        Volume = solution.Volume;
        Heat = solution.Heat;
        CanReact = solution.CanReact;
        ValidateSolution();
    }

    public Solution Clone()
    {
        return new Solution(this);
    }

    /// <summary>
    /// Destroys this solution and all its contents.
    /// </summary>
    public void Clear()
    {
        this = new Solution();
    }

    public IEnumerator<ReagentQuantity> GetEnumerator()
    {
        return Contents.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    [AssertionMethod]
    public void ValidateSolution()
    {
        // sandbox forbids: [Conditional("DEBUG")]
#if DEBUG
        // Correct volume
        DebugTools.Assert(Contents.Select(x => x.Quantity).Sum() == Volume);

        // All reagents have at least some reagent present.
        DebugTools.Assert(!Contents.Any(x => x.Quantity <= FixedPoint2.Zero));

        // No duplicate reagents iDs
        DebugTools.Assert(Contents.Select(x => x.Reagent).ToHashSet().Count == Contents.Count);

        var cur = HeatCapacity;
        this.UpdateHeatCapacity(null); // TODO: Cache heat capacity or pass ProtoMan
        DebugTools.Assert(MathHelper.CloseTo(HeatCapacity, cur, tolerance: 0.01));
#endif
    }

    void ISerializationHooks.AfterDeserialization()
    {
        Volume = FixedPoint2.Zero;
        foreach (var reagent in Contents)
        {
            Volume += reagent.Quantity;
        }
    }

    public ReagentQuantity this[ReagentId id]
    {
        get
        {
            if (!this.TryGet(out var quantity, id))
                throw new KeyNotFoundException(id.ToString());
            return quantity;
        }
    }
}

