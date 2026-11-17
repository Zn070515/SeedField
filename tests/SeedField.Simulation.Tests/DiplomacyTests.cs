using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class DiplomacyTests
{
    [Fact]
    public void ProposeAndAcceptCreatesATreaty()
    {
        var world = WorldWithTwoCivs();
        var engine = new SimulationEngine(world);

        var proposed = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));
        Assert.True(proposed.Success);
        var proposal = Assert.Single(world.TreatyProposals.Values);

        var accepted = engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", proposal.Id, Accept: true));
        Assert.True(accepted.Success);
        Assert.Empty(world.TreatyProposals);
        var treaty = Assert.Single(world.Treaties.Values);
        Assert.Equal(TreatyKind.NonAggression, treaty.Kind);
        Assert.True(treaty.Involves("A"));
        Assert.True(treaty.Involves("B"));
    }

    [Fact]
    public void ProposalRequiresACommunicationPath()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A", withComm: false))
            .AddEntity(Unit(2, "B"))
            .Build();

        var result = new SimulationEngine(world).ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));

        Assert.False(result.Success);
        Assert.Equal(SimulationErrorCode.NoRoute, result.ErrorCode);
        Assert.Empty(world.TreatyProposals);
    }

    [Fact]
    public void DuplicateProposalIsRejected()
    {
        var world = WorldWithTwoCivs();
        var engine = new SimulationEngine(world);

        Assert.True(engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10)).Success);
        var duplicate = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));

        Assert.False(duplicate.Success);
        Assert.Equal(SimulationErrorCode.TreatyExists, duplicate.ErrorCode);
        Assert.Single(world.TreatyProposals);
    }

    [Fact]
    public void OnlyTheTargetCanRespond()
    {
        var world = WorldWithTwoCivs();
        var engine = new SimulationEngine(world);
        var proposed = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));

        var response = engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("A", proposed.ProposalId!.Value, Accept: true));

        Assert.False(response.Success);
        Assert.Equal(SimulationErrorCode.ProposalNotFound, response.ErrorCode);
        Assert.Single(world.TreatyProposals);
    }

    [Fact]
    public void RejectRemovesTheProposalWithoutCreatingATreaty()
    {
        var world = WorldWithTwoCivs();
        var engine = new SimulationEngine(world);
        var proposed = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 10));

        var rejected = engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", proposed.ProposalId!.Value, Accept: false));

        Assert.True(rejected.Success);
        Assert.Empty(world.TreatyProposals);
        Assert.Empty(world.Treaties);
    }

    [Fact]
    public void ProposalExpiresOnTheTickBoundary()
    {
        var world = WorldWithTwoCivs();
        var engine = new SimulationEngine(world);
        engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 1));

        engine.Step(Array.Empty<Intent>());

        Assert.Empty(world.TreatyProposals);
        Assert.Empty(world.Treaties);
    }

    [Fact]
    public void BreakTreatyRequiresAParty()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddEntity(Unit(3, "C"))
            .Build();
        var engine = new SimulationEngine(world);
        var proposed = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));
        var accepted = engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", proposed.ProposalId!.Value, Accept: true));

        var broken = engine.ExecuteDiplomacyCommand(
            new BreakTreatyCommand("C", accepted.TreatyId!.Value));

        Assert.False(broken.Success);
        Assert.Equal(SimulationErrorCode.TreatyNotFound, broken.ErrorCode);
        Assert.Single(world.Treaties);
    }

    [Fact]
    public void PartyCanBreakTreaty()
    {
        var world = WorldWithTwoCivs();
        var engine = new SimulationEngine(world);
        var proposed = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));
        var accepted = engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", proposed.ProposalId!.Value, Accept: true));

        var broken = engine.ExecuteDiplomacyCommand(
            new BreakTreatyCommand("A", accepted.TreatyId!.Value));

        Assert.True(broken.Success);
        Assert.Empty(world.Treaties);
        Assert.Contains(broken.Events, e => e is TreatyBroken);
    }

    [Fact]
    public void NonAggressionBlocksAttacksBeforeEnergyAndCooldown()
    {
        var world = CombatWorld();
        var engine = new SimulationEngine(world);
        var proposed = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));
        engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", proposed.ProposalId!.Value, Accept: true));

        var blocked = engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });

        Assert.Equal(SimulationErrorCode.TreatyDenied, Assert.Single(blocked.IntentResults).ErrorCode);
        var attacker = world.FindEntity(new EntityId(1))!;
        var target = world.FindEntity(new EntityId(2))!;
        Assert.Equal(100, attacker.Energy);
        Assert.Equal(0, attacker.NextWeaponReadyTick);
        Assert.Equal(10, target.Health);
    }

    [Fact]
    public void BreakingNonAggressionAllowsAttacksAgain()
    {
        var world = CombatWorld();
        var engine = new SimulationEngine(world);
        var proposed = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));
        var accepted = engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", proposed.ProposalId!.Value, Accept: true));
        engine.ExecuteDiplomacyCommand(
            new BreakTreatyCommand("A", accepted.TreatyId!.Value));

        var result = engine.Step(new Intent[]
        {
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });

        Assert.True(Assert.Single(result.IntentResults).Accepted);
        Assert.True(world.FindEntity(new EntityId(2))!.Health < 10);
    }

    [Fact]
    public void ObservationOnlyShowsOwnTreatiesAndProposals()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .AddEntity(Unit(3, "C"))
            .Build();
        var engine = new SimulationEngine(world);
        var ab = engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 10));
        engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("B", ab.ProposalId!.Value, Accept: true));
        engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "C", TreatyKind.NonAggression, ExpiresInTicks: 10));

        var observationA = ObservationBuilder.Build(world, "A");
        var observationB = ObservationBuilder.Build(world, "B");
        var observationC = ObservationBuilder.Build(world, "C");

        Assert.Single(observationA.Treaties!);
        Assert.Equal("B", observationA.Treaties![0].OtherCivilizationId);
        Assert.Single(observationA.TreatyProposals!);
        Assert.Single(observationB.Treaties!);
        Assert.Empty(observationB.TreatyProposals!);
        Assert.Empty(observationC.Treaties!);
        Assert.Single(observationC.TreatyProposals!);
    }

    private static WorldState CombatWorld() =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(CombatUnit(1, "A", 100))
            .AddEntity(CombatUnit(2, "B", 200))
            .Build();

    private static EntityDefinition CombatUnit(long id, string civilizationId, int x) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            new Vec2(x, 100),
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Sensor: new SensorModule(5_000),
            Weapon: new WeaponModule(1_000, Damage: 3, EnergyCost: 5, CooldownTicks: 2),
            Comm: new CommModule(1, EnergyCostPerMessage: 0));

    private static WorldState WorldWithTwoCivs() =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "B"))
            .Build();

    private static EntityDefinition Unit(
        long id,
        string civilizationId,
        bool withComm = true) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Comm: withComm ? new CommModule(1, EnergyCostPerMessage: 0) : null);
}
