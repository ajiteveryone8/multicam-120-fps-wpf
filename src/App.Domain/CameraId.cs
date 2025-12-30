namespace App.Domain;

public readonly record struct CameraId(string Value)
{
    public override string ToString() => Value;
    public static CameraId From(string value) => new(value.Trim());
}
