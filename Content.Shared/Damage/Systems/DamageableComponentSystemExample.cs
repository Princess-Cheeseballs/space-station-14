using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radiation.Events;
using Content.Shared.Rejuvenate;

namespace Content.Shared.Damage.Systems;

/// <summary>
/// This handles...
/// </summary>
public sealed class DamageableComponentSystemExample : AbstractDamageSystem<DamageableComponent>
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    protected override void OnRejuvenate(Entity<DamageableComponent> ent, ref RejuvenateEvent args)
    {
        // Do this so that the state changes when we set the damage
        _mobThreshold.SetAllowRevives(ent, true);
        Damage.ClearAllDamage(ent.AsNullable());
        _mobThreshold.SetAllowRevives(ent, false);
    }

    protected override void OnDamageDealt(Entity<DamageableComponent> ent, ref DamageDealtEvent args)
    {
        if (!DamageQuery.TryGetComponent(ent, out var damageable))
            return;

        var damageDone = new DamageSpecifier();

        damageDone.DamageDict.EnsureCapacity(args.Damage.DamageDict.Count);

        var dict = damageable.Damage.DamageDict;
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (!Damage.SupportsType(ent.Comp.DamageContainerID, type))
                continue;

            var oldValue = dict.GetValueOrDefault(type);
            var newValue = FixedPoint2.Max(FixedPoint2.Zero, oldValue + value);
            if (newValue == oldValue)
                continue;

            dict[type] = newValue;
            damageDone.DamageDict[type] = newValue - oldValue;
        }

        args.Handled |= !damageDone.Empty;
    }

    protected override void OnIrradiated(Entity<DamageableComponent> ent, ref OnIrradiatedEvent args)
    {
        var damageValue = FixedPoint2.New(args.TotalRads);

        // Radiation should really just be a damage group instead of a list of types.
        DamageSpecifier damage = new();
        foreach (var typeId in ent.Comp.RadiationDamageTypeIDs)
        {
            damage.DamageDict.Add(typeId, damageValue);
        }

        Damage.ChangeDamage(ent.Owner, damage, interruptsDoAfters: false, origin: args.Origin);
    }
}
