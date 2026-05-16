#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Body.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.NukeOps;
using Content.Shared.Pinpointer;
using Content.Shared.Roles.Components;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class NukeOpsTest : AntagTest
{
    private static readonly ProtoId<NpcFactionPrototype> SyndicateFaction = "Syndicate";
    private static readonly ProtoId<NpcFactionPrototype> NanotrasenFaction = "NanoTrasen";

    [SidedDependency(Side.Server)] private DamageableSystem _damage = default!;
    [SidedDependency(Side.Server)] private InventorySystem _inventory = default!;
    [SidedDependency(Side.Server)] private MapSystem _map = default!;
    [SidedDependency(Side.Server)] private NpcFactionSystem _faction = default!;
    [SidedDependency(Side.Server)] private NukeopsRuleSystem _traitor = default!;
    [SidedDependency(Side.Server)] private RoleSystem _role = default!;
    [SidedDependency(Side.Server)] private RoundEndSystem _round = default!;

    /// <summary>
    /// Check that a nuke ops game mode can start without issue. I.e., that the nuke station and such all get loaded.
    /// </summary>
    [Test]
    public async Task TryStopNukeOpsFromConstantlyFailing()
    {
        Server.CfgMan.SetCVar(CCVars.GridFill, true);

        // Initially in the lobby
        Assert.That(STicker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        Assert.That(Client.AttachedEntity, Is.Null);
        Assert.That(STicker.PlayerGameStatuses[Client.User!.Value], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        // Add several dummy players
        var dummies = await Pair.Server.AddDummySessions(3);
        await Pair.RunUntilSynced();

        // Opt into the nukies role.
        await Pair.SetAntagPreference("NukeopsCommander", true);
        await Pair.SetAntagPreference("NukeopsMedic", true, dummies[1].UserId);

        // Initially, the players have no attached entities
        Assert.That(Pair.Player?.AttachedEntity, Is.Null);
        Assert.That(dummies.All(x => x.AttachedEntity == null));

        // There are no grids or maps
        Assert.That(SEntMan.Count<MapComponent>(), Is.Zero);
        Assert.That(SEntMan.Count<MapGridComponent>(), Is.Zero);
        Assert.That(SEntMan.Count<StationMapComponent>(), Is.Zero);
        Assert.That(SEntMan.Count<StationMemberComponent>(), Is.Zero);
        Assert.That(SEntMan.Count<StationCentcommComponent>(), Is.Zero);

        // And no nukie related components
        Assert.That(SEntMan.Count<NukeopsRuleComponent>(), Is.Zero);
        Assert.That(SEntMan.Count<NukeopsRoleComponent>(), Is.Zero);
        Assert.That(SEntMan.Count<NukeOperativeComponent>(), Is.Zero);
        Assert.That(SEntMan.Count<NukeOpsShuttleComponent>(), Is.Zero);
        Assert.That(SEntMan.Count<NukeOperativeSpawnerComponent>(), Is.Zero);

        // Ready up and start nukeops
        STicker.ToggleReadyAll(true);
        Assert.That(STicker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.ReadyToPlay));
        await Pair.WaitCommand("forcepreset Nukeops");
        await Pair.RunTicksSync(10);

        // Game should have started
        Assert.That(STicker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        Assert.That(STicker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.JoinedGame));
        Assert.That(Client.EntMan.EntityExists(Client.AttachedEntity));

        var dummyEnts = dummies.Select(x => x.AttachedEntity ?? default).ToArray();
        var player = Pair.Player!.AttachedEntity!.Value;
        Assert.That(SEntMan.EntityExists(player));
        Assert.That(dummyEnts.All(e => SEntMan.EntityExists(e)));

        // Maps now exist
        Assert.That(SEntMan.Count<MapComponent>(), Is.GreaterThan(0));
        Assert.That(SEntMan.Count<MapGridComponent>(), Is.GreaterThan(0));
        Assert.That(SEntMan.Count<StationCentcommComponent>(), Is.EqualTo(1));

        // And we now have nukie related components
        Assert.That(SEntMan.Count<NukeopsRuleComponent>(), Is.EqualTo(1));
        Assert.That(SEntMan.Count<NukeopsRoleComponent>(), Is.EqualTo(2));
        Assert.That(SEntMan.Count<NukeOperativeComponent>(), Is.EqualTo(2));
        Assert.That(SEntMan.Count<NukeOpsShuttleComponent>(), Is.EqualTo(1));

        // The player entity should be the nukie commander
        var mind = Mind.GetMind(player)!.Value;
        Assert.That(SEntMan.HasComponent<NukeOperativeComponent>(player));
        Assert.That(_role.MindIsAntagonist(mind));
        Assert.That(_role.MindHasRole<NukeopsRoleComponent>(mind));
        Assert.That(_faction.IsMember(player, SyndicateFaction), Is.True);
        Assert.That(_faction.IsMember(player, NanotrasenFaction), Is.False);
        var roles = _role.MindGetAllRoleInfo(mind);
        var cmdRoles = roles.Where(x => x.Prototype == "NukeopsCommander");
        Assert.That(cmdRoles.Count(), Is.EqualTo(1));

        // The second dummy player should be a medic
        var dummyMind = Mind.GetMind(dummyEnts[1])!.Value;
        Assert.That(SEntMan.HasComponent<NukeOperativeComponent>(dummyEnts[1]));
        Assert.That(_role.MindIsAntagonist(dummyMind));
        Assert.That(_role.MindHasRole<NukeopsRoleComponent>(dummyMind));
        Assert.That(_faction.IsMember(dummyEnts[1], SyndicateFaction), Is.True);
        Assert.That(_faction.IsMember(dummyEnts[1], NanotrasenFaction), Is.False);
        roles = _role.MindGetAllRoleInfo(dummyMind);
        cmdRoles = roles.Where(x => x.Prototype == "NukeopsMedic");
        Assert.That(cmdRoles.Count(), Is.EqualTo(1));

        // The other two players should have just spawned in as normal.
        CheckDummy(0);
        CheckDummy(2);
        void CheckDummy(int i)
        {
            var ent = dummyEnts[i];
            var mindCrew = Mind.GetMind(ent)!.Value;
            Assert.That(SEntMan.HasComponent<NukeOperativeComponent>(ent), Is.False);
            Assert.That(_role.MindIsAntagonist(mindCrew), Is.False);
            Assert.That(_role.MindHasRole<NukeopsRoleComponent>(mindCrew), Is.False);
            Assert.That(_faction.IsMember(ent, SyndicateFaction), Is.False);
            Assert.That(_faction.IsMember(ent, NanotrasenFaction), Is.True);
            var nukeroles = new List<string>() { "Nukeops", "NukeopsMedic", "NukeopsCommander" };
            Assert.That(_role.MindGetAllRoleInfo(mindCrew).Any(x => nukeroles.Contains(x.Prototype)), Is.False);
        }

        // The game rule exists, and all the stations/shuttles/maps are properly initialized
        var rule = SEntMan.AllComponents<NukeopsRuleComponent>().Single();
        var ruleComp = rule.Component;
        var gridsRule = SEntMan.GetComponent<RuleGridsComponent>(rule.Uid);
        foreach (var grid in gridsRule.MapGrids)
        {
            Assert.That(SEntMan.EntityExists(grid));
            Assert.That(SEntMan.HasComponent<MapGridComponent>(grid));
        }
        Assert.That(SEntMan.EntityExists(ruleComp.TargetStation));

        Assert.That(SEntMan.HasComponent<StationDataComponent>(ruleComp.TargetStation));

        var nukieShuttle = SEntMan.AllComponents<NukeOpsShuttleComponent>().Single();
        var nukieShuttlEnt = nukieShuttle.Uid;
        Assert.That(SEntMan.EntityExists(nukieShuttlEnt));
        Assert.That(nukieShuttle.Component.AssociatedRule, Is.EqualTo(rule.Uid));

        EntityUid? nukieStationEnt = null;
        foreach (var grid in gridsRule.MapGrids)
        {
            if (SEntMan.HasComponent<StationMemberComponent>(grid))
            {
                nukieStationEnt = grid;
                break;
            }
        }

        Assert.That(!SEntMan.EntityExists(nukieStationEnt)); // its not supposed to be a station!
        Assert.That(_map.MapExists(gridsRule.Map));
        var nukieMap = _map.GetMap(gridsRule.Map!.Value);

        var targetStation = SEntMan.GetComponent<StationDataComponent>(ruleComp.TargetStation!.Value);
        var targetGrid = targetStation.Grids.First();
        var targetMap = SEntMan.GetComponent<TransformComponent>(targetGrid).MapUid!.Value;
        Assert.That(targetMap, Is.Not.EqualTo(nukieMap));

        Assert.That(SEntMan.GetComponent<TransformComponent>(player).MapUid, Is.EqualTo(nukieMap));
        Assert.That(SEntMan.GetComponent<TransformComponent>(nukieShuttlEnt).MapUid, Is.EqualTo(nukieMap));

        // The maps are all map-initialized, including the player
        // Yes, this is necessary as this has repeatedly been broken somehow.
        Assert.That(_map.IsInitialized(nukieMap));
        Assert.That(_map.IsInitialized(targetMap));
        Assert.That(_map.IsPaused(nukieMap), Is.False);
        Assert.That(_map.IsPaused(targetMap), Is.False);

        EntityLifeStage LifeStage(EntityUid? uid) => SEntMan.GetComponent<MetaDataComponent>(uid!.Value).EntityLifeStage;
        Assert.That(LifeStage(player), Is.GreaterThan(EntityLifeStage.Initialized));
        Assert.That(LifeStage(nukieMap), Is.GreaterThan(EntityLifeStage.Initialized));
        Assert.That(LifeStage(targetMap), Is.GreaterThan(EntityLifeStage.Initialized));
        Assert.That(LifeStage(nukieShuttlEnt), Is.GreaterThan(EntityLifeStage.Initialized));
        Assert.That(LifeStage(ruleComp.TargetStation), Is.GreaterThan(EntityLifeStage.Initialized));

        // Make sure the player has hands. We've had fucking disarmed nukies before.
        Assert.That(SEntMan.HasComponent<HandsComponent>(player));
        Assert.That(SEntMan.GetComponent<HandsComponent>(player).Hands.Count, Is.GreaterThan(0));

        // While we're at it, lets make sure they aren't naked. I don't know how many inventory slots all mobs will be
        // likely to have in the future. But nukies should probably have at least 3 slots with something in them.
        var enumerator = _inventory.GetSlotEnumerator(player);
        var total = 0;
        while (enumerator.NextItem(out _))
        {
            total++;
        }
        Assert.That(total, Is.GreaterThan(3));

        // Check the nukie commander passed basic training and figured out how to breathe.
        if (SEntMan.TryGetComponent<RespiratorComponent>(player, out var resp))
        {
            var totalSeconds = 30;
            var totalTicks = (int)Math.Ceiling(totalSeconds / Server.Timing.TickPeriod.TotalSeconds);
            var increment = 5;
            for (var tick = 0; tick < totalTicks; tick += increment)
            {
                await Pair.RunTicksSync(increment);
                Assert.That(resp.SuffocationCycles, Is.LessThanOrEqualTo(resp.SuffocationCycleThreshold));
                Assert.That(_damage.GetTotalDamage(player), Is.EqualTo(FixedPoint2.Zero));
            }
        }

        // Check that the round does not end prematurely when agents are deleted in the outpost
        var nukies = dummyEnts.Where(SEntMan.HasComponent<NukeOperativeComponent>).Append(player).ToArray();
        await Server.WaitAssertion(() =>
        {
            for (var i = 0; i < nukies.Length - 1; i++)
            {
                SEntMan.DeleteEntity(nukies[i]);
                Assert.That(_round.IsRoundEndRequested,
                    Is.False,
                    $"The round ended, but {nukies.Length - i - 1} nukies are still alive!");
            }
            // Delete the last nukie and make sure the round ends.
            SEntMan.DeleteEntity(nukies[^1]);

            Assert.That(_round.IsRoundEndRequested,
                "All nukies were deleted, but the round didn't end!");
        });

        STicker.SetGamePreset((GamePresetPrototype?) null);
    }
}
