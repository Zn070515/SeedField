using System.Text;
using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunReplayRecord(string path)
    {
        var world = BuildCombatWorld(
            hitChance: 60,
            damageVariance: 40,
            lootChance: 100,
            lootMaterial: 5,
            targetHealth: 100);
        var engine = new SimulationEngine(world);
        var recorder = new ReplayRecorder(world);
        for (var tick = 0; tick < 20; tick++)
        {
            var intents = new[] { Attack() };
            engine.Step(intents);
            recorder.Record(world, tick, intents);
        }

        var json = recorder.Save();
        File.WriteAllText(path, json, Encoding.UTF8);
        Console.WriteLine(
            $"REPLAY_RECORDED path={path} ticks={recorder.Count} " +
            $"bytes={Encoding.UTF8.GetByteCount(json)} " +
            $"hash=0x{WorldStateHasher.Compute(world):x16}");
        return 0;
    }

    private static int RunReplayPlay(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Replay file not found: {path}");
            return 2;
        }

        var data = ReplaySerializer.Load(File.ReadAllText(path, Encoding.UTF8));
        var result = ReplayPlayer.Play(data);
        if (result.Success)
        {
            Console.WriteLine(
                $"REPLAY_OK ticks={result.TickCount} events={result.EventCount} " +
                $"hash=0x{result.FinalHash:x16}");
            return 0;
        }

        Console.WriteLine(
            $"REPLAY_DIVERGED tick={result.DivergedTick} " +
            $"hash=0x{result.FinalHash:x16}");
        return 1;
    }
}
