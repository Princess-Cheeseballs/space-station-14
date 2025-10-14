using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Body;
///<summary>
/// Returns true if this entity's blood level is within a specified minimum and maximum percentage of their total blood level.
/// </summary>
/// <inheritdoc cref="EntityConditionSystem{T, TCondition}"/>
public sealed partial class BloodLevelEntityConditionSystem : EntityConditionSystem<BloodstreamComponent, BloodLevelCondition>
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;

    protected override void Condition(Entity<BloodstreamComponent> entity, ref EntityConditionEvent<BloodLevelCondition> args)
    {
        var bloodLevel = _bloodstream.GetBloodLevelPercentage(entity.AsNullable());

        args.Result = args.Condition.Min  <= bloodLevel && bloodLevel <= args.Condition.Max ;
    }
}

/// <inheritdoc cref="EntityCondition"/>
public sealed partial class BloodLevelCondition : EntityConditionBase<BloodLevelCondition>
{
    [DataField]
    public float Min;

    [DataField]
    public float Max = float.PositiveInfinity;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => String.Empty;
}
