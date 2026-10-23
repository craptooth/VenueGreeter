using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons.Automation;

namespace VenueGreeter;

public class GreeterService : IDisposable
{
    private readonly Plugin _plugin;

    /// <summary>
    /// Keys already accounted for (greeted OR present when tracking started),
    /// keyed by "Name@World" — stable across a character's game-object being
    /// destroyed/recreated (e.g. after they leave and re-enter the instance),
    /// unlike EntityId which is not guaranteed stable.
    /// </summary>
    private readonly HashSet<string> _seen = new();

    private readonly Queue<PendingGreet> _pending = new();
    private DateTime _lastSendUtc = DateTime.MinValue;

    public bool IsTracking { get; private set; }

    public GreeterService(Plugin plugin)
    {
        _plugin = plugin;
        Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    /// <summary>Turn tracking on: snapshot everyone currently present as "already here" (not greeted).</summary>
    public void Start()
    {
        _seen.Clear();
        _pending.Clear();
        foreach (var key in GetCurrentPlayerKeys())
            _seen.Add(key);
        IsTracking = true;
        _plugin.LogActivity($"[greeter] Tracking started. {_seen.Count} guest(s) already present, ignored.");
    }

    public void Stop()
    {
        IsTracking = false;
        _pending.Clear();
        _plugin.LogActivity("[greeter] Tracking stopped.");
    }

    /// <summary>Manually forget everyone seen so far, without leaving/re-entering the zone.</summary>
    public void ResetGuestList()
    {
        if (!IsTracking) return;
        Start();
    }

    private void OnTerritoryChanged(uint territoryType)
    {
        if (IsTracking)
            Start(); // fresh venue instance -> fresh guest list
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!IsTracking) return;

        ScanForNewArrivals();
        ProcessSendQueue();
    }

    private void ScanForNewArrivals()
    {
        var localKey = GetKey(Plugin.ObjectTable.LocalPlayer);

        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is not IPlayerCharacter pc) continue;
            if (obj.ObjectKind != ObjectKind.Pc) continue;

            var key = GetKey(pc);
            if (key is null || key == localKey) continue;
            if (!_seen.Add(key)) continue; // already known -> HashSet.Add returns false

            _pending.Enqueue(new PendingGreet(pc.Name.TextValue, GetWorldName(pc)));
            _plugin.LogActivity($"[greeter] New arrival: {key}");
        }
    }

    private void ProcessSendQueue()
    {
        if (_pending.Count == 0) return;

        var cooldown = TimeSpan.FromSeconds(Math.Max(0.5, _plugin.Config.GreetCooldownSeconds));
        if (DateTime.UtcNow - _lastSendUtc < cooldown) return;

        var greet = _pending.Dequeue();
        _lastSendUtc = DateTime.UtcNow;
        SendGreeting(greet);
    }

    private void SendGreeting(PendingGreet greet)
    {
        var variables = VariableResolver.GetActiveVariableSet(_plugin.Config);
        var message = VariableResolver.FillTemplate(_plugin.Config.MessageTemplate, variables);

        var target = string.IsNullOrEmpty(greet.World) ? greet.Name : $"{greet.Name}@{greet.World}";

        const int maxLen = 400; // headroom under FFXIV's ~500 char limit, minus target prefix
        for (var i = 0; i < message.Length; i += maxLen)
        {
            var chunk = message.Substring(i, Math.Min(maxLen, message.Length - i));
            try
            {
                Chat.SendMessage($"/tell {target} {chunk}");
            }
            catch (Exception ex)
            {
                _plugin.LogActivity($"[error] Failed to greet {target}: {ex.Message}");
            }
        }
    }

    private IEnumerable<string> GetCurrentPlayerKeys()
    {
        var localKey = GetKey(Plugin.ObjectTable.LocalPlayer);
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is not IPlayerCharacter pc) continue;
            if (obj.ObjectKind != ObjectKind.Pc) continue;
            var key = GetKey(pc);
            if (key is null || key == localKey) continue;
            yield return key;
        }
    }

    private static string? GetKey(IPlayerCharacter? pc)
    {
        if (pc is null) return null;
        var name = pc.Name.TextValue;
        if (string.IsNullOrEmpty(name)) return null;
        return $"{name}@{GetWorldName(pc)}";
    }

    private static string GetWorldName(IPlayerCharacter pc)
    {
        try
        {
            return pc.HomeWorld.IsValid ? pc.HomeWorld.Value.Name.ExtractText() : "";
        }
        catch
        {
            // Lumina row shape can shift between API levels; fall back to
            // name-only targeting (works for same-world tells) rather than
            // crashing the scan loop.
            return "";
        }
    }

    public void Dispose()
    {
        Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    private readonly record struct PendingGreet(string Name, string World);
}
