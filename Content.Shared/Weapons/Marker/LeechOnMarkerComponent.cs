using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Marker;

/// <summary>
/// Applies leech upon hitting a damage marker target.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LeechOnMarkerComponent : Component
{
    /// <summary>
    /// What kind of damage we heal from marking a target.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<DamageGroupPrototype> Leech;

    /// <summary>
    /// What percentage of the damage we dealt do we heal back?
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public float Modifier;
}
