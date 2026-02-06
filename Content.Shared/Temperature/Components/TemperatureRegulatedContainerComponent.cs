using Content.Shared.Atmos;
using Robust.Shared.Containers;

namespace Content.Shared.Temperature.Components;

/// <summary>
/// This component attempts to bring entities within it to a specified temperature.
/// </summary>
[RegisterComponent]
public sealed partial class TemperatureRegulatedContainerComponent : Component
{
    /// <summary>
    /// Temperature this container is at, and all entities inside will try to reach.
    /// </summary>
    [DataField]
    public float TargetTemperature = Atmospherics.T20C;

    /// <summary>
    /// Conductivity modifiers for heat exchanges.
    /// </summary>
    [DataField]
    public float Conductance = 5f;

    /// <summary>
    /// Name of the container we're cooling.
    /// </summary>
    [DataField(required: true)]
    public string ContainerId;

    /// <summary>
    /// Cache of the container we're cooling.
    /// </summary>
    [ViewVariables]
    public BaseContainer? Container;
}
