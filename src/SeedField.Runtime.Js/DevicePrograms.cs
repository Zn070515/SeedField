using SeedField.Simulation;

namespace SeedField.Runtime.Js;

/// <summary>
/// Built-in M1 controller templates. They are ordinary scripts under the same
/// observation/intent contract as player code: no host access, no hidden state in the
/// simulation. A blueprint opts in with <see cref="DeviceBlueprint.ControllerProgramId"/>;
/// unknown or missing ids keep the civilization program as fallback.
/// </summary>
public static class DevicePrograms
{
    public const string IdleId = "builtin.idle.v1";
    public const string ScoutId = "builtin.scout.v1";
    public const string SentryId = "builtin.sentry.v1";
    public const string RelayId = "builtin.relay.v1";
    public const string HarvesterId = "builtin.harvester.v1";

    private const string Helpers = """
        var __seedfieldLastReportTick = -1000;

        function __seedfieldSelf(obs) {
            for (var i = 0; i < obs.entities.length; i++) {
                if (obs.entities[i].entityId === obs.viewerEntityId) {
                    return obs.entities[i];
                }
            }
            return null;
        }

        function __seedfieldNearestEnemy(obs, me) {
            var best = null;
            var bestDistance = 1e18;
            for (var i = 0; i < obs.entities.length; i++) {
                var entity = obs.entities[i];
                if (entity.isSelf) {
                    continue;
                }
                var d = __seedfieldDistanceSq(me, entity);
                if (d < bestDistance) {
                    bestDistance = d;
                    best = entity;
                }
            }
            return best;
        }

        function __seedfieldDistanceSq(a, b) {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return (dx * dx) + (dy * dy);
        }

        function __seedfieldDistance(a, b) {
            return Math.sqrt(__seedfieldDistanceSq(a, b));
        }

        function __seedfieldCanReport(obs) {
            if (obs.tick - __seedfieldLastReportTick < 30) {
                return false;
            }
            __seedfieldLastReportTick = obs.tick;
            return true;
        }

        function __seedfieldObjective(obs, me) {
            var best = null;
            var bestDistance = 1e18;
            var points = obs.controlPoints || [];
            for (var i = 0; i < points.length; i++) {
                if (points[i].ownerCivilizationId === obs.civilizationId) {
                    continue;
                }
                var d = __seedfieldDistanceSq(me, points[i]);
                if (d < bestDistance) {
                    bestDistance = d;
                    best = points[i];
                }
            }
            if (best) {
                return { x: best.x, y: best.y };
            }
            return obs.civilizationId === "A"
                ? { x: 10000, y: 4000 }
                : { x: 6000, y: 4000 };
        }

        function __seedfieldReport(obs, me, target, intents) {
            if (!me.hasComm || !__seedfieldCanReport(obs)) {
                return;
            }
            intents.push({
                type: "send",
                entityId: me.entityId,
                toCivilizationId: obs.civilizationId,
                topic: "contact",
                payload: JSON.stringify({ x: target.x, y: target.y })
            });
        }
        """;

    private const string IdleBody = """
        function decide(obs) {
            return [];
        }
        """;

    private const string ScoutBody = """
        function decide(obs) {
            var me = __seedfieldSelf(obs);
            if (!me) {
                return [];
            }

            var target = __seedfieldNearestEnemy(obs, me);
            var intents = [];
            if (target) {
                __seedfieldReport(obs, me, target, intents);
            }

            if (target && me.hasWeapon &&
                __seedfieldDistance(me, target) <= me.weaponRangeMm) {
                intents.push({
                    type: "attack",
                    entityId: me.entityId,
                    targetId: target.entityId
                });
            } else if (me.hasDrive) {
                var destination = target
                    ? { x: target.x, y: target.y }
                    : __seedfieldObjective(obs, me);
                intents.push({
                    type: "move",
                    entityId: me.entityId,
                    target: destination
                });
            }

            return intents;
        }
        """;

    private const string SentryBody = """
        function decide(obs) {
            var me = __seedfieldSelf(obs);
            if (!me) {
                return [];
            }

            var target = __seedfieldNearestEnemy(obs, me);
            if (!target) {
                return [];
            }

            if (me.hasWeapon &&
                __seedfieldDistance(me, target) <= me.weaponRangeMm) {
                return [{
                    type: "attack",
                    entityId: me.entityId,
                    targetId: target.entityId
                }];
            }

            var intents = [];
            if (me.hasDrive) {
                intents.push({
                    type: "move",
                    entityId: me.entityId,
                    target: { x: target.x, y: target.y }
                });
            } else {
                __seedfieldReport(obs, me, target, intents);
            }

            return intents;
        }
        """;

    private const string RelayBody = """
        var __seedfieldLastHeartbeatTick = -1000;

        function decide(obs) {
            var me = __seedfieldSelf(obs);
            if (!me || !me.hasComm) {
                return [];
            }

            if (obs.tick - __seedfieldLastHeartbeatTick < 60) {
                return [];
            }

            __seedfieldLastHeartbeatTick = obs.tick;
            return [{
                type: "send",
                entityId: me.entityId,
                toCivilizationId: obs.civilizationId,
                topic: "status",
                payload: JSON.stringify({ x: me.x, y: me.y })
            }];
        }
        """;

    private const string HarvesterBody = """
        function decide(obs) {
            var me = __seedfieldSelf(obs);
            if (!me) {
                return [];
            }

            var deposits = obs.deposits || [];
            var target = null;
            var bestDistance = 1e18;
            for (var i = 0; i < deposits.length; i++) {
                var deposit = deposits[i];
                if (deposit.remaining <= 0) {
                    continue;
                }
                var d = __seedfieldDistanceSq(me, deposit);
                if (d < bestDistance) {
                    bestDistance = d;
                    target = deposit;
                }
            }

            if (!target) {
                return [];
            }

            if (me.hasDrill &&
                __seedfieldDistance(me, target) <= me.drillRangeMm) {
                return [{ type: "mine", entityId: me.entityId }];
            }

            if (me.hasDrive) {
                return [{
                    type: "move",
                    entityId: me.entityId,
                    target: { x: target.x, y: target.y }
                }];
            }

            return [];
        }
        """;

    public const string IdleSource = Helpers + IdleBody;
    public const string ScoutSource = Helpers + ScoutBody;
    public const string SentrySource = Helpers + SentryBody;
    public const string RelaySource = Helpers + RelayBody;
    public const string HarvesterSource = Helpers + HarvesterBody;

    public static bool TryGetSource(string? programId, out string source)
    {
        switch (programId)
        {
            case IdleId:
                source = IdleSource;
                return true;
            case ScoutId:
                source = ScoutSource;
                return true;
            case SentryId:
                source = SentrySource;
                return true;
            case RelayId:
                source = RelaySource;
                return true;
            case HarvesterId:
                source = HarvesterSource;
                return true;
            default:
                source = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Chooses the least-specialized template for a design. It is advisory: only an
    /// explicit <c>ControllerProgramId</c> changes runtime behavior.
    /// </summary>
    public static string SuggestId(DeviceBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        return SuggestId(
            blueprint.Sensor is not null,
            blueprint.Weapon is not null,
            blueprint.Drive is not null,
            blueprint.Comm is not null,
            blueprint.Drill is not null);
    }

    public static string SuggestId(EntityState entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return SuggestId(
            entity.Sensor is not null,
            entity.Weapon is not null,
            entity.Drive is not null,
            entity.Comm is not null,
            entity.Drill is not null);
    }

    public static string SuggestId(
        bool sensor,
        bool weapon,
        bool drive,
        bool comm,
        bool drill = false)
    {
        if (drill)
        {
            return HarvesterId;
        }

        if (sensor && weapon)
        {
            return SentryId;
        }

        if (sensor && drive)
        {
            return ScoutId;
        }

        if (comm)
        {
            return RelayId;
        }

        return IdleId;
    }

    public static string ResolveSource(string? programId, string fallbackSource) =>
        TryGetSource(programId, out var source) ? source : fallbackSource;

    public static string ResolveSource(EntityState entity, string fallbackSource)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return ResolveSource(entity.ControllerProgramId, fallbackSource);
    }
}
