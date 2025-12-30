namespace App.Common;

public static class Guard
{
    public static T NotNull<T>(T? value, string name) where T : class
        => value ?? throw new ArgumentNullException(name);

    public static string NotNullOrWhiteSpace(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value cannot be null/empty.", name);
        return value;
    }

    public static int InRange(int value, int min, int max, string name)
    {
        if (value < min || value > max) throw new ArgumentOutOfRangeException(name, value, $"Expected {min}..{max}");
        return value;
    }

    public static double InRange(double value, double min, double max, string name)
    {
        if (value < min || value > max) throw new ArgumentOutOfRangeException(name, value, $"Expected {min}..{max}");
        return value;
    }
}
