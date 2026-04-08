using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem
{
    public override void Initialize()
    {
        RebuildContainerCache();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<DamageableComponent, ComponentHandleState>(DamageableHandleState);
        SubscribeLocalEvent<DamageableComponent, ComponentGetState>(DamageableGetState);

        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();

        // Damage modifier CVars are updated and stored here to be queried in other systems.
        // Note that certain modifiers requires reloading the guidebook.
        Subs.CVar(
            _config,
            CCVars.PlaytestAllDamageModifier,
            value =>
            {
                UniversalAllDamageModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
                _explosion.ReloadMap();
            },
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestAllHealModifier,
            value =>
            {
                UniversalAllHealModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            },
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestProjectileDamageModifier,
            value => UniversalProjectileDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestMeleeDamageModifier,
            value => UniversalMeleeDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestProjectileDamageModifier,
            value => UniversalProjectileDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestHitscanDamageModifier,
            value => UniversalHitscanDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestReagentDamageModifier,
            value =>
            {
                UniversalReagentDamageModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            },
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestReagentHealModifier,
            value =>
            {
                UniversalReagentHealModifier = value;
                _chemistryGuideData.ReloadAllReagentPrototypes();
            },
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestExplosionDamageModifier,
            value =>
            {
                UniversalExplosionDamageModifier = value;
                _explosion.ReloadMap();
            },
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestThrownDamageModifier,
            value => UniversalThrownDamageModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestTopicalsHealModifier,
            value => UniversalTopicalsHealModifier = value,
            true
        );
        Subs.CVar(
            _config,
            CCVars.PlaytestMobDamageModifier,
            value => UniversalMobDamageModifier = value,
            true
        );
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (!ev.WasModified<DamageContainerPrototype>() && !ev.WasModified<DamageGroupPrototype>())
            return;

        RebuildContainerCache();
    }

    private void RebuildContainerCache()
    {
        _supportedTypesByContainer.Clear();

        foreach (var proto in _prototypeManager.EnumeratePrototypes<DamageContainerPrototype>())
        {
            var set = new HashSet<ProtoId<DamageTypePrototype>>();
            _supportedTypesByContainer[proto.ID] = set;

            foreach (var type in proto.SupportedTypes)
            {
                set.Add(type);
            }

            foreach (var groupId in proto.SupportedGroups)
            {
                var group = _prototypeManager.Index(groupId);
                foreach (var type in group.DamageTypes)
                {
                    set.Add(type);
                }
            }
        }
    }

    private void DamageableGetState(Entity<DamageableComponent> ent, ref ComponentGetState args)
    {
        args.State = new DamageableComponentState(
            _netMan.IsServer ? ent.Comp.Damage : ent.Comp.Damage.Clone(),
            ent.Comp.DamageContainerID,
            ent.Comp.DamageModifierSetId,
            ent.Comp.HealthBarThreshold
        );
    }

    private void DamageableHandleState(Entity<DamageableComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not DamageableComponentState state)
            return;

        ent.Comp.DamageContainerID = state.DamageContainerId;
        ent.Comp.DamageModifierSetId = state.ModifierSetId;
        ent.Comp.HealthBarThreshold = state.HealthBarThreshold;

        // Has the damage actually changed?
        var newDamage = state.Damage.Clone();
        var delta = newDamage - ent.Comp.Damage;
        delta.TrimZeros();

        if (delta.Empty)
            return;

        ent.Comp.Damage = newDamage;

        OnEntityDamageChanged(ent, delta);
    }
}
