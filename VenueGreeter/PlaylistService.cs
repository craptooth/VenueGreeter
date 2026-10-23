using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using ECommons.Automation;

namespace VenueGreeter;

/// <summary>
/// Cycles through a list of shout messages on a timer, filling {name}/{link}
/// from the same time-based VariableSets the greeter uses. Independent
/// on/off switch from venue-mode arrival tracking — you can run one without
/// the other.
/// </summary>
public class PlaylistService : IDisposable
{
    private readonly Plugin _plugin;
    private DateTime _nextSendUtc = DateTime.MaxValue;
    private int _index;

    /// <summary>Hard floor regardless of configured delay, so a 0/typo'd value can't turn this into a spam loop.</summary>
    private const double MinDelaySeconds = 5;

    public bool IsRunning { get; private set; }

    public PlaylistService(Plugin plugin)
    {
        _plugin = plugin;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Start()
    {
        if (_plugin.Config.PlaylistMessages.Count == 0)
        {
            _plugin.LogActivity("[playlist] No messages configured -- nothing to play.");
            return;
        }

        _index = 0;
        IsRunning = true;
        _nextSendUtc = DateTime.UtcNow; // fire the first message on the next tick
        _plugin.LogActivity("[playlist] Started.");
    }

    public void Stop()
    {
        IsRunning = false;
        _nextSendUtc = DateTime.MaxValue;
        _plugin.LogActivity("[playlist] Stopped.");
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!IsRunning) return;
        if (DateTime.UtcNow < _nextSendUtc) return;

        var messages = _plugin.Config.PlaylistMessages;
        if (messages.Count == 0)
        {
            Stop();
            return;
        }

        SendNext(messages);

        var delay = Math.Max(MinDelaySeconds, _plugin.Config.PlaylistDelaySeconds);
        _nextSendUtc = DateTime.UtcNow.AddSeconds(delay);
    }

    private void SendNext(List<string> messages)
    {
        var template = messages[_index % messages.Count];
        _index = (_index + 1) % messages.Count;

        var variables = VariableResolver.GetActiveVariableSet(_plugin.Config);
        var message = VariableResolver.FillTemplate(template, variables);

        const int maxLen = 480; // headroom under FFXIV's ~500 char limit
        for (var i = 0; i < message.Length; i += maxLen)
        {
            var chunk = message.Substring(i, Math.Min(maxLen, message.Length - i));
            try
            {
                Chat.SendMessage($"/shout {chunk}");
            }
            catch (Exception ex)
            {
                _plugin.LogActivity($"[error] Failed to shout: {ex.Message}");
            }
        }

        _plugin.LogActivity($"[playlist] Shouted: {message}");
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }
}
