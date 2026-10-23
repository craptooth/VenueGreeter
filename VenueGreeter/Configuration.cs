using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VenueGreeter;

/// <summary>
/// One scheduled slot: at local-clock times &gt;= StartTime (until the next
/// slot's StartTime), {name} and {link} in the message template resolve to
/// this slot's Name/Link. e.g. an 9:00 PM slot for the opening DJ and an
/// 11:00 PM slot for a later performer.
/// </summary>
[Serializable]
public class VariableSet
{
    /// <summary>24-hour local time, "HH:mm", e.g. "21:00".</summary>
    public string StartTime = "00:00";

    public string Name = "";
    public string Link = "";
}

[Serializable]
public class Configuration : Dalamud.Configuration.IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool VenueModeEnabled = false;

    /// <summary>
    /// Use {name} and {link} as placeholders. Filled from whichever
    /// VariableSet is active for the current local time.
    /// </summary>
    public string MessageTemplate =
        "Welcome in! Tonight's performer is {name} -- catch the stream here: {link}";

    /// <summary>
    /// Up to 5 entries. Sorted by StartTime when picking the active one.
    ///
    /// ObjectCreationHandling.Replace matters here: Newtonsoft's default
    /// behavior is to reuse a pre-populated list and APPEND the loaded items
    /// to it, which would add one extra default entry every time the config
    /// is loaded and re-saved.
    /// </summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<VariableSet> VariableSets = new();

    /// <summary>Minimum seconds between individual /tell sends, to avoid bursts.</summary>
    public double GreetCooldownSeconds = 1.5;

    public bool PlaylistEnabled = false;

    /// <summary>
    /// Cycled through in order, looping back to the start. Same {name}/{link}
    /// placeholders as MessageTemplate, resolved from the same time-based
    /// VariableSets above. Replace (not append) on load, same reason as above.
    /// </summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> PlaylistMessages = new();

    private const string DefaultPlaylistMessage =
        "Now on stage: {name} -- catch the stream here: {link}";

    /// <summary>Seconds to wait between each playlist message. Floored at 5s in code regardless of this value.</summary>
    public double PlaylistDelaySeconds = 120;

    // --- Debounced saving ---------------------------------------------------
    // The settings UI calls Save() on every keystroke. Writing the JSON file
    // dozens of times while typing makes it much more likely that a game
    // crash lands in the middle of a write and leaves a truncated file. Save()
    // now just marks the config dirty; Plugin flushes it once edits have been
    // quiet for a moment (and once more on unload).

    [JsonIgnore] private bool _dirty;
    [JsonIgnore] private DateTime _lastChangeUtc = DateTime.MinValue;

    /// <summary>
    /// Run after loading. Makes sure the lists exist, drops fully blank
    /// schedule slots when real ones exist (leftovers from the old
    /// append-on-load bug), and seeds one starter entry only if a list is
    /// completely empty. Never touches a slot that has any data in it.
    /// </summary>
    public void Sanitize()
    {
        VariableSets ??= new List<VariableSet>();
        PlaylistMessages ??= new List<string>();

        static bool IsBlank(VariableSet s) =>
            string.IsNullOrWhiteSpace(s.Name) &&
            string.IsNullOrWhiteSpace(s.Link) &&
            (s.StartTime == "00:00" || string.IsNullOrWhiteSpace(s.StartTime));

        if (VariableSets.Exists(s => !IsBlank(s)))
            VariableSets.RemoveAll(IsBlank);

        if (VariableSets.Count == 0)
            VariableSets.Add(new VariableSet());
        if (PlaylistMessages.Count == 0)
            PlaylistMessages.Add(DefaultPlaylistMessage);
    }

    /// <summary>Marks the config as changed; the actual write happens shortly after edits stop.</summary>
    public void Save()
    {
        _dirty = true;
        _lastChangeUtc = DateTime.UtcNow;
    }

    /// <summary>Writes to disk if there are unsaved changes and nothing has been edited for <paramref name="quiet"/>.</summary>
    public void FlushIfDue(TimeSpan quiet)
    {
        if (!_dirty) return;
        if (DateTime.UtcNow - _lastChangeUtc < quiet) return;
        SaveNow();
    }

    /// <summary>Immediate write, e.g. on plugin unload.</summary>
    public void SaveNow()
    {
        if (!_dirty) return;
        _dirty = false;

        try
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to save Venue Greeter configuration.");
        }
    }
}
