namespace SeedField.Runtime.Js;

/// <summary>
/// Trusted packages shipped with the game. They use the same device observation and
/// intent schema as player code; no host APIs are exposed.
/// </summary>
public static class BuiltInOpponents
{
    public static IReadOnlyList<OpponentPackage> All => new[] { Skirmisher, Turtle };

    public static OpponentPackage? Find(string packageId) =>
        All.FirstOrDefault(package =>
            string.Equals(package.Manifest.PackageId, packageId, StringComparison.Ordinal));

    public static OpponentPackage Skirmisher { get; } = Create(
        "builtin.skirmisher",
        "0.1.0",
        """
        function decide(obs) {
            var me = findMe(obs);
            if (!me) { return []; }

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
                intents.push({ type: "attack", entityId: me.entityId, targetId: target.entityId });
            } else if (target && me.hasDrive) {
                intents.push({
                    type: "move",
                    entityId: me.entityId,
                    target: { x: target.x, y: target.y }
                });
            } else if (!target && me.hasDrive) {
                var objective = nearestObjective(obs, me);
                if (objective) {
                    intents.push({
                        type: "move",
                        entityId: me.entityId,
                        target: { x: objective.x, y: objective.y }
                    });
                }
            }

            return intents;
        }

        function findMe(obs) {
            for (var i = 0; i < obs.entities.length; i++) {
                if (obs.entities[i].entityId === obs.viewerEntityId) { return obs.entities[i]; }
            }
            return null;
        }

        function nearestObjective(obs, me) {
            var points = obs.controlPoints || [];
            var best = null;
            var bestDistance = 1e18;
            for (var i = 0; i < points.length; i++) {
                if (points[i].ownerCivilizationId === obs.civilizationId) { continue; }
                var d = distance(me, points[i]);
                if (d < bestDistance) { bestDistance = d; best = points[i]; }
            }
            return best;
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
                if (d < bestDistance) { bestDistance = d; best = list[i]; }
            }
            return best;
        }
        """);

    public static OpponentPackage Turtle { get; } = Create(
        "builtin.turtle",
        "0.1.0",
        """
        function decide(obs) {
            var me = findMe(obs);
            if (!me) { return []; }

            var enemies = obs.entities.filter(function (e) { return !e.isSelf; });
            var target = nearest(me, enemies);
            if (target && me.hasWeapon && distance(me, target) <= me.weaponRangeMm) {
                return [{ type: "attack", entityId: me.entityId, targetId: target.entityId }];
            }

            if (!me.hasDrive) { return []; }

            var objective = nearestObjective(obs, me);
            if (objective) {
                return [{
                    type: "move",
                    entityId: me.entityId,
                    target: { x: objective.x, y: objective.y }
                }];
            }

            return [];
        }

        function findMe(obs) {
            for (var i = 0; i < obs.entities.length; i++) {
                if (obs.entities[i].entityId === obs.viewerEntityId) { return obs.entities[i]; }
            }
            return null;
        }

        function nearestObjective(obs, me) {
            var points = obs.controlPoints || [];
            var best = null;
            var bestDistance = 1e18;
            for (var i = 0; i < points.length; i++) {
                var owner = points[i].ownerCivilizationId;
                if (owner !== null && owner !== obs.civilizationId) { continue; }
                var d = distance(me, points[i]);
                if (d < bestDistance) { bestDistance = d; best = points[i]; }
            }
            return best;
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
                if (d < bestDistance) { bestDistance = d; best = list[i]; }
            }
            return best;
        }
        """);

    private static OpponentPackage Create(string packageId, string version, string program) =>
        OpponentPackage.Create(
            new OpponentPackageManifest(
                OpponentPackage.CurrentSchemaVersion,
                packageId,
                version,
                "offline-opponent",
                OpponentPackage.CurrentEngineCompat,
                new[] { "programs/controller.js" },
                new[] { "tactical-control" },
                string.Empty),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["programs/controller.js"] = program,
            });
}
