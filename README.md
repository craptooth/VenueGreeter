# Venue Greeter

Sends a `/tell` to anyone new who walks into your venue (house/apartment/FC
room), using a message template with `{name}` and `{link}` placeholders. Up
to 5 time-of-day slots let you swap out who/what those placeholders point to
(e.g. the current performer + their Twitch link) without touching the
message text itself.

## How the dedupe works

- Turning **Venue mode** on snapshots everyone currently in the instance as
  "already here" — they're never greeted.
- Anyone new who loads into the instance afterward gets a `/tell` once, then
  is remembered.
- If they leave and come back later in the same session, they're skipped —
  matching is by character name + home world, not by the game's internal
  object ID, specifically so it survives someone's character object being
  destroyed/recreated on zone-out/zone-in.
- Changing zones (e.g. you leave and re-enter your own house) resets the
  guest list automatically, since that's a new instance with a new set of
  occupants. **Reset guest list** in the settings window does the same thing
  manually, without needing to actually change zones (handy for starting a
  fresh event in the same session).

## Time-based variable sets

Each slot has a `StartTime` (24-hour `HH:mm`, your PC's local clock), a
`Name`, and a `Link`. At greet time, the plugin picks whichever slot's start
time is the latest one at or before the current time — so three slots at
`21:00`, `23:00`, and `01:00` mean: anyone arriving between 21:00–22:59 gets
the first performer, 23:00–00:59 the second, and 01:00 onward (wrapping past
midnight) the third, until 21:00 rolls around again.

Both the arrival greeting and the shout playlist (below) pull from this same
schedule — one place to update who's performing right now, and both
`{name}`/`{link}` in either feature stay in sync.

## Shout playlist

A separate, independent toggle from arrival greeting. Turn it on and it
cycles through a list of messages, sending one via `/shout` every N seconds
(configurable, floored at 5s in code so a mistyped delay can't turn it into
a rapid-fire spam loop), looping back to the first message after the last.
Each message can use the same `{name}`/`{link}` placeholders, resolved from
whichever time slot is currently active — so a single message like
`"Now on stage: {name} -- {link}"` automatically reflects whoever's
scheduled at the moment it fires, without needing a separate playlist entry
per performer.

Since `/shout` in a housing interior only reaches people already inside that
instance, this stays in the same "announcing to people who chose to be in
your space" category as the tell-based greeting — it's not broadcasting
outside the venue.

## Startup and saved settings

- Venue mode and the playlist always load switched **off**, whatever was
  saved. Logging in drops you into a public area, so neither feature ever
  resumes on its own after a restart or crash; you turn them on from the
  settings window when you're in your venue. This also keeps the plugin's
  constructor from touching the object table before login, which is what
  used to make it fail to load whenever a toggle had been left on.
- Config loading falls back to defaults if the file can't be read.
- Saved lists are replaced on load rather than appended to defaults.
- A blank time slot is only added when the schedule is completely empty.
  Fully blank slots left over from the old behavior are removed on load if
  at least one slot with real data exists; a slot with any name, link, or
  non-midnight start time is never touched.
- Settings are written about a second after you stop editing (and on
  unload) rather than on every keystroke, so a crash is much less likely to
  catch the file mid-write.

## Sending mechanics

- Uses ECommons' `Chat.SendMessage` to actually post the `/tell`, same
  approach as discussed for other plugins in this project — it's calling the
  game's own chatbox-send function, not raw packet injection.
- Sends are throttled (`GreetCooldownSeconds`, default 1.5s) and queued, so a
  burst of arrivals doesn't fire off a wall of tells in the same tick — both
  gentler on the people receiving them and less likely to look automated to
  any server-side spam heuristics.
- Long messages are chunked to fit under FFXIV's message length limit, with
  the full `/tell Name@World` prefix repeated on each chunk (tells don't
  support a "continue without re-targeting" shorthand the way local chat
  does).

## Things worth verifying on your first build

I don't have a Dalamud dev environment to compile against, so a few specific
calls are worth a close look if the build complains:

- `pc.HomeWorld.Value.Name.ExtractText()` — resolves a player's home world
  name via Lumina's `RowRef<World>`. The overall `RowRef<T>` shape
  (`.IsValid`, `.Value`, `.ValueNullable`) is confirmed current, but the
  exact method used to get a display string off the `World` row
  (`ExtractText()` vs `.ToString()` vs something else) is the one piece I
  couldn't fully confirm. It's wrapped in a try/catch that falls back to
  name-only targeting (works fine for same-world tells; cross-world tells
  need the world), so a wrong method name here degrades rather than breaks
  the plugin — but if you want cross-world greeting to work reliably, that's
  the line to check first.
- `ImGui.InputTextMultiline` / `ImGui.InputText` ref-string overloads —
  same caveat as the earlier chat-bridge plugin's UI file.

## Project layout

```
VenueGreeter/
  VenueGreeter/
    VenueGreeter.csproj   -- Dalamud.NET.Sdk project, manifest fields inline
    Plugin.cs             -- entry point
    Configuration.cs      -- message template, schedule, playlist, all persisted
    GreeterService.cs     -- arrival tracking, dedupe, /tell greeting
    PlaylistService.cs    -- looping /shout playlist on a timer
    VariableResolver.cs   -- shared time-slot selection + placeholder filling
    PluginUI.cs           -- settings window
```

## Building

Same as the chat-bridge plugin: .NET 10 SDK, `dotnet build` from the
`VenueGreeter/VenueGreeter/` folder, load as a dev plugin via `/xlplugins` →
Dev Tools. Open settings with `/venuegreeter`.

## A note on scope

This is built around "everyone who enters this specific private instance"
— not scanning for new players across public zones. If you ever wanted to
repurpose this for an open-world area instead of a house/venue instance,
that's a materially different thing (unsolicited messaging to strangers who
didn't choose to walk into your space), and I'd want to talk through that
separately rather than assume it's fine by extension.
