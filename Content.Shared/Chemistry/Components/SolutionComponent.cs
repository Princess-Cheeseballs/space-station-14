using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.FixedPoint;
using Content.Shared.Materials;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components;

/// <summary>
/// <para>Holds the composition of an entity made from reagents and its reagent temperature.</para>
/// <para>If the entity is used to represent a collection of reagents inside of a container such as a beaker, syringe, bloodstream, food, or similar the entity is tracked by a <see cref="SolutionContainerManagerComponent"/> on the container and has a <see cref="ContainedSolutionComponent"/> tracking which container it's in.</para>
/// </summary>
/// <remarks>
/// <para>Once reagents and materials have been merged this component should be depricated in favor of using a combination of <see cref="PhysicalCompositionComponent"/> and <see cref="TemperatureComponent"/>. May require minor reworks to both.</para>
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SolutionComponent : Component
{
    /// <summary>
    /// The reagents the entity is composed of and their temperature.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Solution Solution = new();

    /// <summary>
    /// The unique identifier of this solution entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Id = "solution";

    // TODO: Separate this from Solutions and make it its own datafield!
    //[DataField, AutoNetworkedField]
    public FixedPoint2 MaxVolume => Solution.MaxVolume;

    /// <summary>
    /// Helper so we don't have to keep typing "entity.Comp.Solution.Volume"
    /// </summary>
    public FixedPoint2 Volume => Solution.Volume;
}
