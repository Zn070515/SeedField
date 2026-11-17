namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private void EvaluateControlPoints(long tick, List<SimulationEvent> events)
    {
        if (World.ControlPoints.Count == 0 ||
            World.Match?.IsFinished == true)
        {
            return;
        }

        foreach (var point in World.ControlPoints.Values)
        {
            if (point.IsCaptured ||
                !World.Regions.TryGetValue(point.RegionId, out var region))
            {
                continue;
            }

            var present = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var entity in region.Entities.Values)
            {
                if (entity.Position.IsWithinRange(point.Position, point.RadiusMm))
                {
                    present.Add(entity.CivilizationId);
                }
            }

            if (present.Count != 1)
            {
                point.HoldingCivilizationId = null;
                point.HoldTicks = 0;
                continue;
            }

            var civilizationId = present.Min!;
            if (string.Equals(
                    point.HoldingCivilizationId,
                    civilizationId,
                    StringComparison.Ordinal))
            {
                point.HoldTicks += 1;
            }
            else
            {
                point.HoldingCivilizationId = civilizationId;
                point.HoldTicks = 1;
            }

            if (point.HoldTicks < point.HoldTicksRequired)
            {
                continue;
            }

            point.OwnerCivilizationId = civilizationId;
            events.Add(new ControlPointCaptured(
                tick,
                point.Id,
                civilizationId,
                point.HoldTicks));
        }

        if (World.Match is not { IsFinished: false } match)
        {
            return;
        }

        var captured = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var point in World.ControlPoints.Values)
        {
            if (point.OwnerCivilizationId is { } owner)
            {
                captured[owner] = captured.GetValueOrDefault(owner) + 1;
            }
        }

        foreach (var pair in captured)
        {
            if (pair.Value < match.Rules.CapturedPointsToWin)
            {
                continue;
            }

            match.WinnerCivilizationId = pair.Key;
            events.Add(new MatchEnded(tick, pair.Key, null, pair.Value));
            break;
        }
    }

}
