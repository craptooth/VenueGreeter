# Fang's Venue Greeter

Sends a `/tell` to anyone new who walks into your venue (house/apartment/FC
room), using a message template with `{name}` and `{link}` placeholders. Up
to 5 time-of-day slots let you swap out who/what those placeholders point to
(e.g. the current performer + their Twitch link) without touching the
message text itself.

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

## Sending mechanics

- Sends are throttled (1.5s) and queued, so a burst of arrivals doesn't
  fire off a wall of tells in the same tick — both gentler on the people
  receiving them and less likely to look automated to any server-side spam
  heuristics.
- Long messages are chunked to fit under FFXIV's message length limit, with
  the full `/tell Name@World` prefix repeated on each chunk (tells don't
  support a "continue without re-targeting" shorthand the way local chat
  does).

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
