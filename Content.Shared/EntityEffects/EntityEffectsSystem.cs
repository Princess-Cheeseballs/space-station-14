using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reaction;

namespace Content.Shared.EntityEffects;

/// <summary>
/// This handles Entity Effects, except they're in shared and predicted...
/// </summary>
public abstract partial class EntityEffectsSystem<T, TEffect> : EntitySystem where T : Component where TEffect : EntityEffectBase
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        // Generic
        SubscribeLocalEvent<T, EntityEffectEvent<TEffect>>(Effect);

        // Relays ???
        //SubscribeLocalEvent<ReactiveComponent, ReactionEntityEvent>(OnReactive);
    }

    protected abstract void Effect(Entity<T> entity, ref EntityEffectEvent<TEffect> args);
}

public sealed partial class EntityEffectsSystem2 : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ReactiveComponent, ReactionEntityEvent>(OnReactive);
    }

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

    private void RaiseEffectEvent<T>(EntityUid target, T effect) where T : EntityEffectBase
    {
        var effectEv = new EntityEffectEvent<T>(effect);
        RaiseLocalEvent(target, ref effectEv);
    }
}


[ByRefEvent]
public readonly record struct EntityEffectEvent<T>(T Effect) where T : EntityEffectBase;

public abstract partial class EntityEffectBase;
