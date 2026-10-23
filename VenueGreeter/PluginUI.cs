using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VenueGreeter;

public class PluginUI
{
    private readonly Plugin _plugin;
    public bool IsOpen;

    private const int MaxVariableSets = 5;

    public PluginUI(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Toggle() => IsOpen = !IsOpen;

    public void Draw()
    {
        if (!IsOpen) return;

        ImGui.SetNextWindowSize(new Vector2(560, 560), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Fang's Venue Greeter", ref IsOpen))
        {
            var cfg = _plugin.Config;

            var enabled = cfg.VenueModeEnabled;
            if (ImGui.Checkbox("Venue mode enabled", ref enabled))
            {
                cfg.VenueModeEnabled = enabled;
                cfg.Save();
                if (enabled) _plugin.Greeter.Start();
                else _plugin.Greeter.Stop();
            }

            ImGui.SameLine();
            if (ImGui.Button("Reset guest list"))
                _plugin.Greeter.ResetGuestList();
            ImGui.SameLine();
            ImGui.TextDisabled("(forget everyone seen so far, without changing zones)");

            ImGui.Separator();
            ImGui.Text("Message template (use {name} and {link}):");
            var template = cfg.MessageTemplate;
            if (ImGui.InputTextMultiline("##template", ref template, 1024, new Vector2(-1, 60)))
            {
                cfg.MessageTemplate = template;
                cfg.Save();
            }

            ImGui.Separator();
            ImGui.Text("Schedule (active set = latest start time <= now, local clock):");

            for (var i = 0; i < cfg.VariableSets.Count; i++)
            {
                var set = cfg.VariableSets[i];
                ImGui.PushID(i);

                ImGui.SetNextItemWidth(70);
                var start = set.StartTime;
                if (ImGui.InputText("##start", ref start, 5))
                {
                    set.StartTime = start;
                    cfg.Save();
                }
                ImGui.SameLine();
                ImGui.TextDisabled("HH:mm");

                ImGui.SameLine();
                ImGui.SetNextItemWidth(150);
                var name = set.Name;
                if (ImGui.InputText("##name", ref name, 64))
                {
                    set.Name = name;
                    cfg.Save();
                }
                ImGui.SameLine();
                ImGui.TextDisabled("name");

                ImGui.SameLine();
                ImGui.SetNextItemWidth(200);
                var link = set.Link;
                if (ImGui.InputText("##link", ref link, 256))
                {
                    set.Link = link;
                    cfg.Save();
                }
                ImGui.SameLine();
                ImGui.TextDisabled("link");

                ImGui.SameLine();
                if (ImGui.Button("X") && cfg.VariableSets.Count > 1)
                {
                    cfg.VariableSets.RemoveAt(i);
                    cfg.Save();
                    ImGui.PopID();
                    break; // list mutated, stop iterating this frame
                }

                ImGui.PopID();
            }

            if (cfg.VariableSets.Count < MaxVariableSets)
            {
                if (ImGui.Button("+ Add time slot"))
                {
                    cfg.VariableSets.Add(new VariableSet());
                    cfg.Save();
                }
            }
            else
            {
                ImGui.TextDisabled($"Maximum of {MaxVariableSets} time slots reached.");
            }

            ImGui.Separator();
            ImGui.Text("Shout playlist (loops on a timer, uses the same {name}/{link} schedule above):");

            var playlistEnabled = cfg.PlaylistEnabled;
            if (ImGui.Checkbox("Playlist enabled", ref playlistEnabled))
            {
                cfg.PlaylistEnabled = playlistEnabled;
                cfg.Save();
                if (playlistEnabled) _plugin.Playlist.Start();
                else _plugin.Playlist.Stop();
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            var delaySeconds = (float)cfg.PlaylistDelaySeconds;
            if (ImGui.DragFloat("Delay (sec)", ref delaySeconds, 1f, 5f, 3600f, "%.0f"))
            {
                cfg.PlaylistDelaySeconds = delaySeconds;
                cfg.Save();
            }

            for (var i = 0; i < cfg.PlaylistMessages.Count; i++)
            {
                ImGui.PushID(1000 + i); // offset to avoid clashing with the variable-set IDs above
                var msg = cfg.PlaylistMessages[i];
                ImGui.SetNextItemWidth(440);
                if (ImGui.InputText("##playlistmsg", ref msg, 400))
                {
                    cfg.PlaylistMessages[i] = msg;
                    cfg.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("X") && cfg.PlaylistMessages.Count > 1)
                {
                    cfg.PlaylistMessages.RemoveAt(i);
                    cfg.Save();
                    ImGui.PopID();
                    break; // list mutated, stop iterating this frame
                }
                ImGui.PopID();
            }

            if (ImGui.Button("+ Add playlist message"))
            {
                cfg.PlaylistMessages.Add("");
                cfg.Save();
            }

            ImGui.Separator();
            ImGui.Text("Activity:");
            ImGui.BeginChild("activity_log", new Vector2(0, 0), true);
            foreach (var line in _plugin.ActivityLog)
                ImGui.TextWrapped(line);
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                ImGui.SetScrollHereY(1.0f);
            ImGui.EndChild();
        }
        ImGui.End();
    }
}
