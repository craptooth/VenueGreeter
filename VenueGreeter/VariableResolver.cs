using System;
using System.Linq;

namespace VenueGreeter;

/// <summary>
/// Shared by GreeterService (per-arrival /tell) and PlaylistService (looping
/// /shout) so both pick the same "active" name/link pair for a given moment
/// and fill {name}/{link} placeholders the same way.
/// </summary>
public static class VariableResolver
{
    /// <summary>
    /// Picks whichever VariableSet's StartTime is the latest one at or before
    /// the current local time, wrapping around midnight if the clock is
    /// earlier than every configured slot (i.e. still within the last slot
    /// from the previous day).
    /// </summary>
    public static VariableSet GetActiveVariableSet(Configuration cfg)
    {
        var sets = cfg.VariableSets;
        if (sets.Count == 0) return new VariableSet();

        var now = DateTime.Now.TimeOfDay;
        var ordered = sets
            .Select(s => (Set: s, Time: ParseTime(s.StartTime)))
            .OrderBy(x => x.Time)
            .ToList();

        var active = ordered.LastOrDefault(x => x.Time <= now);
        return active.Set ?? ordered[^1].Set; // before the earliest slot -> use the last (wraps from previous day)
    }

    public static string FillTemplate(string template, VariableSet variables)
    {
        return template
            .Replace("{name}", variables.Name)
            .Replace("{link}", variables.Link);
    }

    private static TimeSpan ParseTime(string hhmm)
    {
        return TimeSpan.TryParse(hhmm, out var t) ? t : TimeSpan.Zero;
    }
}
