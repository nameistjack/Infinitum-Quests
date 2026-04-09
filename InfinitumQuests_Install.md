# InfinitumQuests — Install / Update Guide

---

## Fresh Install

1. Copy the plugin:
   ```
   oxide/plugins/InfinitumQuests.cs
   ```

2. Start or reload the server:
   ```
   oxide.reload InfinitumQuests
   ```
   The plugin auto-generates the config and default quest files on first load.

3. Grant permissions:
   ```
   oxide.grant group default infinitumquests.use
   oxide.grant group vip     infinitumquests.vip
   oxide.grant group admin   infinitumquests.admin
   ```

---

## Updating an Existing Install

> **Always back up the data folder before making any changes.**
> ```
> oxide/data/InfinitumQuests/   →   backup somewhere safe
> ```

### Step 1 — Copy the plugin

Replace the existing file:
```
oxide/plugins/InfinitumQuests.cs
```

### Step 2 — Copy the config (if updated)

Replace the existing file:
```
oxide/config/InfinitumQuests.json
```

> If you have custom settings (theme color, webhook URL, icon URLs, NPC clothing), re-apply them after copying.

### Step 3 — Replace quest files (if updated)

Copy the updated quest JSON files into:
```
oxide/data/InfinitumQuests/quests/
```

You can overwrite existing files or add new ones. The plugin loads every `.json` file in that folder on startup.

> **Do not delete these files — they are not quest definitions:**
> ```
> oxide/data/InfinitumQuests/contractor_positions.json   ← saved NPC positions
> oxide/data/InfinitumQuests/players.json                ← all player progress
> ```
> Deleting `contractor_positions.json` resets all manually placed NPC positions.  
> Deleting `players.json` wipes all player rank XP, completions, streaks, and reputation.

### Step 4 — Reload

```
oxide.reload InfinitumQuests
```

Orphaned active quests (from any removed quest IDs) are purged automatically on load.

---

## File Reference

| File | Copy on update | Notes |
|------|---------------|-------|
| `oxide/plugins/InfinitumQuests.cs` | Yes | Plugin source |
| `oxide/config/InfinitumQuests.json` | Yes (re-apply custom settings) | Main config |
| `oxide/data/InfinitumQuests/quests/*.json` | Yes | Quest definitions |
| `oxide/data/InfinitumQuests/contractor_positions.json` | **Never overwrite** | Saved NPC positions |
| `oxide/data/InfinitumQuests/players.json` | **Never overwrite** | Player progress |
