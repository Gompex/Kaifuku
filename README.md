# Kaifuku v2

Skins: **Ledger**, **Heraldry** and **Signal** (pick one in Settings > Look > Skin), plus **Classic (v1)**.
Skin fonts are bundled under `Fonts/` (SIL Open Font License; licenses included).

HealBot-style click-to-heal party frames for FFXIV (Dalamud).

## For players
Install it, switch to a healer, and the frames appear next to your party list with bindings already set.
Nothing to configure. Everything is optional and lives in `/kaifuku config`:

- **Bindings**: 4 modifiers (none, Shift, Ctrl, Alt) x 5 mouse buttons (Left, Right, Middle, Mouse 4, Mouse 5).
  Click a square, pick a suggested skill or search every skill your job has.
- **Look**: frame size, text size, rounding, out-of-range fade, health colors by HP or by role, every color.
- **Layout**: frames per row, spacing, sort by party order / role / name, me first, group by role.
- **Display**: name style, health text (percent / current / missing), MP bar, shields, job icon,
  your buffs, debuffs, timers.
- **General**: lock/unlock, snap to party list, preview party, show on non-healer jobs.

`/kaifuku` shows or hides the frames.

## Building and testing
1. `dotnet build -c Release` in the `Kaifuku` folder (needs the .NET 10 SDK).
2. In game: `/xlsettings` > Experimental > **Dev Plugin Locations**, add the full path to `bin\x64\Release\Kaifuku.dll`.
3. `/xlplugins` > Dev Tools > Installed Dev Plugins, enable Kaifuku. After rebuilding, click reload there.

## Design rule
One click casts exactly one action on the frame you clicked. The plugin never chooses a target,
chooses a skill, or casts on its own. No sort-by-HP (frames moving under the cursor cause misclicks).

Distribution: combat-affecting plugins aren't accepted in the official Dalamud repo, so share it
through your own custom repository.

## License
Kaifuku is licensed under the GNU AGPL-3.0 (see LICENSE). Bundled fonts are under the SIL Open Font License (see Kaifuku/Fonts).
