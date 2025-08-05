using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Standing;

namespace Content.Shared.EntityEffects.NewEffects;

/// <summary>
/// This is used for...
/// </summary>
public sealed partial class ExtinguishEntityEffectsSystem : EntityEffectsSystem<FlammableComponent, ExtinguishEffectArgs>
{
    protected override void Effect(Entity<FlammableComponent> entity, ref EntityEffectEvent<ExtinguishEffectArgs> effectEvent)
    {
        var ev = new ExtinguishEvent
        {
            FireStacksAdjustment = effectEvent.Effect.FireStacksAdjustment,
        };

        RaiseLocalEvent(entity, ref ev);
    }
}

public sealed partial class ExtinguishEntityEffectsSystem2 : EntityEffectsSystem<StandingStateComponent, ExtinguishEffectArgs>
{
    protected override void Effect(Entity<StandingStateComponent> entity,
        ref EntityEffectEvent<ExtinguishEffectArgs> effectEvent)
    {

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
