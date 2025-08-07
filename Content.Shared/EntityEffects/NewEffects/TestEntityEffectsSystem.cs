using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;

namespace Content.Shared.EntityEffects.NewEffects;

/// <summary>
/// This is used for...
/// </summary>
public sealed partial class TestEntityEffectsSystem : EntityEffectsSystem<FlammableComponent, TestEffectArgs>
{
    protected override void Effect(Entity<FlammableComponent> entity, ref EntityEffectEvent<TestEffectArgs> effectEvent)
    {
        var ev = new ExtinguishEvent
        {
            FireStacksAdjustment = effectEvent.Effect.FireStacksAdjustment,
        };

        RaiseLocalEvent(entity, ref ev);
    }
}

public sealed partial class TestEffectArgs : EntityEffectBase<TestEffectArgs>
{
    /// <summary>
    ///     Amount of firestacks reduced.
    /// </summary>
    [DataField]
    public float FireStacksAdjustment = -1.5f;
}
