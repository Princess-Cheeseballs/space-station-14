using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;

namespace Content.Shared.EntityEffects.NewEffects;

/// <summary>
/// This is used for...
/// </summary>
public sealed partial class ExtinguishEntityEffectsSystem : SharedEntityEffectsSystem<FlammableComponent, ExtinguishEffectArgs>
{
    protected override void Effect(Entity<FlammableComponent> entity, ref EntityEffectEvent<ExtinguishEffectArgs> effectEvent)
    {
        var ev = new ExtinguishEvent
        {
            FireStacksAdjustment = -1.5f,
        };

        RaiseLocalEvent(entity, ref ev);
    }
}

public sealed partial class ExtinguishEffectArgs : EntityEffectBase
{
    /// <summary>
    ///     Amount of firestacks reduced.
    /// </summary>
    [DataField]
    public float FireStacksAdjustment = -1.5f;
}
