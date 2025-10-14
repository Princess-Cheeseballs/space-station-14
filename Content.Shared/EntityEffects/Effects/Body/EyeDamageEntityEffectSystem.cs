using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Body;

/// <summary>
/// Modifies eye damage by a given amount, modified by scale, floored to an integer.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class EyeDamageEntityEffectSystem : EntityEffectSystem<BlindableComponent, EyeDamage>
{
    [Dependency] private readonly BlindableSystem _blindable = default!;

    protected override void Effect(Entity<BlindableComponent> entity, ref EntityEffectEvent<EyeDamage> args)
    {
        var amount = (int) Math.Floor(args.Effect.Amount * args.Scale);
        _blindable.AdjustEyeDamage(entity.Owner, amount);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class EyeDamage : EntityEffect
{
    /// <summary>
    /// The amount of eye damage we're adding or removing
    /// </summary>
    [DataField]
    public int Amount = -1;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-eye-damage", ("chance", Probability), ("deltasign", MathF.Sign(Amount)));
}
