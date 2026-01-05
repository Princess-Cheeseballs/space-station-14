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
///     A solution of reagents.
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
    private HeatContainer _heat = new (0);

    /// <inheritdoc cref="HeatContainer.Temperature"/>
    [ViewVariables]
    public float Temperature => _heat.Temperature;

    /// <inheritdoc cref="HeatContainer.Temperature"/>
    [ViewVariables]
    public float TemperatureC => _heat.TemperatureC;

    /// <inheritdoc cref="HeatContainer.HeatCapacity"/>
    [ViewVariables]
    public float HeatCapacity => _heat.HeatCapacity;

    /// <inheritdoc cref="HeatContainer.InternalEnergy"/>
    [ViewVariables]
    public float ThermalEnergy => _heat.InternalEnergy;

    /// <summary>
    ///     The calculated total volume of all reagents in the solution (ex. Total volume of liquid in beaker).
    /// </summary>
    [ViewVariables]
    public FixedPoint2 Volume { get; set; }

    /// <summary>
    ///     Maximum volume this solution supports.
    /// </summary>
    /// <remarks>
    ///     A value of zero means the maximum will automatically be set equal to the current volume during
    ///     initialization. Note that most solution methods ignore max volume altogether, but various solution
    ///     systems use this.
    /// </remarks>
    [DataField("maxVol")]
    public FixedPoint2 MaxVolume { get; set; } = FixedPoint2.Zero;

    public float FillFraction => MaxVolume == 0 ? 1 : Volume.Float() / MaxVolume.Float();

    /// <summary>
    ///     Volume needed to fill this container.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 AvailableVolume => MaxVolume - Volume;

    /// <summary>
    ///     If reactions will be checked for when adding reagents to the container.
    /// </summary>
    [DataField]
    public bool CanReact { get; set; } = true;

    /// <summary>
    ///     If true, then <see cref="HeatCapacity"/> needs to be recomputed.
    /// </summary>
    [ViewVariables] private bool _heatCapacityDirty = true;

    [ViewVariables(VVAccess.ReadWrite)]
    private int _heatCapacityUpdateCounter;

    // This value is arbitrary btw.
    private const int HeatCapacityUpdateInterval = 15;

    private void RecalculateHeatCapacity(IPrototypeManager? protoMan)
    {
        IoCManager.Resolve(ref protoMan);
        // TODO: Do we even need to do this shit???
        DebugTools.Assert(_heatCapacityDirty);
        _heatCapacityDirty = false;
        _heat.HeatCapacity = 0;
        foreach (var (reagent, quantity) in Contents)
        {
            _heat.HeatCapacity += (float)quantity * protoMan.Index<ReagentPrototype>(reagent.Prototype).SpecificHeat;
        }

        _heatCapacityUpdateCounter = 0;
    }

    public void CheckRecalculateHeatCapacity()
    {
        // For performance, we have a few ways for heat capacity to get modified without a full recalculation.
        // To avoid these drifting too much due to float error, we mark it as dirty after N such operations,
        // so it will be recalculated.
        if (++_heatCapacityUpdateCounter >= HeatCapacityUpdateInterval)
            _heatCapacityDirty = true;
    }

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
    public Solution([ForbidLiteral] string prototype, FixedPoint2 quantity, List<ReagentData>? data = null) : this(1)
    {
        AddReagent(new ReagentId(prototype, data), quantity);
    }

    public Solution(IEnumerable<ReagentQuantity> reagents, bool setMaxVol = true)
    {
        Contents = new(reagents);
        Volume = FixedPoint2.Zero;
        foreach (var reagent in Contents)
        {
            Volume += reagent.Quantity;
        }

        if (setMaxVol)
            MaxVolume = Volume;

        ValidateSolution();
    }

    public Solution(Solution solution)
    {
        Contents = new(solution.Contents.Count);
        foreach (var item in solution.Contents)
        {
            Contents.Add(item.Clone());
        }

        Volume = solution.Volume;
        MaxVolume = solution.MaxVolume;
        _heat = solution._heat;
        CanReact = solution.CanReact;
        _heatCapacityDirty = solution._heatCapacityDirty;
        _heatCapacityUpdateCounter = solution._heatCapacityUpdateCounter;
        ValidateSolution();
    }

    public Solution Clone()
    {
        return new Solution(this);
    }

    /// <summary>
    /// Destroys this solution and all its contents.
    /// </summary>
    private void Destroy()
    {
        this = new Solution();
    }
}

