using System.Globalization;

namespace Synthient;

/// <summary>
/// Builds the IDs of parquet exports. An export ID is <see cref="Latest"/>, a UTC day as
/// "YYYY-MM-DD", or a UTC hour as "YYYY-MM-DD/H", where a zero-padded hour is also accepted.
/// <see cref="Export.Id"/> values can be passed as they are. An invalid ID throws
/// <see cref="ArgumentException"/> before any request is sent.
/// </summary>
public static class ExportId
{
    /// <summary>
    /// The feed's freshest export: the newest hourly export during the day, or the daily export once
    /// that day's hours are rolled up.
    /// </summary>
    public const string Latest = "latest";

    /// <summary>Returns the ID of the daily export for a UTC day.</summary>
    public static string Daily(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Returns the ID of the hourly export for an hour of a UTC day.</summary>
    /// <param name="day">The UTC day.</param>
    /// <param name="hour">The UTC hour, from 0 to 23.</param>
    public static string Hourly(DateOnly day, int hour)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hour);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hour, 23);
        return string.Create(CultureInfo.InvariantCulture, $"{day:yyyy-MM-dd}/{hour}");
    }
}
