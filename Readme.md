# Infinitum Quests

**Version:** 1.8.1
**Author:** LemmyMaverick
**License:** MIT
**Game:** Rust (Oxide / uMod)

A contractor-style quest system with a tiered progression board, dynamic multi-objective contracts, daily streaks, chain quests, in-world Contractor NPCs, and a fully custom CUI.

---

## Features

- **Tiered rank progression** — five contractor ranks (Recruit → Legend) each unlocking more quest slots and higher-tier contracts
- **Quest board UI** — filterable by tier, category, and free-text search; window or fullscreen mode
- **All major objective types** — kills, gathering, crafting, looting, fishing, repairing, recycling, delivering, and external event completions
- **Daily quests** — reset at midnight UTC with configurable streak bonuses (up to +70% at 7-day streak)
- **Chain quests** — sequential multi-quest storylines with bonus rewards on chain completion
- **VIP rewards** — optional extra reward pool for players with the VIP permission
- **Contractor NPCs** — in-world scientists at Outpost, Bandit, Fishing Villages, and Barn; interact to open the board
- **HUD** — draggable mini overlay showing active contract progress; toggleable per player
- **Toast notifications** — on-screen pop-ups when objectives advance
- **Discord webhook** — broadcasts completions to a Discord channel
- **Leaderboard** — top N players by total completions
- **Admin panel** — in-game UI for viewing stats and managing player quest data
- **ImageLibrary support** — custom icons for kill objectives
- **External integrations** — Convoy, Harbor, Air Event, Junkyard, Supermarket, GasStation, ArcticBase, ArmoredTrain, BossMonster, RaidableBases, DungeonEvents, VirtualQuarries, InfinitumBradleyDrops, ZombieHunter

---

## Dependencies

| Plugin | Required | Purpose |
|--------|----------|---------|
| **Oxide / uMod** | Yes | Plugin framework |
| **ImageLibrary** | Optional | Custom kill objective icons |
| **Economics** | Optional | Economics currency rewards |
| **ServerRewards** | Optional | RP currency rewards |
| **SkillTree** | Optional | Skill XP rewards + level-up objective tracking |
| **ZombieHunter** | Optional | Zombie kill objective tracking |

All optional plugins can be enabled/disabled individually in the config.

---

## Installation

### Fresh Install

1. Copy `InfinitumQuests.cs` into `oxide/plugins/`
2. Start or reload the server (`oxide.reload InfinitumQuests`)
3. The plugin auto-generates:
   - `oxide/config/InfinitumQuests.json` — main config
   - `oxide/data/InfinitumQuests/quests/` — starter quest files (one per category)
   - `oxide/data/InfinitumQuests/players.json` — player progress (empty)
4. Grant permissions (see below)
5. Edit the config and quest files to suit your server
6. Run `oxide.reload InfinitumQuests` after editing quest files, or use `/quest admin` → Reload

### Migrating / Copying an Existing Setup

If you are moving the plugin to a new server, or restoring after a wipe, copy these data files:

```
oxide/data/InfinitumQuests/quests/           ← ALL your quest JSON files (keep through every wipe)
oxide/data/InfinitumQuests/contractor_positions.json  ← Saved NPC positions (keep through every wipe)
oxide/config/InfinitumQuests.json            ← Your config (keep through every wipe)
```

Only copy this if you are migrating player progress (do NOT copy on a fresh wipe):
```
oxide/data/InfinitumQuests/players.json      ← Player ranks, completions, cooldowns
```

### Wipe-Safe NPC Spawning
Starting in version 1.8.0, NPC positions are managed per-location. If no saved position exists for a monument in `contractor_positions.json`, the plugin will automatically fallback to a **Static Anchor** (Recycler, Vending Machine, or stable Monument NPC). This ensures your NPCs always spawn correctly on a fresh map without manual setup.


After copying, reload the plugin — it will detect and purge any orphaned active quests automatically.

---

## Permissions

| Permission | Description |
|-----------|-------------|
| `infinitumquests.use` | Access the quest board. Grant to the default group. |
| `infinitumquests.admin` | Open the admin panel and run admin subcommands. |
| `infinitumquests.vip` | Receive VIP bonus rewards on contract completion. |

```
oxide.grant group default infinitumquests.use
oxide.grant group vip     infinitumquests.vip
oxide.grant group admin   infinitumquests.admin
```

---

## Commands

### Player Commands

| Command | Description |
|---------|-------------|
| `/quest` `/quests` `/q` | Open the contract board |
| `/quest hud` | Toggle the mini HUD overlay |
| `/quest mode window` | Switch to windowed board UI |
| `/quest mode fullscreen` | Switch to fullscreen board UI |

### Admin Commands

| Command | Description |
|---------|-------------|
| `/quest admin` | Open the in-game admin panel |
| `/quest stats` | Show completion counts per quest in chat |

---

## Configuration (`oxide/config/InfinitumQuests.json`)

| Key | Default | Description |
|-----|---------|-------------|
| `Commands to open quest board` | `["quest","quests","q"]` | Chat commands that open the board |
| `HUD enabled` | `true` | Enable the mini HUD overlay |
| `Announce completions to server` | `true` | Broadcast completions in global chat |
| `Tier XP persists through wipe` | `true` | Whether contractor rank carries over on wipe |
| `UI accent color (hex, no #)` | `E8912B` | Theme color for the board header and badges |
| `Discord webhook URL` | `""` | Paste your Discord webhook to enable broadcasts |
| `Use Economics plugin` | `false` | Enable Economics reward type |
| `Use ServerRewards plugin` | `false` | Enable ServerRewards RP reward type |
| `Use SkillTree XP plugin` | `true` | Enable SkillTree XP rewards + level-up tracking |
| `Use ImageLibrary for item icons` | `true` | Load custom kill icons via ImageLibrary |
| `Streak bonus percent per day (stacks)` | `10` | Each consecutive daily adds this % to rewards |
| `Max streak bonus days (cap)` | `7` | Maximum days the streak bonus stacks |
| `Leaderboard top N players` | `20` | How many players appear on the leaderboard |
| `Chain completion announcement` | `true` | Broadcast to server when a chain is finished |
| `Show objective progress toasts` | `true` | On-screen pop-up when an objective advances |
| `Toast display duration (seconds)` | `3.5` | How long each toast is visible |
| `Play sound when objectives are complete` | `true` | Audio cue when all objectives are done |
| `Play sound on reward collect / tier-up` | `true` | Audio cue on reward collection or rank-up |

### Contractor NPC Settings

Contractor NPCs spawn automatically at monuments. Each spawn point uses an anchor entity within the monument (e.g. the recycler at Outpost) to determine the exact position. Positions can be overridden using the admin panel's **Set Position** tool.

| Key | Description |
|-----|-------------|
| `Enabled` | Spawn Contractor NPCs at configured monuments |
| `NPC display name` | Name shown above the NPC |
| `Clothing item shortnames` | Items dressed on the NPC (default: hazmat + bandana) |
| `Gesture` | Periodic gesture: `wave`, `thumbsup`, `shrug`, `clap`, `point`, `victory`, or `""` to disable |
| `Gesture interval in seconds` | How often the gesture plays |
| `Greeting sound effect path` | Sound played to the player who interacts |
| `Monument spawn points` | Array of monument filter + anchor entity pairs |

| Monument filter | Anchor | Notes |
|----------------|--------|-------|
| `outpost` | `recycler` | Standard fallback |
| `compound` | `recycler` | Standard fallback |
| `bandit` | `cardtable` | Spawns in gambling room |
| `fishing` | `vending` | Spawns on the shop pier |
| `barn` | `stablemaster` | Spawns beside horse vendor |
| `ranch` | `stablemaster` | Spawns beside horse vendor |

**Manual Override:** You can still set a precise position manually using `/iq.contractor setpos <filter>`. This position is saved to `oxide/data/InfinitumQuests/contractor_positions.json` and will persist until the next map wipe (or until cleared manually).

---

## Quest Files

Quest definitions are JSON files in `oxide/data/InfinitumQuests/quests/`. Each file contains an array of quest objects. You can have as many files as you like — all are loaded on startup and on reload.

### Quest Object Schema

```jsonc
{
  "Id":               "unique_quest_id",          // Required. Must be globally unique.
  "Title":            "Contract Title",
  "Description":      "Flavor text shown in the detail panel.",
  "Tier":             "Recruit",                  // Recruit | Operative | Specialist | Elite | Legend
  "Category":         "Gathering",                // Any string — used for board filter tabs
  "DifficultyStars":  1,                          // 1–5 stars shown in the board
  "Repeatable":       false,                      // Can be taken again after cooldown?
  "CooldownSeconds":  3600,                       // Cooldown before the quest can be repeated (0 = none)
  "VipCooldownSeconds": 1800,                     // Shorter cooldown for VIP players (0 = same as above)
  "TimeLimitMinutes": 60,                         // Time limit in minutes (0 = no limit)
  "Permission":       "",                         // Optional extra permission required to accept
  "ObjectiveLogic":   "ALL",                      // ALL = all objectives must complete | ANY = any one is enough
  "Daily":            false,                      // Resets daily at midnight UTC
  "Weekly":           false,                      // Resets weekly
  "ChainId":          "",                         // Group multiple quests into a chain with the same ChainId
  "ChainOrder":       0,                          // Position in the chain (0 = first, 1 = second, etc.)
  "ChainTitle":       "",                         // Display name shown for the overall chain
  "RequiredQuestIds": [],                         // Prerequisite quest IDs that must be completed first
  "Objectives":       [ /* see below */ ],
  "Rewards":          [ /* see below */ ],
  "VipRewards":       [ /* extra rewards for VIP players */ ],
  "ChainBonusRewards":[ /* awarded once when the full chain is complete */ ]
}
```

### Objective Types

| Type | Description | Target field | Notes |
|------|-------------|-------------|-------|
| `kill` | Kill an entity | Entity keyword (see list below) | Supports `HeadshotOnly`, `TimeCondition` |
| `chop` | Gather wood by chopping | `wood` | |
| `mine` | Mine a resource node | `stones`, `metal.ore`, `sulfur.ore`, etc. | |
| `gather` | Collect any resource | Item shortname | |
| `craft` | Craft an item | Item shortname | |
| `loot` | Loot a container | Container prefab keyword (`barrel`, `crate_normal`, etc.) | |
| `recycle` | Recycle items | Item shortname | |
| `fish` | Catch fish | Fish item shortname or `fish` | |
| `pickup` | Pick up a dropped item | Item shortname | |
| `harvest` | Harvest a plant | Plant/food shortname | |
| `repair` | Repair an entity | Item shortname or `building` | |
| `deliver` | Deliver items to a Contractor NPC | Item shortname | `Location` filters to a specific monument |
| `event_win` | Complete a server event | `convoy`, `harbor`, `air`, `junkyard`, `supermarket`, `gasstation`, `arcticbase`, `armoredtrain`, or `""` for any |  |
| `boss_kill` | Kill a BossMonster entity | Boss prefab name or `""` for any | Requires BossMonster plugin |
| `raidable_base` | Complete a Raidable Base | Difficulty: `0`–`4` or `""` for any | Requires RaidableBases plugin |
| `dungeon_win` | Complete a Dungeon Event | Dungeon map name or `""` for any | Requires DungeonEvents plugin |
| `quarry_upgrade` | Upgrade a Virtual Quarry | Quarry profile name or `""` | Requires VirtualQuarries plugin |
| `quarry_place` | Place a Virtual Quarry | Quarry profile name or `""` | Requires VirtualQuarries plugin |
| `skilltree_level` | Reach a SkillTree level | Target level number as string (e.g. `"25"`) | Requires SkillTree plugin |
| `bradley_tier` | Destroy a tiered Bradley | Bradley tier profile name or `""` for any | Requires InfinitumBradleyDrops |
| `zombie` | Kill a ZombieHunter zombie | `zombie` or `zombie_hunter` | Requires ZombieHunter plugin |

**Objective schema:**
```jsonc
{
  "Type":          "kill",
  "Target":        "scientist",
  "Count":         10,
  "Description":   "Kill 10 scientists",
  "HeadshotOnly":  false,       // kill only: count headshots only
  "TimeCondition": "",          // "day", "night", or "" for any time
  "Location":      ""           // deliver only: monument filter, e.g. "outpost"
}
```

**Common kill targets:**

| Target keyword | Matches |
|---------------|---------|
| `scientist` | Standard scientists |
| `heavyscientist` | Heavy scientists |
| `tunneldweller` | Tunnel dwellers |
| `underwaterdweller` | Underwater dwellers |
| `npc` | Any NPC / scientist |
| `wolf` | Wolves |
| `bear` | Bears |
| `boar` | Boars |
| `stag` | Stags / deer |
| `chicken` | Chickens |
| `horse` | Horses |
| `croc` | Crocodiles |
| `panther` | Panthers |
| `tiger` | Tigers |
| `animal` | Any animal |
| `bradley` / `bradleyapc` | Bradley APC |
| `heli` / `patrolheli` | Patrol helicopter |
| `attack_heli` | Attack helicopter |
| `ch47` | Chinook CH47 |
| `player` | Human players (PvP servers) |

### Reward Types

| Type | Fields | Description |
|------|--------|-------------|
| `item` | `Shortname`, `Amount`, `SkinId`, `CustomName` | Give a physical item |
| `blueprint` | `Shortname`, `Amount` | Give an item as a learned blueprint |
| `tier_xp` | `Amount` | Award Contractor rank XP |
| `reputation` | `Amount` | Award Reputation points (tracked internally) |
| `economics` | `Amount` | Award Economics currency |
| `server_rewards` | `Amount` | Award ServerRewards RP |
| `skill_xp` | `Amount` | Award SkillTree XP |
| `command` | `Command` | Run a server console command. Use `{steamid}` as a placeholder for the player's Steam ID. |

**Reward schema:**
```jsonc
{
  "Type":       "item",
  "Shortname":  "scrap",
  "Amount":     200,
  "SkinId":     0,
  "CustomName": ""
}
```

---

## Contractor Rank System

Contractor XP is earned from `tier_xp` rewards. Ranks unlock more active quest slots.

| Rank | XP Required | Quest Slots |
|------|------------|-------------|
| RECRUIT | 0 | 3 |
| OPERATIVE | 500 | 4 |
| SPECIALIST | 1,500 | 5 |
| ELITE | 3,500 | 6 |
| LEGEND | 7,000 | 8 |

Rank XP can be configured to persist through wipes (`"Tier XP persists through wipe": true`).

---

## Daily Streak System

Each day a player completes at least one daily quest, their streak increments. Streak bonuses apply as a percentage increase to all rewards on that day's completions.

- Default: +10% per streak day, capped at 7 days (+70% maximum)
- Streak resets if no daily is completed on a given day
- Configurable via `Streak bonus percent per day` and `Max streak bonus days`

---

## Example Quest File

```json
[
  {
    "Id": "kill_scientists_operative",
    "Title": "Clear the Monuments",
    "Description": "Scientists have been sighted. The network needs them cleared.",
    "Tier": "Operative",
    "Category": "Combat",
    "DifficultyStars": 2,
    "Repeatable": true,
    "CooldownSeconds": 7200,
    "VipCooldownSeconds": 3600,
    "TimeLimitMinutes": 0,
    "ObjectiveLogic": "ALL",
    "Objectives": [
      {
        "Type": "kill",
        "Target": "scientist",
        "Count": 15,
        "Description": "Kill 15 scientists"
      }
    ],
    "Rewards": [
      { "Type": "item",      "Shortname": "scrap",    "Amount": 300 },
      { "Type": "tier_xp",  "Amount": 150 },
      { "Type": "reputation","Amount": 50 }
    ],
    "VipRewards": [
      { "Type": "item", "Shortname": "scrap", "Amount": 100 }
    ]
  },
  {
    "Id": "chain_story_1",
    "Title": "Into the Network — Part 1",
    "Description": "Prove yourself by gathering the basics.",
    "Tier": "Recruit",
    "Category": "Story",
    "DifficultyStars": 1,
    "ChainId": "story_intro",
    "ChainOrder": 0,
    "ChainTitle": "Into the Network",
    "Objectives": [
      { "Type": "chop", "Target": "wood", "Count": 1000, "Description": "Chop 1000 wood" }
    ],
    "Rewards": [
      { "Type": "tier_xp", "Amount": 80 }
    ]
  },
  {
    "Id": "chain_story_2",
    "Title": "Into the Network — Part 2",
    "Description": "The network needs your combat skills too.",
    "Tier": "Recruit",
    "Category": "Story",
    "DifficultyStars": 2,
    "ChainId": "story_intro",
    "ChainOrder": 1,
    "ChainTitle": "Into the Network",
    "RequiredQuestIds": ["chain_story_1"],
    "Objectives": [
      { "Type": "kill", "Target": "scientist", "Count": 5, "Description": "Kill 5 scientists" }
    ],
    "Rewards": [
      { "Type": "tier_xp", "Amount": 120 }
    ],
    "ChainBonusRewards": [
      { "Type": "item", "Shortname": "supply.signal", "Amount": 1 }
    ]
  }
]
```

---

## Wipe Handling

### Keep through every wipe
- `oxide/config/InfinitumQuests.json`
- `oxide/data/InfinitumQuests/quests/*.json`
- `oxide/data/InfinitumQuests/contractor_positions.json`

### Wipe monthly (full wipe)
- `oxide/data/InfinitumQuests/players.json`

### Wipe weekly (optional — resets progress but keeps ranks)
- Not recommended unless you want to clear cooldowns; rank XP is in `players.json`

The plugin automatically purges orphaned active quests (quests in progress that no longer have a definition) on every load, so it is safe to add or remove quest files between wipes.

---

## Changelog

| Version | Notes |
|---------|-------|
| 1.8.1 | Fixed compile error on older Oxide builds; Reverted incompatible CuiItemIconComponent; Implemented ImageLibrary bridge for async skinned reward icons; Optimized Bandit/Ranch offsets. |
| 1.8.0 | **Wipe-Safe Spawn Refactor:** NPCs now independently fallback to static anchors (vending, recyclers, stablemasters) if no saved pos exists. Added support for skinned item rewards in the UI. Added Auto-Cleanup for orphan NPCs. |
| 1.7.9 | HUD move mode, board search filter, UI state preserved across reloads |
| 1.6.5 | Chain quests, VIP rewards, streak bonuses |
| 1.5.x | Contractor NPC system, monument spawn anchors |
| 1.4.x | External event integrations (Convoy, RaidableBases, DungeonEvents, etc.) |
| 1.3.x | Admin panel, leaderboard, Discord webhook |
| 1.0.0 | Initial release |
