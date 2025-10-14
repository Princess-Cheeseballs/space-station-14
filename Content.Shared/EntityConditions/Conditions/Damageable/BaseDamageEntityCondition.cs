using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityConditions.Conditions.Damageable;

/// <summary>
/// A type of <see cref="EntityEffectBase{T}"/> for effects that spawn entities by prototype.
/// </summary>
/// <typeparam name="T">The entity effect inheriting this BaseEffect</typeparam>
/// <inheritdoc cref="EntityEffect"/>
public abstract partial class BaseSpawnEntityEntityEffect<T> : EntityConditionBase<T> where T : BaseSpawnEntityEntityEffect<T>
{
    [DataField(required:true)]
    public DamageRange[] Type;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        {
            var typeList = new List<string>();

            foreach (var type in Type)
            {
                if (!prototype.Resolve(type.DamageType, out var proto))
                    continue;

                typeList.Add(proto.LocalizedName);
            }

            var names = ContentLocalizationManager.FormatListToOr(typeList);

            // TODO: This shit
            return Loc.GetString("reagent-effect-condition-guidebook-type-damage",
                ("name", names),
                ("shouldhave", !Inverted));
        }
    }
        /*Loc.GetString("reagent-effect-condition-guidebook-type-damage",
            ("max", Max == FixedPoint2.MaxValue ? int.MaxValue : Max.Float()),
            ("min", Min.Float()),
            ("type", prototype.Index(DamageType).LocalizedName));*/
}

public record struct DamageRange()
{
    [DataField]
    public FixedPoint2 Max = FixedPoint2.MaxValue;

    [DataField]
    public FixedPoint2 Min = FixedPoint2.Zero;

    [DataField(required: true)]
    public ProtoId<DamageTypePrototype> DamageType;
}
