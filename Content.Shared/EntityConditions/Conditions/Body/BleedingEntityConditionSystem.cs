using Content.Shared.Body.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Body;
///<summary>
/// Returns true if this entity's bleed is within a specified minimum and maximum value.
/// </summary>
/// <inheritdoc cref="EntityConditionSystem{T, TCondition}"/>
public sealed partial class BleedingEntityConditionSystem : EntityConditionSystem<BloodstreamComponent, BleedingCondition>
{
    protected override void Condition(Entity<BloodstreamComponent> entity, ref EntityConditionEvent<BleedingCondition> args)
    {
        args.Result = args.Condition.Min <= entity.Comp.BleedAmount && entity.Comp.BleedAmount <= args.Condition.Max;
    }
}

/// <inheritdoc cref="EntityCondition"/>
public sealed partial class BleedingCondition : EntityConditionBase<BleedingCondition>
{
    [DataField]
    public float Min;

    [DataField]
    public float Max = float.PositiveInfinity;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => String.Empty;
}
