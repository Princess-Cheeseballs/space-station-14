using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Damageable;

/// <summary>
/// Returns true if this entity can take damage and if any of the specified damage types are within a specified min and max range.
/// </summary>
/// <inheritdoc cref="EntityConditionSystem{T, TCondition}"/>
public sealed partial class DamageTypesEntityConditionSystem : EntityConditionSystem<DamageableComponent, DamageTypesCondition>
{
    protected override void Condition(Entity<DamageableComponent> entity, ref EntityConditionEvent<DamageTypesCondition> args)
    {
        foreach (var range in args.Condition.DamageTypes)
        {
            var value = entity.Comp.Damage.DamageDict.GetValueOrDefault(range.DamageType);
            if (value < range.Min || value > range.Max)
                continue;

            args.Result = true;
            return;
        }
    }
}

/// <inheritdoc cref="EntityCondition"/>
public sealed partial class DamageTypesCondition : EntityConditionBase<DamageTypesCondition>
{
    [DataField(required:true)]
    public DamageRange[] DamageTypes;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
        /*Loc.GetString("reagent-effect-condition-guidebook-type-damage",
            ("max", Max == FixedPoint2.MaxValue ? int.MaxValue : Max.Float()),
            ("min", Min.Float()),
            ("type", prototype.Index(DamageType).LocalizedName));*/
}
