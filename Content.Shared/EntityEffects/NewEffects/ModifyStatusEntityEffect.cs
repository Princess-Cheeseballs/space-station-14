using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.NewEffects;

/// <summary>
/// Changes status effects on entities: Adds, removes or sets time.
/// </summary>
public sealed partial class ModifyStatusEntityEffect : EntityEffectSystem<StatusEffectContainerComponent, ModifyStatusEffectArgs>
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    /// <inheritdoc />
    protected override void Effect(Entity<StatusEffectContainerComponent> entity, ref EntityEffectEvent<ModifyStatusEffectArgs> effectEvent)
    {
        var time = effectEvent.Effect.Time;

        var duration = TimeSpan.FromSeconds((double)time);
        switch (effectEvent.Effect.Type)
        {
            case StatusEffectMetabolismType.Add:
                if (effectEvent.Effect.Refresh)
                    _status.TryUpdateStatusEffectDuration(entity, effectEvent.Effect.EffectProto, duration);
                else
                    _status.TryAddStatusEffectDuration(entity, effectEvent.Effect.EffectProto, duration);
                break;
            case StatusEffectMetabolismType.Remove:
                _status.TryAddTime(entity, effectEvent.Effect.EffectProto, -duration);
                break;
            case StatusEffectMetabolismType.Set:
                _status.TrySetStatusEffectDuration(entity, effectEvent.Effect.EffectProto, duration);
                break;
        }
    }

    // TODO: REAGENTEFFECTGUIDEBOOKTEXT FUCK!!!
    /*
    /// <inheritdoc />
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString(
            "reagent-effect-guidebook-status-effect",
            ("chance", Probability),
            ("type", Type),
            ("time", Time),
            ("key", prototype.Index(EffectProto).Name)
        );
        */
}

public sealed class ModifyStatusEffectArgs : EntityEffectBase<ModifyStatusEffectArgs>
{
    [DataField(required: true)]
    public EntProtoId EffectProto;

    /// <summary>
    /// Time for which status effect should be applied. Behaviour changes according to <see cref="Refresh" />.
    /// </summary>
    [DataField]
    public float Time = 2.0f;

    /// <remarks>
    /// true - refresh status effect time (update to greater value), false - accumulate status effect time.
    /// </remarks>
    [DataField]
    public bool Refresh = true;

    /// <summary>
    /// Should this effect add the status effect, remove time from it, or set its cooldown?
    /// </summary>
    [DataField]
    public StatusEffectMetabolismType Type = StatusEffectMetabolismType.Add;
}
