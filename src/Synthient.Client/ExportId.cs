using System.Globalization;

namespace Synthient;

/// <summary>
/// Identifies a parquet export: <see cref="Latest"/>, a UTC day as "YYYY-MM-DD", or a UTC hour as
/// "YYYY-MM-DD/H", where a zero-padded hour is also accepted. <see cref="Export.Id"/> values can be
/// passed as they are. An ID is checked when it is used, and an invalid one throws
/// <see cref="ArgumentException"/> before any request is sent.
/// </summary>
public readonly struct ExportId : IEquatable<ExportId>
{
    private readonly string? _value;

    /// <summary>Initializes an export ID from its string form.</summary>
    /// <param name="value">The ID, such as "latest", "2026-05-03", or "2026-05-03/7".</param>
    public ExportId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary>
    /// The feed's freshest export: the newest hourly export during the day, or the daily export once
    /// that day's hours are rolled up.
    /// </summary>
    public static ExportId Latest { get; } = new("latest");

    /// <summary>Returns the ID of the daily export for a UTC day.</summary>
    /// <param name="day">The UTC day.</param>
    /// <returns>The export ID.</returns>
    public static ExportId Daily(DateOnly day) => new(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    /// <summary>Returns the ID of the hourly export for an hour of a UTC day.</summary>
    /// <param name="day">The UTC day.</param>
    /// <param name="hour">The UTC hour, from 0 to 23.</param>
    /// <returns>The export ID.</returns>
    public static ExportId Hourly(DateOnly day, int hour)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hour);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hour, 23);
        return new(string.Create(CultureInfo.InvariantCulture, $"{day:yyyy-MM-dd}/{hour}"));
    }

    /// <summary>Compares two export IDs by their string form.</summary>
    /// <param name="left">The first ID.</param>
    /// <param name="right">The second ID.</param>
    /// <returns>Whether the IDs are equal.</returns>
    public static bool operator ==(ExportId left, ExportId right) => left.Equals(right);

    /// <summary>Compares two export IDs by their string form.</summary>
    /// <param name="left">The first ID.</param>
    /// <param name="right">The second ID.</param>
    /// <returns>Whether the IDs differ.</returns>
    public static bool operator !=(ExportId left, ExportId right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(ExportId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ExportId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <inheritdoc/>
    public override string ToString() => _value ?? "";

    // Returns the ID as a URL path, with the hour unpadded.
    internal string ToPath()
    {
        var value = _value ?? "";
        var parts = value.Split('/');
        var validDay = DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day);
        if (value == "latest" || (validDay && parts.Length == 1))
        {
            return value;
        }
        if (validDay
            && parts.Length == 2
            && parts[1].Length is 1 or 2
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            && hour <= 23)
        {
            return Hourly(day, hour).ToString();
        }
        throw new ArgumentException($"\"{_value}\" is not a valid export ID.");
    }
}
