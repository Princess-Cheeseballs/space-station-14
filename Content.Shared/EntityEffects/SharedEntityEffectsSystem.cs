using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Database;
using Content.Shared.EntityConditions;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.EntityEffects;

/// <summary>
/// This handles entity effects.
/// Specifically it handles the receiving of events for causing entity effects, and provides
/// public API for other systems to take advantage of entity effects.
/// </summary>
public sealed partial class SharedEntityEffectsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IEntityEffectApplier _applier = default!;
    [Dependency] private readonly SharedEntityConditionsSystem _condition = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ReactiveComponent, ReactionEntityEvent>(OnReactive);
    }

    private void OnReactive(Entity<ReactiveComponent> entity, ref ReactionEntityEvent args)
    {
        if (args.Reagent.ReactiveEffects == null || entity.Comp.ReactiveGroups == null)
            return;

        var scale = args.ReagentQuantity.Quantity.Float();

        foreach (var (key, val) in args.Reagent.ReactiveEffects)
        {
            if (!val.Methods.Contains(args.Method))
                continue;

            if (!entity.Comp.ReactiveGroups.TryGetValue(key, out var group))
                continue;

            if (!group.Contains(args.Method))
                continue;

            ApplyEffects(entity, val.Effects, scale);
        }

        if (entity.Comp.Reactions == null)
            return;

        foreach (var entry in entity.Comp.Reactions)
        {
            if (!entry.Methods.Contains(args.Method))
                continue;

            if (entry.Reagents == null || entry.Reagents.Contains(args.Reagent.ID))
                ApplyEffects(entity, entry.Effects, scale);
        }
    }

    /// <summary>
    /// Applies a list of entity effects to a target entity.
    /// </summary>
    /// <param name="target">Entity being targeted by the effects</param>
    /// <param name="effects">Effects we're applying to the entity</param>
    /// <param name="scale">Optional scale multiplier for the effects</param>
    public void ApplyEffects(EntityUid target, EntityEffect[] effects, float scale = 1f)
    {
        // do all effects, if conditions apply
        foreach (var effect in effects)
        {
            TryApplyEffect(target, effect, scale);
        }
    }

    /// <summary>
    /// Applies an entity effect to a target if all conditions pass.
    /// </summary>
    /// <param name="target">Target we're applying an effect to</param>
    /// <param name="effect">Effect we're applying</param>
    /// <param name="scale">Optional scale multiplier for the effect. Not all </param>
    /// <returns>True if all conditions pass!</returns>
    public bool TryApplyEffect(EntityUid target, EntityEffect effect, float scale = 1f)
    {
        if (scale < effect.MinScale)
            return false;

        // TODO: Replace with proper random prediciton when it exists.
        if (effect.Probability <= 1f)
        {
            var seed = SharedRandomExtensions.HashCodeCombine(new() { (int)_timing.CurTick.Value, GetNetEntity(target).Id, 0 });
            var rand = new System.Random(seed);
            if (!rand.Prob(effect.Probability))
                return false;
        }

        // See if conditions apply
        if (!_condition.TryConditions(target, effect.Conditions))
            return false;

        ApplyEffect(target, effect, scale);
        return true;
    }

    /// <summary>
    /// Applies an <see cref="EntityEffect"/> to a given target.
    /// This doesn't check conditions so you should only call this if you know what you're doing!
    /// </summary>
    /// <param name="target">Target we're applying an effect to</param>
    /// <param name="effect">Effect we're applying</param>
    /// <param name="scale">Optional scale multiplier for the effect. Not all </param>
    public void ApplyEffect(EntityUid target, EntityEffect effect, float scale = 1f)
    {
        // Clamp the scale if the effect doesn't allow scaling.
        if (!effect.Scaling)
            scale = Math.Min(scale, 1f);

        if (effect.ShouldLog)
        {
            _adminLog.Add(
                LogType.EntityEffect,
                effect.LogImpact,
                $"Entity effect {effect.GetType().Name:effect}"
                + $" applied on entity {target:entity}"
                + $" at {Transform(target).Coordinates:coordinates}"
                + $" with a scale multiplier of {scale}"
            );
        }

        _applier.ApplyEffect(target, effect, scale);
    }
}

/// <summary>
/// A type of <see cref="EntityEffectSystem{TEffect}"/> which comes with a baked in EntityQuery...
/// </summary>
/// <typeparam name="TComp">A component which we may want.</typeparam>
/// <typeparam name="TEffect">The Entity Effect itself</typeparam>
public abstract partial class EntityEffectSystem<TComp, TEffect> : EntitySystem, IEntityEffectApplier where TComp : IComponent where TEffect : EntityEffect
{
    EntityQuery<TComp> _compQuery;

    public override void Initialize()
    {
        base.Initialize();

        _compQuery = GetEntityQuery<TComp>();
    }

    public void ApplyEffect<T>(EntityUid target, T effect, float scale) where T : EntityEffect
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

/*/// <summary>
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
}*/

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
