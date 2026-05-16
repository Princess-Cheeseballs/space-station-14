using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class TraitorRuleTest : AntagTest
{
    private const string TraitorGameRuleProtoId = "Traitor";
    private const string TraitorAntagRoleName = "Traitor";
    private static readonly ProtoId<NpcFactionPrototype> SyndicateFaction = "Syndicate";
    private static readonly ProtoId<NpcFactionPrototype> NanotrasenFaction = "NanoTrasen";

    [SidedDependency(Side.Server)] private NpcFactionSystem _faction = default!;
    [SidedDependency(Side.Server)] private RoleSystem _role = default!;
    [SidedDependency(Side.Server)] private TraitorRuleSystem _traitor = default!;

    // TODO: Fill the rest of this out!!!
    [Test]
    public async Task TestTraitorObjectives()
    {
        // Look up the minimum player count and max total objective difficulty for the game rule
        var minPlayers = 1;
        var maxDifficulty = 0f;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SProtoMan.TryIndex<EntityPrototype>(TraitorGameRuleProtoId, out var gameRuleEnt),
            $"Failed to lookup traitor game rule entity prototype with ID \"{TraitorGameRuleProtoId}\"!");

            Assert.That(gameRuleEnt.TryGetComponent<GameRuleComponent>(out var gameRule, SEntMan.ComponentFactory),
            $"Game rule entity {TraitorGameRuleProtoId} does not have a GameRuleComponent!");

            Assert.That(gameRuleEnt.TryGetComponent<AntagRandomObjectivesComponent>(out var randomObjectives, SEntMan.ComponentFactory),
            $"Game rule entity {TraitorGameRuleProtoId} does not have an AntagRandomObjectivesComponent!");

            minPlayers = gameRule.MinPlayers;
            maxDifficulty = randomObjectives.MaxDifficulty;
        });

        // Initially in the lobby
        Assert.That(STicker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        Assert.That(Client.AttachedEntity, Is.Null);
        Assert.That(STicker.PlayerGameStatuses[Client.User!.Value], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        // Add enough dummy players for the game rule
        var dummies = await Server.AddDummySessions(minPlayers);
        await Pair.RunUntilSynced();

        // Initially, the players have no attached entities
        Assert.That(Pair.Player?.AttachedEntity, Is.Null);
        Assert.That(dummies.All(x => x.AttachedEntity == null));

        // Opt-in the player for the traitor role
        await Pair.SetAntagPreference(TraitorAntagRoleName, true);

        // Add the game rule
        TraitorRuleComponent traitorRule = null;
        await Server.WaitPost(() =>
        {
            var gameRuleEnt = STicker.AddGameRule(TraitorGameRuleProtoId);
            Assert.That(SEntMan.TryGetComponent<TraitorRuleComponent>(gameRuleEnt, out traitorRule));

            // Ready up
            STicker.ToggleReadyAll(true);
            Assert.That(STicker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.ReadyToPlay));

            // Start the round
            STicker.StartRound();
            // Force traitor mode to start (skip the delay)
            STicker.StartGameRule(gameRuleEnt);
        });
        await Pair.RunTicksSync(10);

        // Game should have started
        Assert.That(STicker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        Assert.That(STicker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.JoinedGame));
        Assert.That(Client.EntMan.EntityExists(Client.AttachedEntity));

        // Check the player and dummies are spawned
        var dummyEnts = dummies.Select(x => x.AttachedEntity ?? default).ToArray();
        var player = Pair.Player!.AttachedEntity!.Value;
        Assert.That(SEntMan.EntityExists(player));
        Assert.That(dummyEnts.All(SEntMan.EntityExists));

        // Make sure the player is a traitor.
        var mind = Mind.GetMind(player)!.Value;
        Assert.That(_role.MindIsAntagonist(mind));
        Assert.That(_faction.IsMember(player, SyndicateFaction), Is.True);
        Assert.That(_faction.IsMember(player, NanotrasenFaction), Is.False);
        Assert.That(traitorRule.TotalTraitors, Is.EqualTo(1));
        Assert.That(traitorRule.TraitorMinds[0], Is.EqualTo(mind));

        // Check total objective difficulty
        Assert.That(SEntMan.TryGetComponent<MindComponent>(mind, out var mindComp));
        var totalDifficulty = mindComp.Objectives.Sum(o => SEntMan.GetComponent<ObjectiveComponent>(o).Difficulty);
        Assert.That(totalDifficulty, Is.AtMost(maxDifficulty),
            $"MaxDifficulty exceeded! Objectives: {string.Join(", ", mindComp.Objectives.Select(o => FormatObjective(o, SEntMan)))}");
        Assert.That(mindComp.Objectives, Is.Not.Empty,
            $"No objectives assigned!");
    }

    private static string FormatObjective(Entity<ObjectiveComponent> entity, IEntityManager entMan)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(entity);
        var objective = entMan.GetComponent<ObjectiveComponent>(entity);
        return $"{meta.EntityName} ({objective.Difficulty})";
    }
}
