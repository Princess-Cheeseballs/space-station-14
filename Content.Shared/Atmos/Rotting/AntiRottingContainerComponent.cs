namespace Content.Shared.Atmos.Rotting;

/// <summary>
/// Entities inside this container will not rot.
/// </summary>
[RegisterComponent]
public sealed partial class AntiRottingContainerComponent : Component
{
    /// <summary>
    /// Does this component require power to function.
    /// </summary>
    [DataField]
    public bool RequiresPower = true;

    /// <summary>
    /// Whether this component is active or not.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = true;
}

