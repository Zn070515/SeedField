namespace SeedField.Headless;

internal static partial class Program
{
    private static int ReadIntFlag(string[] args, string flag, int fallback)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length &&
               int.TryParse(args[index + 1], out var value)
            ? value
            : fallback;
    }

    private static double ReadDoubleFlag(string[] args, string flag, double fallback)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length &&
               double.TryParse(
                   args[index + 1],
                   System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var value)
            ? value
            : fallback;
    }
}
