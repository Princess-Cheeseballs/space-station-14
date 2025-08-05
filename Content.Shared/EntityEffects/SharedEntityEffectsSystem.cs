using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reaction;

namespace Content.Shared.EntityEffects;

/// <summary>
/// This handles Entity Effects, except they're in shared and predicted...
/// </summary>
public abstract partial class SharedEntityEffectsSystem<T, TEffect> : EntitySystem where T : Component where TEffect : EntityEffectBase
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        // Generic
        SubscribeLocalEvent<T, EntityEffectEvent<TEffect>>(Effect);

        // Relays ???
        SubscribeLocalEvent<ReactiveComponent, ReactionEntityEvent>(OnReactive);
    }

    protected abstract void Effect(Entity<T> entity, ref EntityEffectEvent<TEffect> args);

    private void OnReactive(Entity<ReactiveComponent> entity, ref ReactionEntityEvent args)
    {
        if (args.Reagent.NewReactiveEffects != null && entity.Comp.ReactiveGroups != null)
        {
            foreach (var (key, val) in args.Reagent.NewReactiveEffects)
            {
                if (!val.Methods.Contains(args.Method))
                    continue;

                if (!entity.Comp.ReactiveGroups.ContainsKey(key))
                    continue;

                if (!entity.Comp.ReactiveGroups[key].Contains(args.Method))
                    continue;

                foreach (var effect in val.Effects)
                {
                    RaiseEffectEvent(entity, effect);
                }
            }
        }
    }

    protected void RaiseEffectEvent(EntityUid target, EntityEffectBase effect)
    {
        var effectEv = new EntityEffectEvent<TEffect>(effect);
        RaiseLocalEvent(target, ref effectEv);
    }
}

[ByRefEvent]
public readonly record struct EntityEffectEvent<TEffect>(EntityEffectBase Effect) where TEffect : EntityEffectBase;

public abstract partial class EntityEffectBase;
