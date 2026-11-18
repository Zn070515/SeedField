using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Visual-slice scenario. Numbers are demo values only, not balance decisions.
/// The same world builder and controller contract as the Headless runner are used.
/// </summary>
internal static class Scenarios
{
    public const int WidthMm = 16_000;
    public const int HeightMm = 8_000;

    public static readonly Vec2 AStagingPosition = new(2_000, 4_000);
    public static readonly Vec2 BStagingPosition = new(13_000, 4_000);

    public static readonly RecoveryBlueprint EmergencyRecovery = new(
        "emergency-cell",
        RequiredEnergy: 40,
        RequiredMaterial: 30,
        MaxHealth: 100,
        Energy: 100,
        EnergyCapacity: 1_000,
        Material: 0,
        Drive: new DriveModule(60, EnergyCostPerMove: 1),
        Comm: new CommModule(1, EnergyCostPerMessage: 1));

    public static WorldState BuildWorld(bool peaceful = false)
    {
        var builder = new WorldBuilder()
            .AddRegion("core-a", 2_000, 2_000, isCoreRegion: true)
            .AddRegion("frontier", WidthMm, HeightMm)
            .AddLink("l-core", "core-a", "frontier", latencyTicks: 1)
            .AddCore(
                "A",
                "core-a",
                new SeedCoreConfig(
                    EnergyPerTick: 2,
                    MaterialPerTick: 2,
                    MaxEnergyReserve: 100,
                    MaxMaterialReserve: 100,
                    RecoveryWindowTicks: 60,
                    RecoveryQuotaPerWindow: 2,
                    RecoveryCostGrowthPercent: 50,
                    RecoveryStreakDecayTicks: 30,
                    RecoveryOverdraftCostPercent: 200,
                    AnchorRecoveryCostPercent: 25,
                    SiegeRecoveryCostPercent: 100,
                    SiegeQuotaPenalty: 1,
                    RecoveryMaintenanceMaterialCost: 20))
            .AddStock("frontier", "A", 500)
            .AddStock("frontier", "B", 500)
            .AddControlPoint(
                "relay-west",
                "frontier",
                new Vec2(5_000, 4_000),
                radiusMm: 1_200,
                holdTicksRequired: 12)
            .AddControlPoint(
                "relay-center",
                "frontier",
                new Vec2(8_000, 4_000),
                radiusMm: 1_200,
                holdTicksRequired: 12)
            .AddControlPoint(
                "relay-east",
                "frontier",
                new Vec2(11_000, 4_000),
                radiusMm: 1_200,
                holdTicksRequired: 12)
            .SetVictoryCondition(capturedPointsToWin: 2)
            .SetGridCapacity("frontier", "A", capacity: 500)
            .SetGridCapacity("frontier", "B", capacity: 500)
            .SetPowerPolicy(
                "A",
                reserveEnergy: 10,
                highPriorityThreshold: 5,
                priorityReserveEnergy: 20)
            .SetPowerPolicy(
                "B",
                reserveEnergy: 10,
                highPriorityThreshold: 5,
                priorityReserveEnergy: 20);

        if (!peaceful)
        {
            builder.AddEntity(new EntityDefinition(
                new EntityId(4),
                "B",
                "frontier",
                new Vec2(15_000, 1_000),
                MaxHealth: 1_000,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0));
        }

        var world = builder.Build();

        world.Regions["frontier"].AddEnergy("A", 200);
        world.Regions["frontier"].AddEnergy("B", 200);

        var workshop = world.Workshop;
        workshop.Publish(new DeviceBlueprint(
            "power", 0, "Power Cell", 60, 5_000,
            Power: new PowerModule(EnergyPerTick: 4, StorageCapacity: 5_000),
            ControllerProgramId: DevicePrograms.IdleId,
            MaterialCost: 20,
            BuildTicks: 2));
        workshop.Publish(new DeviceBlueprint(
            "sensing", 0, "Sensor Suite", 60, 0,
            Sensor: new SensorModule(5_000),
            ControllerProgramId: DevicePrograms.IdleId,
            MaterialCost: 30));
        workshop.Publish(new DeviceBlueprint(
            "comm", 0, "Comm Relay", 60, 0,
            Comm: new CommModule(1, 1, BandwidthPerTick: 1),
            ControllerProgramId: DevicePrograms.RelayId,
            MaterialCost: 30));
        workshop.Publish(new DeviceBlueprint(
            "state", 0, "State Core", 60, 0,
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64),
            ControllerProgramId: DevicePrograms.IdleId,
            MaterialCost: 40));

        var strike = workshop.Publish(new DeviceBlueprint(
            "strike", 0, "Strike Frame", 150, 5_000,
            Sensor: new SensorModule(4_500),
            Weapon: new WeaponModule(3_500, Damage: 8, EnergyCost: 2, CooldownTicks: 2),
            Drive: new DriveModule(90, EnergyCostPerMove: 1),
            Comm: new CommModule(1, 1, BandwidthPerTick: 1),
            Power: new PowerModule(
                EnergyPerTick: 2,
                StorageCapacity: 5_000,
                Priority: 10),
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64),
            ControllerProgramId: "program.strike",
            MaterialCost: 80));

        var scoutV1 = workshop.Publish(new DeviceBlueprint(
            "scout", 0, "Scout Mk1", 120, 5_000,
            Sensor: new SensorModule(5_000),
            Drive: new DriveModule(120, EnergyCostPerMove: 1),
            Comm: new CommModule(1, 1, BandwidthPerTick: 1),
            Power: new PowerModule(EnergyPerTick: 2, StorageCapacity: 5_000),
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64),
            ControllerProgramId: "program.scout.v1",
            MaterialCost: 50));

        workshop.Publish(new DeviceBlueprint(
            "scout", 0, "Scout Mk2", 140, 5_000,
            Sensor: new SensorModule(6_000),
            Drive: new DriveModule(130, EnergyCostPerMove: 1),
            Comm: new CommModule(1, 1, BandwidthPerTick: 1),
            Power: new PowerModule(EnergyPerTick: 3, StorageCapacity: 5_000),
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64),
            ControllerProgramId: "program.scout.v2",
            MaterialCost: 60));

        // A same-frame module swap used by the visual smoke test:
        // sensor-only scout -> sensor + drill arm. UpgradeCommand drives it.
        workshop.Publish(new DeviceBlueprint(
            "scout", 0, "Scout Mk3 Breacher", 150, 5_000,
            Sensor: new SensorModule(6_500),
            Weapon: new WeaponModule(3_000, Damage: 6, EnergyCost: 1, CooldownTicks: 2),
            Drive: new DriveModule(130, EnergyCostPerMove: 1),
            Comm: new CommModule(1, 1, BandwidthPerTick: 1),
            Power: new PowerModule(EnergyPerTick: 3, StorageCapacity: 5_000),
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64),
            ControllerProgramId: "program.scout.v1",
            MaterialCost: 65));

        DeploymentService.Deploy(world, scoutV1, "A", "frontier", AStagingPosition);
        DeploymentService.Deploy(world, strike, "A", "frontier", new Vec2(4_000, 4_000));
        if (!peaceful)
        {
            DeploymentService.Deploy(world, strike, "B", "frontier", BStagingPosition);
        }

        return world;
    }

    /// <summary>
    /// Generic per-device script. It only commands obs.viewerEntityId and relies on the
    /// device-scoped observation (own sensor only, region-filtered messages).
    /// </summary>
    public const string DeviceScript = """
        function decide(obs) {
            var me = null;
            for (var i = 0; i < obs.entities.length; i++) {
                if (obs.entities[i].entityId === obs.viewerEntityId) {
                    me = obs.entities[i];
                }
            }
            if (!me) {
                return [];
            }

            var enemies = obs.entities.filter(function (e) { return !e.isSelf; });
            var target = nearest(me, enemies);
            var intents = [];

            if (me.hasComm && target) {
                intents.push({
                    type: "send",
                    entityId: me.entityId,
                    toCivilizationId: obs.civilizationId,
                    topic: "contact",
                    payload: JSON.stringify({ x: target.x, y: target.y })
                });
            }

            if (target && me.hasWeapon && distance(me, target) <= me.weaponRangeMm) {
                intents.push({
                    type: "attack",
                    entityId: me.entityId,
                    targetId: target.entityId
                });
            } else if (target && me.hasDrive) {
                intents.push({
                    type: "move",
                    entityId: me.entityId,
                    target: { x: target.x, y: target.y }
                });
            } else if (!target && me.hasDrive) {
                var objective = nearestObjective(obs, me);
                var destination = objective
                    ? { x: objective.x, y: objective.y }
                    : (obs.civilizationId === "A"
                        ? { x: 10000, y: 4000 }
                        : { x: 6000, y: 4000 });
                var last = lastContact(obs);
                if (last && !objective) {
                    destination = JSON.parse(last.payload);
                }
                intents.push({
                    type: "move",
                    entityId: me.entityId,
                    target: destination
                });
            }

            return intents;
        }

        function nearestObjective(obs, me) {
            var best = null;
            var bestDistance = 1e18;
            var points = obs.controlPoints || [];
            for (var i = 0; i < points.length; i++) {
                if (points[i].ownerCivilizationId === obs.civilizationId) {
                    continue;
                }
                var d = distance(me, points[i]);
                if (d < bestDistance) {
                    bestDistance = d;
                    best = points[i];
                }
            }
            return best;
        }

        function lastContact(obs) {
            var last = null;
            for (var i = 0; i < obs.messages.length; i++) {
                if (obs.messages[i].topic === "contact") {
                    last = obs.messages[i];
                }
            }
            return last;
        }

        function distance(a, b) {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return Math.sqrt(dx * dx + dy * dy);
        }

        function nearest(from, list) {
            var best = null;
            var bestDistance = 1e18;
            for (var i = 0; i < list.length; i++) {
                var d = distance(from, list[i]);
                if (d < bestDistance) {
                    bestDistance = d;
                    best = list[i];
                }
            }
            return best;
        }
        """;

    public const string ScriptA = DeviceScript;
    public const string ScriptB = DeviceScript;

    /// <summary>
    /// Mixed-benchmark control: a moving controller with no messages or attacks.
    /// Compare it with ScriptA to isolate inbox/observation broadcast cost.
    /// </summary>
    public const string BenchmarkQuietScript = """
        function decide(obs) {
            var me = null;
            for (var i = 0; i < obs.entities.length; i++) {
                if (obs.entities[i].entityId === obs.viewerEntityId) {
                    me = obs.entities[i];
                    break;
                }
            }
            if (!me || !me.hasDrive) {
                return [];
            }
            return [{
                type: "move",
                entityId: me.entityId,
                target: { x: 10000, y: 4000 }
            }];
        }
        """;
}
