using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunMessageAclDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddEntity(Sender(1, "A", "r0"))
            .AddEntity(Sender(2, "B", "r0"))
            .AddEntity(Sender(3, "C", "r0"))
            .AddEntity(Sender(4, "D", "r1"))
            .AddMessagePolicy(
                "B",
                allowAllInbound: false,
                allowedCivilizations: new[] { "A" },
                allowAllTopics: false,
                allowedTopics: new[] { "diplomacy" })
            .AddMessagePolicy(
                "C",
                allowAllInbound: false,
                allowedCivilizations: new[] { "D" })
            .Build();
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "B", "diplomacy", "hello"),
            new SendMessageIntent(new EntityId(1), "B", "trade", "offer"),
            new SendMessageIntent(new EntityId(1), "C", "diplomacy", "hello"),
            new SendMessageIntent(new EntityId(1), "D", "diplomacy", "hello", "r1"),
        });

        var codes = result.IntentResults.Select(r => r.ErrorCode).ToArray();
        var senderAllowed = codes[0] == SimulationErrorCode.None;
        var senderDenied = codes[2] == SimulationErrorCode.MessageDenied;
        var topicAllowed = codes[0] == SimulationErrorCode.None;
        var topicDenied = codes[1] == SimulationErrorCode.MessageDenied;
        var noRoute = codes[3] == SimulationErrorCode.NoRoute;
        var energy = world.FindEntity(new EntityId(1))!.Energy;

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var hashOk = WorldStateHasher.Compute(world) == WorldStateHasher.Compute(loaded);

        var ok = senderAllowed &&
                 senderDenied &&
                 topicAllowed &&
                 topicDenied &&
                 noRoute &&
                 energy == 99 &&
                 world.GetInbox("B").Count == 1 &&
                 world.GetInbox("C").Count == 0 &&
                 hashOk;

        Console.WriteLine(
            $"MESSAGE_ACL senderAllowed={senderAllowed} senderDenied={senderDenied} " +
            $"topicAllowed={topicAllowed} topicDenied={topicDenied} noRoute={noRoute} " +
            $"energy={energy} inboxB={world.GetInbox("B").Count} " +
            $"inboxC={world.GetInbox("C").Count} hashOk={hashOk} ok={ok}");

        return ok ? 0 : 1;
    }

}
