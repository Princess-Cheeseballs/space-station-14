using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Database;
using Content.Shared.EntityConditions;
using Content.Shared.Random.Helpers;
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

        // TODO: Apply the effect through delegates&callbacks or events or something
    }
}
