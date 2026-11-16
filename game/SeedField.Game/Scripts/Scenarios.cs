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

    public static WorldState BuildWorld()
    {
        var world = new WorldBuilder()
            .AddRegion("frontier", WidthMm, HeightMm)
            .Build();

        var workshop = world.Workshop;
        workshop.Publish(new DeviceBlueprint(
            "power", 0, "Power Cell", 60, 5_000,
            Power: new PowerModule(EnergyPerTick: 4, StorageCapacity: 5_000)));
        workshop.Publish(new DeviceBlueprint(
            "sensing", 0, "Sensor Suite", 60, 0,
            Sensor: new SensorModule(5_000)));
        workshop.Publish(new DeviceBlueprint(
            "comm", 0, "Comm Relay", 60, 0,
            Comm: new CommModule(1, 1, BandwidthPerTick: 1)));
        workshop.Publish(new DeviceBlueprint(
            "state", 0, "State Core", 60, 0,
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64)));

        var strike = workshop.Publish(new DeviceBlueprint(
            "strike", 0, "Strike Frame", 150, 5_000,
            Sensor: new SensorModule(4_500),
            Weapon: new WeaponModule(3_500, Damage: 8, EnergyCost: 2, CooldownTicks: 2),
            Drive: new DriveModule(90, EnergyCostPerMove: 1),
            Comm: new CommModule(1, 1, BandwidthPerTick: 1),
            Power: new PowerModule(EnergyPerTick: 2, StorageCapacity: 5_000),
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64),
            ControllerProgramId: "program.strike"));

        var scoutV1 = workshop.Publish(new DeviceBlueprint(
            "scout", 0, "Scout Mk1", 120, 5_000,
            Sensor: new SensorModule(5_000),
            Drive: new DriveModule(120, EnergyCostPerMove: 1),
            Comm: new CommModule(1, 1, BandwidthPerTick: 1),
            Power: new PowerModule(EnergyPerTick: 2, StorageCapacity: 5_000),
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64),
            ControllerProgramId: "program.scout.v1"));

        workshop.Publish(new DeviceBlueprint(
            "scout", 0, "Scout Mk2", 140, 5_000,
            Sensor: new SensorModule(6_000),
            Drive: new DriveModule(130, EnergyCostPerMove: 1),
            Comm: new CommModule(1, 1, BandwidthPerTick: 1),
            Power: new PowerModule(EnergyPerTick: 3, StorageCapacity: 5_000),
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64),
            ControllerProgramId: "program.scout.v2"));

        DeploymentService.Deploy(world, scoutV1, "A", "frontier", new Vec2(2_000, 4_000));
        DeploymentService.Deploy(world, strike, "A", "frontier", new Vec2(3_000, 4_000));
        DeploymentService.Deploy(world, strike, "B", "frontier", new Vec2(13_000, 4_000));

        return world;
    }

    public const string ScriptA = """
        function decide(obs) {
            var intents = [];
            var self = obs.entities.filter(function (e) { return e.isSelf; });
            var enemies = obs.entities.filter(function (e) { return !e.isSelf; });

            var scout = self.filter(function (e) { return e.entityId === 1; })[0];
            var gunner = self.filter(function (e) { return e.entityId === 2; })[0];

            if (scout) {
                if (enemies.length > 0) {
                    var seen = enemies[0];
                    intents.push({
                        type: "send",
                        entityId: 1,
                        toCivilizationId: "A",
                        topic: "contact",
                        payload: JSON.stringify({ x: seen.x, y: seen.y })
                    });
                    intents.push({
                        type: "move",
                        entityId: 1,
                        target: { x: seen.x, y: seen.y }
                    });
                } else {
                    intents.push({
                        type: "move",
                        entityId: 1,
                        target: { x: 10000, y: 4000 }
                    });
                }
            }

            if (gunner) {
                var target = nearest(gunner, enemies);
                if (target && distance(gunner, target) <= 3500) {
                    intents.push({ type: "attack", entityId: 2, targetId: target.entityId });
                } else if (target) {
                    intents.push({
                        type: "move",
                        entityId: 2,
                        target: { x: target.x, y: target.y }
                    });
                } else {
                    var last = null;
                    for (var i = 0; i < obs.messages.length; i++) {
                        if (obs.messages[i].topic === "contact") {
                            last = obs.messages[i];
                        }
                    }
                    if (last) {
                        var p = JSON.parse(last.payload);
                        intents.push({ type: "move", entityId: 2, target: p });
                    }
                }
            }

            return intents;
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

    public const string ScriptB = """
        function decide(obs) {
            var self = obs.entities.filter(function (e) { return e.isSelf; })[0];
            if (!self) {
                return [];
            }

            var enemies = obs.entities.filter(function (e) { return !e.isSelf; });
            if (enemies.length === 0) {
                return [{ type: "move", entityId: 3, target: { x: 6000, y: 4000 } }];
            }

            var target = enemies[0];
            var dx = target.x - self.x;
            var dy = target.y - self.y;
            var distance = Math.sqrt(dx * dx + dy * dy);

            if (distance <= 3500) {
                return [{ type: "attack", entityId: 3, targetId: target.entityId }];
            }

            return [{
                type: "move",
                entityId: 3,
                target: { x: target.x, y: target.y }
            }];
        }
        """;
}
