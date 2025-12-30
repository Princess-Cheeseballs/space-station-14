using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components;

/// <summary>
/// This is used to mark and track all solutions on this entity
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SolutionManagerComponent : Component
{
    /// <summary>
    /// The name of the container which we store our solutions in.
    /// </summary>
    /// TODO: Maybe not have this be a DataField because fuck you???
    [DataField, AutoNetworkedField]
    public string ContainerId = "solutions";

    /// <summary>
    /// A reference to the container our solutions are stored in for easier access.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Container? Container;

    /// <summary>
    /// Dictionary of every single solution entity and their string identifier.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, Entity<SolutionComponent>> Solutions = new();
}
