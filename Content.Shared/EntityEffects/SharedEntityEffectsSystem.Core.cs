using Content.Shared.Database;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects;

/// <summary>
/// A type of <see cref="EntityEffectSystem{TEffect}"/> which comes with a baked in EntityQuery...
/// </summary>
/// <typeparam name="TComp">A component which we may want.</typeparam>
/// <typeparam name="TEffect">The Entity Effect itself</typeparam>
public abstract partial class EntityEffectSystem<TComp, TEffect> : BaseEntityEffectSystem where TComp : IComponent where TEffect : EntityEffect
{
    EntityQuery<TComp> _compQuery;

    public override void Initialize()
    {
        base.Initialize();

        _compQuery = GetEntityQuery<TComp>();
    }

    public override void ApplyEffect<T>(EntityUid target, T effect, float scale)
    {
        if (effect is not TEffect eff || !_compQuery.TryComp(target, out var comp))
            return;

        var ev = new EntityEffectEvent<TEffect>(eff, scale);
        Effect((target, comp), ref ev);

        //Execute(target, eff, scale);
    }

    protected abstract void Effect(Entity<TComp> entity, ref EntityEffectEvent<TEffect> ev);

    //public abstract void Execute(EntityUid target, TEffect effect, float scale);
}

/// <summary>
/// This is a basic abstract entity effect containing all the data an entity effect needs to affect entities with effects...
/// </summary>
/// <typeparam name="TEffect">The Entity Effect itself</typeparam>
public abstract partial class EntityEffectSystem<TEffect> : EntitySystem, IEntityEffectApplier where TEffect : EntityEffect
{
    public void ApplyEffect<T>(EntityUid target, T effect, float scale) where T : EntityEffect
    {
        if (effect is not TEffect eff)
            return;

        var ev = new EntityEffectEvent<TEffect>(eff, scale);
        Effect(target, ref ev);

        //Execute(target, eff, scale);
    }

    protected abstract void Effect(EntityUid uid, ref EntityEffectEvent<TEffect> ev);

    //public abstract void Execute(EntityUid target, TEffect effect, float scale);
}

public abstract partial class BaseEntityEffectSystem : EntitySystem, IEntityEffectApplier
{
    // TODO: Add event subscription/unsubscription logic here...
    public abstract void ApplyEffect<T>(EntityUid target, T effect, float scale) where T : EntityEffect;
}

/// <summary>
/// Used to raise an EntityEffect without losing the type of effect.
/// </summary>
public interface IEntityEffectApplier
{
    void ApplyEffect<T>(EntityUid target, T effect, float scale) where T : EntityEffect;
}

/// <summary>
/// A basic instantaneous effect which can be applied to an entity via events.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class EntityEffect
{
    [DataField]
    public EntityCondition[]? Conditions;

    /// <summary>
    /// If our scale is less than this value, the effect fails.
    /// </summary>
    [DataField]
    public virtual float MinScale { get; private set; }

    /// <summary>
    /// If true, then it allows the scale multiplier to go above 1.
    /// </summary>
    [DataField]
    public virtual bool Scaling { get; private set; }

    // TODO: This should be an entity condition but guidebook relies on it heavily for formatting...
    /// <summary>
    /// Probability of the effect occuring.
    /// </summary>
    [DataField]
    public float Probability = 1.0f;

    /// <summary>
    /// The description of this entity effect that shows in guidebooks.
    /// </summary>
    public virtual string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    /// <summary>
    /// Whether this effect should be logged in admin logs.
    /// </summary>
    [ViewVariables]
    public virtual bool ShouldLog => true;

    /// <summary>
    /// If this effect is logged, how important is the log?
    /// </summary>
    [ViewVariables]
    public virtual LogImpact LogImpact => LogImpact.Low;
}

/// <summary>
/// An Event carrying an entity effect. Useful for if we need to apply an effect to multiple components!
/// </summary>
/// <param name="Effect">The Effect</param>
/// <param name="Scale">A strength scalar for the effect, defaults to 1 and typically only goes under for incomplete reactions.</param>
[ByRefEvent, Access(typeof(SharedEntityEffectsSystem))]
public readonly record struct EntityEffectEvent<T>(T Effect, float Scale) where T : EntityEffect
{
    /// <summary>
    /// The Condition being raised in this event
    /// </summary>
    public readonly T Effect = Effect;

    /// <summary>
    /// The Scale modifier of this Effect.
    /// </summary>
    public readonly float Scale = Scale;
}
