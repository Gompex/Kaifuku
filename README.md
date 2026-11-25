# Kaifuku (回復)

HealBot-style click-to-heal party frames for Final Fantasy XIV, as a Dalamud plugin.
*Kaifuku* means "recovery" in Japanese. Formerly known as HealFrames.

## Install

1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this URL, click **+**, make sure its checkbox is ticked, then **Save and Close**:

   ```
   https://raw.githubusercontent.com/Gompex/DalamudPlugins/main/repo.json
   ```

3. Type `/xlplugins`, search for **Kaifuku** in **All Plugins** and click **Install**.

Updates install automatically, like any other plugin. The same repo also lists my other plugins.

## Using it

- `/kaifuku` shows or hides the frames, `/kaifuku config` opens the settings, `/kaifuku guide` toggles the binding guide, `/kaifuku lock` locks or unlocks the frames.
- Switch to a healer and the frames appear next to your party list with bindings already set: click a frame to heal that member, with Shift, Ctrl, Alt and mouse buttons 4/5 for more skills.
- Three skins (Ledger, Heraldry, Signal) plus the Classic look, a plugin-wide scale slider, profiles per job or content, and a preview party to tune everything while solo.
- Shows raises on the way, removable debuffs, heal-to-full debuffs, tank busters, raidwides, aggro, cast bars, pets and more.

One click always casts exactly one action. Kaifuku never chooses a target or a skill, and never acts on its own.

## License

Kaifuku is licensed under the GNU AGPL-3.0 (see LICENSE). Bundled fonts are under the SIL Open Font License (see Kaifuku/Fonts).