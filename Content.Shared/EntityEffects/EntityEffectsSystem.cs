using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reaction;

namespace Content.Shared.EntityEffects;

/// <summary>
/// This handles Entity Effects, except they're in shared and predicted...
/// </summary>
public abstract partial class EntityEffectSystem<T, TEffect> : EntitySystem where T : Component where TEffect : EntityEffectBase<TEffect>
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<T, EntityEffectEvent<TEffect>>(Effect);
    }
    protected abstract void Effect(Entity<T> entity, ref EntityEffectEvent<TEffect> args);
}

public sealed partial class SharedEntityEffectsSystem : EntitySystem, IEntityEffectRaiser
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
                    effect.RaiseEvent(entity, this);
                }
            }
        }
    }

    public void RaiseEffectEvent<T>(EntityUid target, T effect) where T : EntityEffectBase<T>
    {
        var effectEv = new EntityEffectEvent<T>(effect);
        RaiseLocalEvent(target, ref effectEv);
    }
}

public interface IEntityEffectRaiser
{
    void RaiseEffectEvent<T>(EntityUid target, T effect) where T : EntityEffectBase<T>;
}

[ByRefEvent]
public readonly record struct EntityEffectEvent<T>(T Effect) where T : EntityEffectBase<T>;

public abstract partial class EntityEffectBase<T> : AnyEntityEffect where T : EntityEffectBase<T>
{
    public override void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser)
    {
        if (this is not T type)
            return;

        raiser.RaiseEffectEvent(target, type);
    }
}

// This exists so we can store entity effects in list and raise events without type erasure.
public abstract partial class AnyEntityEffect
{
    public abstract void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser);
}
