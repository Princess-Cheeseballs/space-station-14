using Content.Shared.Damage.Components;
using Content.Shared.Inventory;
using Content.Shared.Radiation.Events;
using Content.Shared.Rejuvenate;
using Robust.Shared.Prototypes;

namespace Content.Shared.Damage.Systems;

public abstract partial class AbstractDamageSystem<T> : EntitySystem where T : DamageTakerComponent
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly DamageableSystem Damage = default!;
    [Dependency] protected readonly EntityQuery<T> DamageQuery = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<T, ComponentInit>(DamageableInit);
        SubscribeLocalEvent<T, OnIrradiatedEvent>(OnIrradiated);
        SubscribeLocalEvent<T, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<T, DamageDealtEvent>(OnDamageDealt);
    }

    /// <summary>
    ///     Initialize a damageable component
    /// </summary>
    private void DamageableInit(Entity<T> ent, ref ComponentInit _)
    {
        ent.Comp.Damage.GetDamagePerGroup(Proto, ent.Comp.DamagePerGroup);
        ent.Comp.TotalDamage = ent.Comp.Damage.GetTotal();
    }

    protected abstract void OnIrradiated(Entity<T> ent, ref OnIrradiatedEvent args);

    protected abstract void OnRejuvenate(Entity<T> ent, ref RejuvenateEvent args);

    protected abstract void OnDamageDealt(Entity<T> ent, ref DamageDealtEvent args);
}

/// <summary>
///     Raised before damage is done, so stuff can cancel it if necessary.
/// </summary>
[ByRefEvent]
public record struct BeforeDamageChangedEvent(DamageSpecifier Damage, EntityUid? Origin = null, bool Cancelled = false);

/// <summary>
///     Raised on an entity when damage is about to be dealt,
///     in case anything else needs to modify it other than the base
///     damageable component.
///
///     For example, armor.
/// </summary>
public sealed class DamageModifyEvent(DamageSpecifier damage, EntityUid? origin = null)
    : EntityEventArgs, IInventoryRelayEvent
{
    /// <inheritdoc/>
    /// <remarks>
    ///     Whenever locational damage is a thing, this should just check only that bit of armor.
    /// </remarks>
    public SlotFlags TargetSlots => ~SlotFlags.POCKET;

    /// <summary>
    ///     Contains the original damage, prior to any modifers.
    /// </summary>
    public readonly DamageSpecifier OriginalDamage = damage;

    /// <summary>
    ///     Contains the damage after modifiers have been applied.
    ///     This is the damage that will be inflicted.
    /// </summary>
    public DamageSpecifier Damage = damage;

    /// <summary>
    ///     Contains the entity which caused the damage, if any was responsible.
    /// </summary>
    public readonly EntityUid? Origin = origin;
}

/// <summary>
/// Event raised when an entity with <see cref="DamageableComponent" /> has taken some amount of damage.
/// </summary>
/// <param name="Damage">The amount of damage the entity is being subject to.</param>
/// <param name="Origin">The originator of the damage</param>
/// <param name="InterruptsDoAfters">If the damage being dealt will interrupt do-afters</param>
[ByRefEvent]
public record struct DamageDealtEvent(DamageSpecifier Damage, EntityUid? Origin, bool InterruptsDoAfters)
{
    public readonly DamageSpecifier Damage = Damage;
    public readonly EntityUid? Origin = Origin;
    public readonly bool InterruptsDoAfters = InterruptsDoAfters;
    public bool Handled = false;
}

[Obsolete("Will be replaced with damage-model specific events; general 'took damage' can be served by DamageDealtEvent")]
public sealed class DamageChangedEvent : EntityEventArgs
{
    /// <summary>
    ///     This is the component whose damage was changed.
    /// </summary>
    /// <remarks>
    ///     Given that nearly every component that cares about a change in the damage, needs to know the
    ///     current damage values, directly passing this information prevents a lot of duplicate
    ///     Owner.TryGetComponent() calls.
    /// </remarks>
    public readonly DamageableComponent Damageable;

    /// <summary>
    ///     The amount by which the damage has changed. If the damage was set directly to some number, this will be
    ///     null.
    /// </summary>
    public readonly DamageSpecifier? DamageDelta;

    /// <summary>
    ///     Was any of the damage change dealing damage, or was it all healing?
    /// </summary>
    public readonly bool DamageIncreased;

    /// <summary>
    ///     Does this event interrupt DoAfters?
    ///     Note: As provided in the constructor, this *does not* account for DamageIncreased.
    ///     As written into the event, this *does* account for DamageIncreased.
    /// </summary>
    public readonly bool InterruptsDoAfters;

    /// <summary>
    ///     Contains the entity which caused the change in damage, if any was responsible.
    /// </summary>
    public readonly EntityUid? Origin;

    public DamageChangedEvent(
        DamageableComponent damageable,
        DamageSpecifier? damageDelta,
        bool interruptsDoAfters,
        EntityUid? origin
    )
    {
        Damageable = damageable;
        DamageDelta = damageDelta;
        Origin = origin;

        if (DamageDelta is null)
            return;

        foreach (var damageChange in DamageDelta.DamageDict.Values)
        {
            if (damageChange <= 0)
                continue;

            DamageIncreased = true;

            break;
        }

        InterruptsDoAfters = interruptsDoAfters && DamageIncreased;
    }
}
