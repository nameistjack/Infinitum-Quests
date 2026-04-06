// ============================================================
//  Infinitum Quests
//  Author  : LemmyMaverick
//  Version : 1.6.5
//  License : MIT
//
//  MIT License
//  Copyright (c) 2026 LemmyMaverick
//
//  Permission is hereby granted, free of charge, to any person obtaining
//  a copy of this software and associated documentation files (the
//  "Software"), to deal in the Software without restriction, including
//  without limitation the rights to use, copy, modify, merge, publish,
//  distribute, sublicense, and/or sell copies of the Software, and to
//  permit persons to whom the Software is furnished to do so, subject to
//  the following conditions:
//
//  The above copyright notice and this permission notice shall be included
//  in all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//  IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//  CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//  TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//  SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//  Developed with the assistance of Claude (Anthropic) — AI pair programming.
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Infinitum Quests", "LemmyMaverick", "1.8.1")]
    [Description("Contractor-style quest system — tiered board, dynamic objectives, and progression.")]
    class InfinitumQuests : RustPlugin
    {
        // ─────────────────────── Constants ────────────────────────────────────
        private static InfinitumQuests Instance;

        private const string UI_MAIN   = "IQ_Main";
        private const string UI_HUD    = "IQ_HUD";
        private const string UI_TOAST  = "IQ_Toast";
        private const string UI_ADMIN  = "IQ_Admin";

        private const string PERM_USE   = "infinitumquests.use";
        private const string PERM_ADMIN = "infinitumquests.admin";
        private const string PERM_VIP   = "infinitumquests.vip";

        // Tier advancement is driven by TierXP earned from quest rewards (type "tier_xp").
        // Thresholds: Recruit→Operative 500, →Specialist 1500, →Elite 3500, →Legend 7000
        private static readonly int[]    TierThresholds = { 0, 500, 1500, 3500, 7000 };
        private static readonly string[] TierNames      = { "RECRUIT", "OPERATIVE", "SPECIALIST", "ELITE", "LEGEND" };

        // Tier colors — each rank tells a story:
        //   Recruit   → slate gray    (unproven, no rank)
        //   Operative → field green   (cleared for duty)
        //   Specialist→ precision blue(technical, skilled)
        //   Elite     → amber gold    ( Infinitum brand — intentional)
        //   Legend    → imperial violet(beyond the network)
        private static readonly string[] TierColors = {
            "0.540 0.540 0.560 1",   // RECRUIT   — slate #898990
            "0.168 0.756 0.352 1",   // OPERATIVE — field green #2BC159
            "0.168 0.548 0.840 1",   // SPECIALIST— precision blue #2B8CD7
            "0.910 0.620 0.168 1",   // ELITE     — amber gold #E89E2B (= Infinitum brand)
            "0.680 0.168 0.840 1",   // LEGEND    — imperial violet #AD2BD7
        };
        private static readonly string[] TierBg = {
            "0.068 0.068 0.072 1",   // RECRUIT
            "0.032 0.102 0.052 1",   // OPERATIVE
            "0.032 0.068 0.120 1",   // SPECIALIST
            "0.118 0.068 0.012 1",   // ELITE
            "0.082 0.028 0.110 1",   // LEGEND
        };
        // Row backgrounds for the tier progression panel — same hues, very subtle transparent tint
        private static readonly string[] TierRowBg = {
            "0.068 0.068 0.072 0.18",  // RECRUIT   — subtle slate tint
            "0.032 0.102 0.052 0.18",  // OPERATIVE — subtle green tint
            "0.032 0.068 0.120 0.18",  // SPECIALIST— subtle blue tint
            "0.118 0.068 0.012 0.18",  // ELITE     — subtle amber tint
            "0.082 0.028 0.110 0.18",  // LEGEND    — subtle violet tint
        };
        private static readonly int[]    TierSlots      = { 3, 4, 5, 6, 8 };

        // ─── Infinitum Palette ────────────────────────────────────────────────
        // Backgrounds — warm gunmetal hierarchy (slightly warm to avoid cold blue)
        private const string C_BG0 = "0.038 0.036 0.042 0.82";  // deepest / scrim
        private const string C_BG1 = "0.062 0.060 0.070 0.62";  // main panel (+ blur)
        private const string C_BG2 = "0.082 0.080 0.092 0.72";  // header / footer strip
        private const string C_BG3 = "0.098 0.096 0.112 0.66";  // left sidebar
        private const string C_BG4 = "0.112 0.110 0.128 0.68";  // card row even
        private const string C_BG5 = "0.124 0.122 0.142 0.70";  // card row odd
        private const string C_SEL = "0.048 0.112 0.058 0.78";  // selected row bg
        private const string C_DIV = "0.150 0.148 0.172 1";     // divider / separator (keep crisp)

        // Text
        private const string C_TXT_HI = "0.940 0.940 0.956 1"; // near-white primary
        private const string C_TXT_MD = "0.800 0.800 0.830 1"; // light-gray secondary
        private const string C_TXT_DM = "0.520 0.520 0.560 1"; // dimmed / disabled

        // Status
        private const string C_OK     = "0.168 0.756 0.352 1"; // #2BC259  complete / positive
        private const string C_OK_BG  = "0.030 0.140 0.060 1"; // #071F0E  success bg
        private const string C_ERR    = "0.840 0.200 0.200 1"; // #D73333  locked / danger
        private const string C_ERR_BG = "0.148 0.030 0.030 1"; // #260707  danger bg
        private const string C_WRN    = "0.820 0.620 0.100 1"; // #D19E1A  cooldown / warning
        private const string C_INF    = "0.168 0.548 0.840 1"; // #2B8CD7  in-progress info

        // Button fills
        private const string C_BTN    = "0.142 0.142 0.168 0.72"; // generic dark button
        private const string C_BTN_HI = "0.175 0.175 0.205 0.78"; // button hover / raised

        private int RowsPerPageWin => config.RowsPerPageWindow;
        private int RowsPerPageFs  => config.RowsPerPageFullscreen;
        private const float ROW_H              = 44f;

        private readonly Dictionary<ulong, PlayerData> _players     = new Dictionary<ulong, PlayerData>();
        private readonly List<QuestDefinition>          _quests      = new List<QuestDefinition>();
        private readonly Dictionary<ulong, UiState>     _ui          = new Dictionary<ulong, UiState>();
        private readonly Dictionary<ulong, string>      _hudHash     = new Dictionary<ulong, string>(); // HUD rebuild skip cache
        private readonly HashSet<ulong>                 _hudMoveMode  = new HashSet<ulong>();
        private readonly Dictionary<ulong, int>         _hudMoveStep  = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, Timer>        _toastTimers  = new Dictionary<ulong, Timer>();
        private readonly HashSet<ulong>                    _zombieHunterIds  = new HashSet<ulong>();            // net IDs of active ZombieHunter zombies
        private readonly Dictionary<ulong, ulong>          _recyclerUser    = new Dictionary<ulong, ulong>();  // recycler netId → userID of player currently using it
        private readonly Dictionary<ulong, float>          _lastRepair      = new Dictionary<ulong, float>();  // entity netId → UnityEngine.Time.realtimeSinceStartup of last awarded repair
        private readonly List<ScientistNPC>                 _contractorNpcs    = new List<ScientistNPC>();
        private readonly List<MapMarkerGenericRadius>       _contractorMarkers = new List<MapMarkerGenericRadius>();
        private readonly List<Timer>                        _contractorTimers  = new List<Timer>();
        private Dictionary<string, ContractorSavedPos>     _contractorPos     = new Dictionary<string, ContractorSavedPos>(StringComparer.OrdinalIgnoreCase);
        private bool  _dataDirty;   // true when player data has unsaved changes

        // Fallback item-icon map used ONLY when no URL is configured for a kill target.
        // Target keyword → native item shortname whose icon is shown instead.
        private static readonly Dictionary<string, string> _killIconFallback =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["animal"]         = "skull.wolf",
            ["bear"]           = "skull.wolf",
            ["wolf"]           = "skull.wolf",
            ["boar"]           = "skull.wolf",
            ["stag"]           = "skull.wolf",
            ["chicken"]        = "skull.wolf",
            ["horse"]          = "skull.wolf",
            ["deer"]           = "skull.wolf",
            ["croc"]           = "skull.wolf",
            ["panther"]        = "skull.wolf",
            ["npc"]              = "skull.human",
            ["scientist"]        = "skull.human",
            ["heavyscientist"]   = "skull.human",
            ["heavy"]            = "skull.human",
            ["tunneldweller"]    = "skull.human",
            ["tunnel"]           = "skull.human",
            ["underwaterdweller"]= "skull.human",
            ["underwater"]       = "skull.human",
            ["junkyard"]         = "skull.human",
            ["scarecrow"]        = "skull.human",
            ["player"]           = "skull.human",
            ["bradley"]              = "ammo.rocket.basic",
            ["bradleyapc"]           = "ammo.rocket.basic",
            ["heli"]                 = "ammo.rocket.hv",
            ["attack_heli"]          = "ammo.rocket.hv",
            ["patrolheli"]           = "ammo.rocket.hv",
            ["ch47"]                 = "ammo.rocket.mlrs",
            ["autoturret"]           = "autoturret",
            ["autoturret_deployed"]  = "autoturret",
            ["samsite"]              = "autoturret",
            ["sam_site_turret_deployed"] = "autoturret",
        };

        // Returns the ImageLibrary key used to store a kill icon for the given target.
        // Keys are prefixed "iq_kill_" to avoid clashing with item shortnames in ImageLibrary.
        private static string KillIconKey(string target) => $"iq_kill_{target.ToLower()}";

        private static Configuration config;
        private Timer _hudTimer;
        private Timer _saveTimer;

        [PluginReference] Plugin Economics, ServerRewards, SkillTree, ImageLibrary, ZombieHunter;

        // ─────────────────────── Configuration ────────────────────────────────
        private class Configuration
        {
            [JsonProperty("Config Version")]
            public string ConfigVersion { get; set; } = "";

            [JsonProperty("Commands to open quest board")]
            public List<string> OpenCommands { get; set; } = new List<string> { "quest", "quests", "q" };

            [JsonProperty("HUD enabled")]
            public bool HudEnabled { get; set; } = true;

            [JsonProperty("Announce completions to server")]
            public bool AnnounceCompletions { get; set; } = true;

            [JsonProperty("Tier XP persists through wipe")]
            public bool TierXpPersistsThroughWipe { get; set; } = true;

            [JsonProperty("UI accent color (hex, no #)")]
            public string ThemeColor { get; set; } = "E8912B";

            [JsonProperty("Discord webhook URL")]
            public string DiscordWebhook { get; set; } = "";

            [JsonProperty("Use Economics plugin")]
            public bool UseEconomics { get; set; } = false;

            [JsonProperty("Use ServerRewards plugin")]
            public bool UseServerRewards { get; set; } = false;

            [JsonProperty("Use SkillTree XP plugin")]
            public bool UseSkillTree { get; set; } = true;

            [JsonProperty("Use ImageLibrary for item icons")]
            public bool UseImageLibrary { get; set; } = true;

            [JsonProperty("Streak bonus percent per day (stacks)")]
            public int StreakBonusPercent { get; set; } = 10;

            [JsonProperty("Max streak bonus days (cap)")]
            public int MaxStreakDays { get; set; } = 7;

            [JsonProperty("Leaderboard top N players")]
            public int LeaderboardSize { get; set; } = 20;

            [JsonProperty("Chain completion announcement")]
            public bool AnnounceChains { get; set; } = true;

            [JsonProperty("Show objective progress toasts")]
            public bool ShowProgressToasts { get; set; } = true;

            [JsonProperty("Toast display duration (seconds)")]
            public float ToastDuration { get; set; } = 3.5f;

            [JsonProperty("Quest list rows per page — windowed mode")]
            public int RowsPerPageWindow { get; set; } = 8;

            [JsonProperty("Quest list rows per page — fullscreen mode (10 = safe for 720p, 11 = 768p+, 13+ = 1080p+)")]
            public int RowsPerPageFullscreen { get; set; } = 11;

            [JsonProperty("Play sound when objectives are complete")]
            public bool PlayObjectivesSound { get; set; } = true;

            [JsonProperty("Objectives complete sound prefab")]
            public string ObjectivesSoundPrefab { get; set; } =
                "assets/prefabs/misc/halloween/lootbag/effects/loot_bag_upgrade.prefab";

            [JsonProperty("Play sound on reward collect / tier-up")]
            public bool PlayCompleteSound { get; set; } = true;

            [JsonProperty("Reward collect sound prefab")]
            public string CompleteSoundPrefab { get; set; } =
                "assets/prefabs/misc/halloween/lootbag/effects/gold_open.prefab";

            [JsonProperty("External event integrations")]
            public EventIntegrationConfig EventIntegrations { get; set; } = new EventIntegrationConfig();

            [JsonProperty("Contractor NPC settings")]
            public ContractorNpcConfig ContractorNpcs { get; set; } = new ContractorNpcConfig();

            // Maps kill objective target keyword → image URL.
            // ImageLibrary downloads and caches these in oxide/data/ImageLibrary/ automatically.
            // Key  = the Target value used in quest objectives (e.g. "wolf", "bear", "bradley")
            // Value= direct image URL (imgur, Discord CDN, etc.) — leave blank to fall back to item icon
            [JsonProperty("Kill objective icon URLs (target → image URL, requires ImageLibrary)")]
            public Dictionary<string, string> KillIconUrls { get; set; } = new Dictionary<string, string>
            {
                ["tiger"]         = "https://wiki.rustclash.com/img/screenshots/tiger.png",
                ["wolf"]           = "https://wiki.rustclash.com/img/screenshots/wolf.png",
                ["bear"]           = "https://wiki.rustclash.com/img/screenshots/bear.png",
                ["boar"]           = "https://wiki.rustclash.com/img/screenshots/boar.png",
                ["stag"]           = "https://wiki.rustclash.com/img/screenshots/stag.png",  
                ["chicken"]        = "https://wiki.rustclash.com/img/screenshots/chicken.png",
                ["horse"]          = "https://wiki.rustclash.com/img/screenshots/horse.png",
                ["deer"]           = "https://wiki.rustclash.com/img/screenshots/stag.png",
                ["croc"]           = "https://wiki.rustclash.com/img/screenshots/croc.png",
                ["panther"]        = "https://wiki.rustclash.com/img/screenshots/panther.png",
                ["npc"]            = "https://wiki.rustclash.com/img/screenshots/npc.png",
                ["scientist"]      = "https://i.imgur.com/2oK58Iy.png",
                ["heavyscientist"] = "https://i.imgur.com/THOBLOC.png",
                ["player"]         = "https://i.imgur.com/q4UZ5oq.png",
                ["bradley"]        = "https://wiki.rustclash.com/img/screenshots/bradleyapc.png",
                ["bradleyapc"]     = "https://wiki.rustclash.com/img/screenshots/bradleyapc.png",
                ["heli"]           = "https://wiki.rustclash.com/img/screenshots/helicopter.png",
                ["patrolheli"]     = "https://wiki.rustclash.com/img/screenshots/helicopter.png",
                ["attack_heli"]    = "",
                ["ch47"]           = "",
                ["animal"]         = "https://cdn-icons-png.flaticon.com/128/616/616412.png",
            };
        }

        private class ContractorNpcConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty("NPC display name")]
            public string DisplayName { get; set; } = "Contractor";

            [JsonProperty("Clothing item shortnames (leave empty to keep default scientist appearance)")]
            public List<string> Clothing { get; set; } = new List<string>
                { "hazmatsuit", "mask.bandana" };

            [JsonProperty("Gesture to play periodically (wave, thumbsup, shrug, clap, point, victory — empty to disable)")]
            public string Gesture { get; set; } = "wave";

            [JsonProperty("Gesture interval in seconds")]
            public float GestureInterval { get; set; } = 20f;

            [JsonProperty("Greeting sound effect path (played only to the interacting player — empty to disable)")]
            public string GreetingSound { get; set; } = "assets/prefabs/misc/vending_machine/effects/vending-machine-purchase-human.prefab";

            [JsonProperty("Monument spawn points")]
            public List<ContractorSpawnPoint> SpawnPoints { get; set; } = new List<ContractorSpawnPoint>
            {
                new ContractorSpawnPoint { MonumentFilter = "outpost",  AnchorEntity = "recycler",     SideOffset = 1.5f },
                new ContractorSpawnPoint { MonumentFilter = "compound", AnchorEntity = "recycler",     SideOffset = 1.5f },
                new ContractorSpawnPoint { MonumentFilter = "bandit",   AnchorEntity = "cardtable",    SideOffset = 2.0f },
                new ContractorSpawnPoint { MonumentFilter = "fishing",  AnchorEntity = "vending",     SideOffset = 1.5f },
                new ContractorSpawnPoint { MonumentFilter = "barn",     AnchorEntity = "stablemaster", SideOffset = 1.5f },
                new ContractorSpawnPoint { MonumentFilter = "ranch",    AnchorEntity = "stablemaster", SideOffset = 1.5f },
            };

            [JsonProperty("Auto-clear saved positions on wipe (wipe-safe NPCs without manual 'iq.contractor clear')")]
            public bool AutoClearPositionsOnWipe { get; set; } = false;
        }

        private class ContractorSpawnPoint
        {
            [JsonProperty("Monument filter (partial name match, lowercase)")]
            public string MonumentFilter { get; set; } = "";

            [JsonProperty("Anchor entity prefab keyword (recycler, workbench, fish_trap — empty = monument center)")]
            public string AnchorEntity { get; set; } = "recycler";

            [JsonProperty("Side offset in metres from anchor entity")]
            public float SideOffset { get; set; } = 1.5f;

            [JsonProperty("Y offset (height above ground)")]
            public float YOffset { get; set; } = 0.5f;

            [JsonProperty("Clothing item shortnames (overrides global Clothing — empty list uses global)")]
            public List<string> Clothing { get; set; } = new List<string>();
        }

        private class EventIntegrationConfig
        {
            [JsonProperty("Track event wins (Convoy, Harbor, Air, Junkyard, Supermarket, GasStation, ArcticBase, ArmoredTrain)")]
            public bool TrackEventWins       = true;
            [JsonProperty("Track boss kills (BossMonster)")]
            public bool TrackBossKills       = true;
            [JsonProperty("Track raidable base completions (RaidableBases)")]
            public bool TrackRaidableBases   = true;
            [JsonProperty("Track dungeon completions (DungeonEvents)")]
            public bool TrackDungeonEvents   = true;
            [JsonProperty("Track virtual quarry events (VirtualQuarries)")]
            public bool TrackVirtualQuarries = true;
            [JsonProperty("Track SkillTree level-ups")]
            public bool TrackSkillTreeLevels = true;
            [JsonProperty("Track Bradley tier kills (InfinitumBradleyDrops)")]
            public bool TrackBradleyTiers    = true;
            [JsonProperty("Track ZombieHunter kills (ZombieHunter)")]
            public bool TrackZombieHunter    = true;
        }

        protected override void LoadDefaultConfig() => config = new Configuration { ConfigVersion = Version.ToString() };

        protected override void LoadConfig()
        {
            base.LoadConfig();
            bool save = false;
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                if (config.OpenCommands == null) { config.OpenCommands = new List<string> { "quest", "quests", "q" }; save = true; }

                // Deduplicate OpenCommands (bug: repeated saves can create duplicates)
                var distinctCmds = new List<string>();
                foreach (var c in config.OpenCommands)
                    if (!distinctCmds.Contains(c)) distinctCmds.Add(c);
                if (distinctCmds.Count != config.OpenCommands.Count) { config.OpenCommands = distinctCmds; save = true; }

                // Deduplicate ContractorNpc SpawnPoints by MonumentFilter+AnchorEntity key
                if (config.ContractorNpcs?.SpawnPoints != null)
                {
                    var seen = new HashSet<string>();
                    var distinctPts = new List<ContractorSpawnPoint>();
                    foreach (var sp in config.ContractorNpcs.SpawnPoints)
                    {
                        string key = $"{sp.MonumentFilter}|{sp.AnchorEntity}";
                        if (seen.Add(key)) distinctPts.Add(sp);
                    }
                    if (distinctPts.Count != config.ContractorNpcs.SpawnPoints.Count)
                    { config.ContractorNpcs.SpawnPoints = distinctPts; save = true; }
                }

                string ver = Version.ToString();
                if (config.ConfigVersion != ver) { config.ConfigVersion = ver; save = true; }
            }
            catch { LoadDefaultConfig(); save = true; }
            if (save) SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        // ─────────────────────── Quest Definitions ────────────────────────────
        private class QuestDefinition
        {
            [JsonProperty("Id")]               public string Id               = "";
            [JsonProperty("Title")]            public string Title            = "Unnamed Quest";
            [JsonProperty("Description")]      public string Description      = "";
            [JsonProperty("Tier")]             public string Tier             = "Recruit";
            [JsonProperty("Category")]         public string Category         = "General";
            [JsonProperty("DifficultyStars")]  public int    DifficultyStars  = 1;
            [JsonProperty("Repeatable")]       public bool   Repeatable       = false;
            [JsonProperty("CooldownSeconds")]  public int    CooldownSeconds  = 0;
            [JsonProperty("TimeLimitMinutes")] public int    TimeLimitMinutes = 0;
            [JsonProperty("Permission")]       public string Permission       = "";
            [JsonProperty("ObjectiveLogic")]   public string ObjectiveLogic   = "ALL";
            [JsonProperty("Objectives")]       public List<ObjectiveDef> Objectives  = new List<ObjectiveDef>();
            [JsonProperty("Rewards")]          public List<RewardDef>    Rewards     = new List<RewardDef>();
            [JsonProperty("RequiredQuestIds")] public List<string>       RequiredIds = new List<string>();
            [JsonProperty("Daily")]            public bool   Daily            = false;
            [JsonProperty("Weekly")]           public bool   Weekly           = false;
            [JsonProperty("ChainId")]         public string ChainId          = "";
            [JsonProperty("ChainOrder")]      public int    ChainOrder       = 0;
            [JsonProperty("ChainTitle")]      public string ChainTitle       = "";
            [JsonProperty("ChainBonusRewards")]   public List<RewardDef> ChainBonusRewards   = new List<RewardDef>();
            [JsonProperty("VipCooldownSeconds")]  public int             VipCooldownSeconds  = 0;
            [JsonProperty("VipRewards")]          public List<RewardDef> VipRewards          = new List<RewardDef>();
        }

        private class ObjectiveDef
        {
            [JsonProperty("Type")]          public string Type          = "";
            [JsonProperty("Target")]        public string Target        = "";
            [JsonProperty("Count")]         public int    Count         = 1;
            [JsonProperty("Description")]   public string Description   = "";
            // Optional modifiers
            [JsonProperty("HeadshotOnly")]  public bool   HeadshotOnly  = false;  // kill: only headshot kills count
            [JsonProperty("TimeCondition")] public string TimeCondition = "";     // "night", "day", or "" for any
            [JsonProperty("Location")]      public string Location      = "";     // deliver: contractor location filter (e.g. "outpost", "bandit") — empty = any
        }

        private class RewardDef
        {
            [JsonProperty("Type")]       public string Type       = "item";
            [JsonProperty("Shortname")]  public string Shortname  = "";
            [JsonProperty("Amount")]     public int    Amount     = 1;
            [JsonProperty("SkinId")]     public ulong  SkinId     = 0;
            [JsonProperty("CustomName")] public string CustomName = "";
            [JsonProperty("Command")]    public string Command    = "";
        }

        // ─────────────────────── Player Data ──────────────────────────────────
        private class PlayerData
        {
            [JsonProperty("N")]  public string DisplayName  = "";
            [JsonProperty("XP")] public int    TierXP       = 0;
            [JsonProperty("RP")] public int    Reputation   = 0;
            [JsonProperty("HX")] public int    HudX         = 0;
            [JsonProperty("HY")] public int    HudY         = 0;
            [JsonProperty("HV")] public bool   HudVisible   = true;
            [JsonProperty("UM")] public string UiMode       = "window";
            [JsonProperty("SK")] public int    Streak          = 0;
            [JsonProperty("SD")] public string LastDailyDate   = "";
            [JsonProperty("DW")] public string DailyWindowStart = "";
            [JsonProperty("DC")] public List<string> DailyCompleted  = new List<string>();
            [JsonProperty("CC")] public List<string> CompletedChains = new List<string>();
            [JsonProperty("AQ")] public List<ActiveQuest>    ActiveQuests    = new List<ActiveQuest>();
            [JsonProperty("RQ")] public List<ActiveQuest>    ReadyToCollect  = new List<ActiveQuest>();
            [JsonProperty("CQ")] public List<CompletedRecord> Completed      = new List<CompletedRecord>();
            [JsonProperty("CD")] public Dictionary<string, string> Cooldowns = new Dictionary<string, string>();

            public int TotalCompletions()
            {
                int n = 0;
                for (int i = 0; i < Completed.Count; i++) n += Completed[i].Times;
                return n;
            }
        }

        private class ActiveQuest
        {
            [JsonProperty("Id")] public string   Id             = "";
            [JsonProperty("At")] public string   AcceptedAt     = "";
            [JsonProperty("Ex")] public string   ExpiresAt      = "";
            [JsonProperty("Pg")] public List<int> Progress      = new List<int>();
            [JsonProperty("DN")] public ulong    DeliveryNpcId  = 0;  // entity UID of assigned delivery NPC (0 = none)
        }

        private class ContractorSavedPos
        {
            [JsonProperty("x")]  public float X;
            [JsonProperty("y")]  public float Y;
            [JsonProperty("z")]  public float Z;
            [JsonProperty("ry")] public float RotationY;        // yaw in degrees (admin facing direction)
            [JsonProperty("name")] public string Name = "";

            [JsonIgnore] public Vector3    Position => new Vector3(X, Y, Z);
            [JsonIgnore] public Quaternion Rotation => Quaternion.Euler(0f, RotationY, 0f);
        }

        private class CompletedRecord
        {
            [JsonProperty("Id")] public string Id    = "";
            [JsonProperty("At")] public string At    = "";
            [JsonProperty("T")]  public int    Times = 1;
        }

        // ─────────────────────── UI State ─────────────────────────────────────
        private class UiState
        {
            public string Tab    = "board";
            public int    Page   = 0;
            public string Tier   = "";
            public string Cat    = "";
            public string Detail = "";
            public string Search = ""; // board search filter
            public ulong SelectedPlayer = 0;
            public string Mode   = "window"; // "window" | "fullscreen"
            // Admin panel state
            public string AdminTab       = "stats";
            public int    AdminPage      = 0;
            public ulong  AdminSelPlayer = 0;
        }

        // ─────────────────────── Lang ─────────────────────────────────────────
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"]       = "<color=#E8912B>[Quests]</color> You don't have permission.",
                ["BoardEmpty"]         = "<color=#E8912B>[Quests]</color> No quests available right now.",
                ["QuestAccepted"]      = "<color=#E8912B>[Quests]</color> Contract accepted: <color=#fff>{0}</color>",
                ["QuestComplete"]      = "<color=#E8912B>[Quests]</color> Contract complete: <color=#E8912B>{0}</color>  Rewards collected.",
                ["QuestAbandoned"]     = "<color=#E8912B>[Quests]</color> Contract abandoned: {0}",
                ["QuestAlreadyActive"] = "<color=#E8912B>[Quests]</color> That contract is already active.",
                ["QuestCompleted"]     = "<color=#E8912B>[Quests]</color> You already completed that contract.",
                ["QuestOnCooldown"]    = "<color=#E8912B>[Quests]</color> Contract on cooldown — available in {0}.",
                ["QuestTierLocked"]    = "<color=#E8912B>[Quests]</color> You need {0} tier to take that contract.",
                ["QuestPrereqMissing"] = "<color=#E8912B>[Quests]</color> Prerequisites not met for that contract.",
                ["QuestSlotsFull"]     = "<color=#E8912B>[Quests]</color> You have too many active contracts. Complete one first.",
                ["QuestNotFound"]      = "<color=#E8912B>[Quests]</color> Quest not found.",
                ["QuestExpired"]       = "<color=#E8912B>[Quests]</color> Contract expired: {0}",
                ["Announce"]           = "<color=#E8912B>[Quests]</color> <color=#fff>{0}</color> completed a contract: <color=#E8912B>{1}</color>",
                ["TierUp"]             = "<color=#E8912B>[Quests]</color> Contractor rank up!  You are now <color=#E8912B>{0}</color>.",
                ["ReloadedQuests"]     = "<color=#E8912B>[Quests]</color> Reloaded {0} quest definitions.",
                ["DailyReset"]        = "<color=#E8912B>[Quests]</color> Daily contracts have reset!",
                ["StreakExtended"]    = "<color=#E8912B>[Quests]</color> Streak extended! Day <color=#E8912B>{0}</color>  (+{1}% reward bonus)",
                ["StreakBroken"]      = "<color=#E8912B>[Quests]</color> Streak reset. New streak started.",
                ["ChainComplete"]     = "<color=#E8912B>[Quests]</color> <color=#fff>{0}</color> completed chain: <color=#E8912B>{1}</color>!",
                ["ChainBonusAwarded"] = "<color=#E8912B>[Quests]</color> Chain bonus rewards collected!",
                ["QuestReady"]        = "<color=#E8912B>[Quests]</color> Objectives complete: <color=#2BC259>{0}</color>  — open your contracts to collect rewards.",
                ["QuestCollected"]      = "<color=#E8912B>[Quests]</color> Rewards collected: <color=#E8912B>{0}</color>",
                ["VipBonusCollected"]   = "<color=#E8912B>[Quests]</color>  <color=#FFD700>★ VIP bonus rewards collected!</color>",
                ["DeliveryUnknownItem"] = "<color=#E8912B>[Quests]</color> Unknown item in quest config — contact an admin.",
                ["DeliveryNeedItems"]   = "<color=#E8912B>[Quests]</color> You need {0}x {1} (you have {2}).",
                ["DeliveryAccepted"]    = "<color=#E8912B>[Quests]</color> Delivery accepted — {0}x {1}.",
                ["HudShown"]           = "<color=#E8912B>[Quests]</color> Contract HUD <color=#2BC259>shown</color>.",
                ["HudHidden"]          = "<color=#E8912B>[Quests]</color> Contract HUD <color=#D73333>hidden</color>.  Type <color=#fff>/quest hud</color> to show again.",
            }, this);
        }

        private string Msg(string key, string id = null, params object[] args)
        {
            string m = lang.GetMessage(key, this, id);
            return args.Length > 0 ? string.Format(m, args) : m;
        }

        // ─────────────────────── Data IO ──────────────────────────────────────
        private void LoadPlayerData()
        {
            _players.Clear();
            var loaded = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("InfinitumQuests/players");
            if (loaded != null)
                foreach (var kv in loaded) _players[kv.Key] = kv.Value;

            PurgeOrphanedQuests();
        }

        // Remove active/ready quests whose definition no longer exists (quest file deleted/renamed).
        // Called after both player data and quest definitions are loaded.
        private void PurgeOrphanedQuests()
        {
            if (_quests.Count == 0) return; // no definitions loaded yet — skip (called again after load)
            var knownIds = new HashSet<string>(_quests.Select(q => q.Id), StringComparer.OrdinalIgnoreCase);
            int total = 0;
            foreach (var data in _players.Values)
            {
                int before = data.ActiveQuests.Count + data.ReadyToCollect.Count;
                data.ActiveQuests.RemoveAll(aq => !knownIds.Contains(aq.Id));
                data.ReadyToCollect.RemoveAll(aq => !knownIds.Contains(aq.Id));
                total += before - (data.ActiveQuests.Count + data.ReadyToCollect.Count);
            }
            if (total > 0)
            {
                _dataDirty = true;
                Puts($"[InfinitumQuests] Purged {total} orphaned active quest(s) with no matching definition.");
            }
        }

        private void SavePlayerData() =>
            Interface.Oxide.DataFileSystem.WriteObject("InfinitumQuests/players", _players);

        private void LoadContractorPositions()
        {
            var loaded = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ContractorSavedPos>>("InfinitumQuests/contractor_positions");
            _contractorPos = loaded ?? new Dictionary<string, ContractorSavedPos>(StringComparer.OrdinalIgnoreCase);
        }

        private void SaveContractorPositions() =>
            Interface.Oxide.DataFileSystem.WriteObject("InfinitumQuests/contractor_positions", _contractorPos);

        private PlayerData GetOrCreate(BasePlayer player)
        {
            if (!_players.TryGetValue(player.userID, out var d))
            {
                d = new PlayerData { DisplayName = player.displayName };
                _players[player.userID] = d;
            }
            else d.DisplayName = player.displayName;
            return d;
        }

        private void LoadQuestDefinitions()
        {
            _quests.Clear();
            string dir = Path.Combine(Interface.Oxide.DataDirectory, "InfinitumQuests", "quests");
            if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }

            var files = Directory.GetFiles(dir, "*.json");
            if (files.Length == 0)
            {
                Puts("[InfinitumQuests] No quest files found — writing starter quests to data/InfinitumQuests/quests/");
                WriteStarterQuests(dir);
                files = Directory.GetFiles(dir, "*.json");
            }

            var validRewardTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "item", "tier_xp", "skill_xp", "reputation", "economics", "server_rewards", "command" };

            foreach (var file in files)
            {
                try
                {
                    var list = JsonConvert.DeserializeObject<List<QuestDefinition>>(File.ReadAllText(file));
                    if (list == null) continue;
                    foreach (var q in list)
                    {
                        if (string.IsNullOrEmpty(q.Id))
                        { PrintWarning($"[IQ] Quest in {Path.GetFileName(file)} has no Id — skipped."); continue; }
                        // Validate reward types
                        foreach (var r in q.Rewards.Concat(q.VipRewards).Concat(q.ChainBonusRewards))
                            if (!validRewardTypes.Contains(r.Type))
                                PrintWarning($"[IQ] Quest '{q.Id}': unknown reward type '{r.Type}' — will be ignored at runtime.");
                        _quests.Add(q);
                    }
                }
                catch (Exception ex) { PrintWarning($"[IQ] Failed to load {Path.GetFileName(file)}: {ex.Message}"); }
            }
            Puts($"[InfinitumQuests] Loaded {_quests.Count} quest definitions from {files.Length} file(s).");
            PurgeOrphanedQuests(); // clean up any active quests whose definition no longer exists
        }

        // Writes one example quest per major category so admins have a working starting point.
        private void WriteStarterQuests(string dir)
        {
            var starters = new Dictionary<string, string>
            {
                ["daily"] = @"[
  {
    ""Id"": ""daily_gather_wood"",
    ""Title"": ""Daily Lumber Run"",
    ""Description"": ""Gather wood for the network. Resets every day."",
    ""Tier"": ""Recruit"", ""Category"": ""Daily"", ""DifficultyStars"": 1,
    ""Daily"": true, ""Repeatable"": false,
    ""Objectives"": [{ ""Type"": ""chop"", ""Target"": ""wood"", ""Count"": 500, ""Description"": ""Chop 500 wood"" }],
    ""Rewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 80 }, { ""Type"": ""tier_xp"", ""Amount"": 60 }, { ""Type"": ""reputation"", ""Amount"": 25 }],
    ""VipRewards"": [{ ""Type"": ""reputation"", ""Amount"": 8 }]
  }
]",
                ["gathering"] = @"[
  {
    ""Id"": ""gather_wood_recruit"",
    ""Title"": ""Lumber Contract"",
    ""Description"": ""The network needs wood. Chop and deliver."",
    ""Tier"": ""Recruit"", ""Category"": ""Gathering"", ""DifficultyStars"": 1,
    ""Repeatable"": true, ""CooldownSeconds"": 3600, ""VipCooldownSeconds"": 1800,
    ""Objectives"": [{ ""Type"": ""chop"", ""Target"": ""wood"", ""Count"": 2000, ""Description"": ""Chop 2000 wood"" }],
    ""Rewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 120 }, { ""Type"": ""tier_xp"", ""Amount"": 80 }, { ""Type"": ""reputation"", ""Amount"": 35 }],
    ""VipRewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 40 }, { ""Type"": ""reputation"", ""Amount"": 12 }]
  },
  {
    ""Id"": ""mine_stone_recruit"",
    ""Title"": ""Stone Run"",
    ""Description"": ""Mine stone for construction projects."",
    ""Tier"": ""Recruit"", ""Category"": ""Gathering"", ""DifficultyStars"": 1,
    ""Repeatable"": true, ""CooldownSeconds"": 3600, ""VipCooldownSeconds"": 1800,
    ""Objectives"": [{ ""Type"": ""mine"", ""Target"": ""stones"", ""Count"": 2000, ""Description"": ""Mine 2000 stone"" }],
    ""Rewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 110 }, { ""Type"": ""tier_xp"", ""Amount"": 75 }, { ""Type"": ""reputation"", ""Amount"": 30 }],
    ""VipRewards"": [{ ""Type"": ""reputation"", ""Amount"": 10 }]
  }
]",
                ["combat"] = @"[
  {
    ""Id"": ""kill_scientists_recruit"",
    ""Title"": ""Patrol Clearance"",
    ""Description"": ""Eliminate roaming scientists near monuments."",
    ""Tier"": ""Recruit"", ""Category"": ""Combat"", ""DifficultyStars"": 2,
    ""Repeatable"": true, ""CooldownSeconds"": 7200, ""VipCooldownSeconds"": 3600,
    ""Objectives"": [{ ""Type"": ""kill"", ""Target"": ""scientist"", ""Count"": 5, ""Description"": ""Kill 5 scientists"" }],
    ""Rewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 150 }, { ""Type"": ""tier_xp"", ""Amount"": 100 }, { ""Type"": ""reputation"", ""Amount"": 40 }],
    ""VipRewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 50 }, { ""Type"": ""reputation"", ""Amount"": 14 }]
  }
]",
                ["scavenging"] = @"[
  {
    ""Id"": ""loot_barrels_recruit"",
    ""Title"": ""Barrel Run"",
    ""Description"": ""Smash open roadside barrels across the island."",
    ""Tier"": ""Recruit"", ""Category"": ""Scavenging"", ""DifficultyStars"": 1,
    ""Repeatable"": true, ""CooldownSeconds"": 3600, ""VipCooldownSeconds"": 1800,
    ""Objectives"": [{ ""Type"": ""loot"", ""Target"": ""barrel"", ""Count"": 10, ""Description"": ""Destroy or loot 10 barrels"" }],
    ""Rewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 100 }, { ""Type"": ""tier_xp"", ""Amount"": 70 }, { ""Type"": ""reputation"", ""Amount"": 28 }],
    ""VipRewards"": [{ ""Type"": ""reputation"", ""Amount"": 10 }]
  }
]",
                ["craft"] = @"[
  {
    ""Id"": ""craft_bow_recruit"",
    ""Title"": ""First Arms"",
    ""Description"": ""Craft bows to prove your workbench skills."",
    ""Tier"": ""Recruit"", ""Category"": ""Craft"", ""DifficultyStars"": 1,
    ""Repeatable"": true, ""CooldownSeconds"": 7200, ""VipCooldownSeconds"": 3600,
    ""Objectives"": [{ ""Type"": ""craft"", ""Target"": ""bow.hunting"", ""Count"": 3, ""Description"": ""Craft 3 hunting bows"" }],
    ""Rewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 120 }, { ""Type"": ""tier_xp"", ""Amount"": 80 }, { ""Type"": ""reputation"", ""Amount"": 30 }],
    ""VipRewards"": [{ ""Type"": ""reputation"", ""Amount"": 10 }]
  }
]",
                ["adventure"] = @"[
  {
    ""Id"": ""loot_military_crates"",
    ""Title"": ""Monument Scavenging"",
    ""Description"": ""Loot military crates from monument areas."",
    ""Tier"": ""Operative"", ""Category"": ""Adventure"", ""DifficultyStars"": 2,
    ""Repeatable"": true, ""CooldownSeconds"": 14400, ""VipCooldownSeconds"": 7200,
    ""Objectives"": [{ ""Type"": ""loot"", ""Target"": ""crate_normal"", ""Count"": 5, ""Description"": ""Loot 5 military crates"" }],
    ""Rewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 220 }, { ""Type"": ""item"", ""Shortname"": ""techparts"", ""Amount"": 2 }, { ""Type"": ""tier_xp"", ""Amount"": 180 }, { ""Type"": ""reputation"", ""Amount"": 75 }],
    ""VipRewards"": [{ ""Type"": ""item"", ""Shortname"": ""techparts"", ""Amount"": 1 }, { ""Type"": ""reputation"", ""Amount"": 25 }]
  }
]",
                ["delivery"] = @"[
  {
    ""Id"": ""deliver_wood_recruit"",
    ""Title"": ""Timber Delivery"",
    ""Description"": ""Bring wood to the Outpost contractor."",
    ""Tier"": ""Recruit"", ""Category"": ""Delivery"", ""DifficultyStars"": 1,
    ""Repeatable"": true, ""CooldownSeconds"": 7200, ""VipCooldownSeconds"": 3600,
    ""Objectives"": [{ ""Type"": ""deliver"", ""Target"": ""wood"", ""Count"": 1000, ""Description"": ""Deliver 1000 wood to Outpost"", ""Location"": ""outpost"" }],
    ""Rewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 130 }, { ""Type"": ""tier_xp"", ""Amount"": 90 }, { ""Type"": ""reputation"", ""Amount"": 38 }],
    ""VipRewards"": [{ ""Type"": ""reputation"", ""Amount"": 12 }]
  }
]",
                ["buying"] = @"[
  {
    ""Id"": ""buy_scrap_recruit"",
    ""Title"": ""Scrap Acquisition"",
    ""Description"": ""Purchase scrap from a vending machine to fulfil the contract."",
    ""Tier"": ""Recruit"", ""Category"": ""Buying"", ""DifficultyStars"": 1,
    ""Repeatable"": true, ""CooldownSeconds"": 7200, ""VipCooldownSeconds"": 3600,
    ""Objectives"": [{ ""Type"": ""purchase"", ""Target"": ""scrap"", ""Count"": 200, ""Description"": ""Buy 200 scrap from a vending machine"" }],
    ""Rewards"": [{ ""Type"": ""item"", ""Shortname"": ""scrap"", ""Amount"": 100 }, { ""Type"": ""tier_xp"", ""Amount"": 60 }, { ""Type"": ""reputation"", ""Amount"": 22 }],
    ""VipRewards"": [{ ""Type"": ""reputation"", ""Amount"": 7 }]
  }
]"
            };

            foreach (var kv in starters)
            {
                string path = Path.Combine(dir, $"{kv.Key}.json");
                if (!File.Exists(path))
                    File.WriteAllText(path, kv.Value);
            }
            Puts($"[InfinitumQuests] Wrote {starters.Count} starter quest files to {dir}");
        }


        // ─────────────────────── Oxide Lifecycle ──────────────────────────────
        private void Init()
        {
            Instance = this;
            permission.RegisterPermission(PERM_USE,   this);
            permission.RegisterPermission(PERM_ADMIN, this);
            permission.RegisterPermission(PERM_VIP,   this);
            permission.GrantGroupPermission("default", PERM_USE,   this);
            permission.GrantGroupPermission("admin",   PERM_ADMIN, this);

            foreach (var cmd in config.OpenCommands)
                cmd_register(cmd);

            LoadPlayerData(); // also load here in case OnServerInitialized doesn't fire on reload
            LoadContractorPositions();
        }

        private void cmd_register(string cmd)
        {
            cmd = cmd.Trim('/');
            AddCovalenceCommand(cmd, nameof(CmdQuestOpen));
        }

        private void OnServerInitialized()
        {
            LoadPlayerData();
            LoadQuestDefinitions();

            if (config.HudEnabled)
                _hudTimer = timer.Every(30f, () =>
                {
                    foreach (var p in BasePlayer.activePlayerList)
                    {
                        EnsureDailyWindowFresh(GetOrCreate(p), p);
                        UpdateHud(p);
                    }
                });

            foreach (var p in BasePlayer.activePlayerList) GetOrCreate(p);

            if (config.UseImageLibrary && ImageLibrary != null)
                PrefetchQuestImages();

            // Flush dirty player data to disk every 60 s instead of on every gather/kill event.
            _saveTimer = timer.Every(60f, () =>
            {
                if (_dataDirty) { SavePlayerData(); _dataDirty = false; }
            });

            if (config.ContractorNpcs.Enabled)
                timer.Once(3f, SpawnContractorNpcs);
        }

        // Called by ImageLibrary when it finishes its startup load
        private void OnImageLibraryReady()
        {
            if (config.UseImageLibrary)
                PrefetchQuestImages();
        }

        private void PrefetchQuestImages()
        {
            if (ImageLibrary == null) return;

            // ── 1. Reward item icons (native rustedit URL by shortname) ──────────
            var itemShortnames = new HashSet<string>();
            foreach (var q in _quests)
                foreach (var r in q.Rewards)
                    if ((r.Type.ToLower() == "item" || r.Type.ToLower() == "blueprint") && !string.IsNullOrEmpty(r.Shortname))
                        itemShortnames.Add(r.Shortname);

            foreach (var sn in itemShortnames)
                ImageLibrary.Call("AddImage", $"https://www.rustedit.io/images/imagelibrary/{sn}.png", sn, (ulong)0);

            // Queue skin IDs
            foreach (var q in _quests)
                foreach (var r in q.Rewards)
                    if ((r.Type.ToLower() == "item" || r.Type.ToLower() == "blueprint") && r.SkinId != 0 && !string.IsNullOrEmpty(r.Shortname))
                        ImageLibrary.Call("AddImage", "", r.Shortname, r.SkinId);
            foreach (var q in _quests)
                if (q.VipRewards != null)
                    foreach (var vr in q.VipRewards)
                        if ((vr.Type.ToLower() == "item" || vr.Type.ToLower() == "blueprint") && vr.SkinId != 0 && !string.IsNullOrEmpty(vr.Shortname))
                            ImageLibrary.Call("AddImage", "", vr.Shortname, vr.SkinId);

            // ── 2. Kill objective icons ───────────────────────────────────────────
            // Collect every unique kill target used across all quests.
            var killTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var q in _quests)
                foreach (var obj in q.Objectives)
                    if ((obj.Type?.ToLower() ?? "") == "kill" && !string.IsNullOrEmpty(obj.Target))
                        killTargets.Add(obj.Target.ToLower());

            foreach (var target in killTargets)
            {
                string key = KillIconKey(target);

                // Check if admin has configured a custom URL for this target.
                string url;
                if (config.KillIconUrls.TryGetValue(target, out url) && !string.IsNullOrEmpty(url))
                {
                    // Register the custom URL under our prefixed key.
                    ImageLibrary.Call("AddImage", url, key, (ulong)0);
                }
                else
                {
                    // No custom URL — fall back to the nearest item icon from rustedit.
                    string fallbackSn;
                    if (!_killIconFallback.TryGetValue(target, out fallbackSn))
                        fallbackSn = target; // try the target itself as a shortname
                    ImageLibrary.Call("AddImage",
                        $"https://www.rustedit.io/images/imagelibrary/{fallbackSn}.png",
                        key, (ulong)0);
                }
            }
        }

        // Returns the Contractor NPC the player must deliver to.
        // filter="" or "any" → random; otherwise matches NPC whose saved-pos key contains the filter.
        private ScientistNPC AssignDeliveryNpc(string filter)
        {
            if (_contractorNpcs.Count == 0) return null;
            var valid = _contractorNpcs.Where(n => n != null && !n.IsDestroyed).ToList();
            if (valid.Count == 0) return null;

            if (string.IsNullOrEmpty(filter) || filter.ToLower() == "any")
                return valid[UnityEngine.Random.Range(0, valid.Count)];

            // Match against saved position keys
            string fl = filter.ToLower();
            foreach (var kv in _contractorPos)
                if (kv.Key.ToLower().Contains(fl))
                {
                    // Find the NPC at that saved position (closest match)
                    var pos = kv.Value.Position;
                    ScientistNPC best = null;
                    float bestDist = float.MaxValue;
                    foreach (var n in valid)
                    {
                        float d = Vector3.Distance(n.transform.position, pos);
                        if (d < bestDist) { bestDist = d; best = n; }
                    }
                    if (best != null && bestDist < 10f) return best;
                }

            // Fallback: random
            return valid[UnityEngine.Random.Range(0, valid.Count)];
        }

        private string GetContractorDisplayName(ulong npcNetId)
        {
            if (npcNetId == 0) return null;
            var npc = _contractorNpcs.FirstOrDefault(n => n != null && !n.IsDestroyed && n.net?.ID.Value == npcNetId);
            return npc?.displayName;
        }

        // ─────────────────────── Contractor NPC Spawning ─────────────────────
        private const string CONTRACTOR_PREFAB = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab";

        private void SpawnContractorNpcs()
        {
            var cfg = config.ContractorNpcs;
            if (TerrainMeta.Path?.Monuments == null)
            {
                Puts("[InfinitumQuests] TerrainMeta monuments not ready.");
                return;
            }

            // Kill any orphan contractors left over from a previous load (e.g. after a compile-error reload)
            DespawnContractorNpcs();
            foreach (var e in BaseNetworkable.serverEntities.OfType<ScientistNPC>().ToList())
            {
                if (e == null || e.IsDestroyed) continue;
                string dn = e.displayName ?? "";
                if (dn == cfg.DisplayName || dn.EndsWith(" " + cfg.DisplayName) || dn.Contains("Contractor"))
                {
                    e.Kill();
                    Puts($"[InfinitumQuests] Cleaned up orphan contractor NPC '{dn}' at {e.transform.position}");
                }
            }

            var spawned = new HashSet<string>();

            // ── Per-location logic: each spawn point independently checks for a
            //    saved admin position first; if none exists it falls back to the
            //    monument anchor system.  This is wipe-safe by default because a
            //    fresh install (or cleared positions) will always use monument anchors.
            foreach (var sp in cfg.SpawnPoints)
            {
                if (string.IsNullOrEmpty(sp.MonumentFilter)) continue;
                string filter = sp.MonumentFilter.ToLower();
                if (spawned.Contains(filter)) continue;

                // ── Prefer admin-saved position for this filter ────────────────
                ContractorSavedPos savedPos;
                if (_contractorPos.TryGetValue(filter, out savedPos))
                {
                    string npcName = !string.IsNullOrEmpty(savedPos.Name) ? savedPos.Name : cfg.DisplayName;
                    Puts($"[InfinitumQuests] '{filter}': using saved position {savedPos.Position} rot={savedPos.RotationY:F0}°");
                    SpawnOneContractor(cfg, savedPos.Position, npcName, savedPos.Rotation,
                        sp.Clothing != null && sp.Clothing.Count > 0 ? sp.Clothing : null);
                    spawned.Add(filter);
                    continue;
                }

                // ── No saved position → monument anchor fallback ───────────────
                MonumentInfo monument = FindMonument(filter);
                if (monument == null)
                {
                    Puts($"[InfinitumQuests] '{filter}': no monument match and no saved pos — skipping.");
                    continue;
                }
                spawned.Add(filter);

                Vector3 pos = ResolveAnchorPosition(monument, sp);
                string monName = char.ToUpper(filter[0]) + filter.Substring(1) + " " + cfg.DisplayName;
                Puts($"[InfinitumQuests] '{filter}': monument fallback → spawning '{monName}' at {pos}");
                SpawnOneContractor(cfg, pos, monName, default,
                    sp.Clothing != null && sp.Clothing.Count > 0 ? sp.Clothing : null);
            }

            // ── Spawn any explicitly saved positions not covered by a SpawnPoint
            //    (e.g. custom locations added with a unique filter name) ─────────
            foreach (var kv in _contractorPos)
            {
                string filter = kv.Key.ToLower();
                if (spawned.Contains(filter)) continue;
                string npcName = !string.IsNullOrEmpty(kv.Value.Name) ? kv.Value.Name : cfg.DisplayName;
                Puts($"[InfinitumQuests] '{filter}': custom saved position {kv.Value.Position} rot={kv.Value.RotationY:F0}°");
                SpawnOneContractor(cfg, kv.Value.Position, npcName, kv.Value.Rotation);
                spawned.Add(filter);
            }

            Puts($"[InfinitumQuests] Contractor spawn complete: {_contractorNpcs.Count} spawned.");
        }

        // Returns the first monument whose display name or GameObject name contains the filter string.
        private MonumentInfo FindMonument(string filterLow)
        {
            foreach (var m in TerrainMeta.Path.Monuments)
            {
                string mLow  = (m.displayPhrase?.english ?? "").ToLower();
                string goLow = (m.name ?? "").ToLower();
                if (mLow.Contains(filterLow) || goLow.Contains(filterLow)) return m;
            }
            return null;
        }

        // Resolves the world-space spawn position for a ContractorSpawnPoint relative to a monument.
        // Searches for the nearest matching anchor entity within 60 m; falls back to monument centre.
        private Vector3 ResolveAnchorPosition(MonumentInfo monument, ContractorSpawnPoint sp)
        {
            var nearbyObjects = new List<BaseEntity>();
            Vis.Entities(monument.transform.position, 150f, nearbyObjects);
            
            // Priority 1: Specific config anchor (e.g. if the admin configured "workbench3")
            if (!string.IsNullOrEmpty(sp.AnchorEntity))
            {
                string anchorLow = sp.AnchorEntity.ToLower();
                var configAnchor = nearbyObjects.FirstOrDefault(e => 
                    e != null && !e.IsDestroyed && (
                        (e.ShortPrefabName ?? "").ToLower().Contains(anchorLow) || 
                        ((e as BasePlayer)?.displayName ?? "").ToLower().Contains(anchorLow)
                    ));
                    
                if (configAnchor != null)
                {
                    // Use the full SideOffset and a minimal forward boost to place them 'beside' instead of 'in front'
                    Vector3 ap = configAnchor.transform.position + configAnchor.transform.right * sp.SideOffset + configAnchor.transform.forward * 0.4f;
                    Puts($"[InfinitumQuests] Anchored to config entity '{configAnchor.ShortPrefabName}' at {configAnchor.transform.position}");
                    return ap;
                }
            }

            // Priority 2: Safest native static monument objects 
            // These never move, cannot be lured outside the monument, and are guaranteed to be on a flat floor.
            var staticAnchor = nearbyObjects.FirstOrDefault(e => 
                e != null && !e.IsDestroyed && (
                    e is VendingMachine || e is Recycler || e is RepairBench || e is MixingTable || e is CardTable ||
                    (e.ShortPrefabName != null && (
                        e.ShortPrefabName.Contains("firebarrel") || 
                        e.ShortPrefabName.Contains("locker") || 
                        e.ShortPrefabName.Contains("fridge")
                    ))
                ));

            if (staticAnchor != null)
            {
                // Stand beside the static object with a moderate forward offset
                Vector3 ap = staticAnchor.transform.position + staticAnchor.transform.right * 1.5f + staticAnchor.transform.forward * 1.2f;
                Puts($"[InfinitumQuests] Anchored to static object '{staticAnchor.ShortPrefabName}' at {staticAnchor.transform.position}");
                return ap;
            }

            // Fallback 3: fallback to monument centre mapped over terrain
            Vector3 pos = monument.transform.position + Vector3.up * sp.YOffset;
            float terrainY = TerrainMeta.HeightMap.GetHeight(pos);
            if (pos.y < terrainY) pos.y = terrainY;
            return pos;
        }

        private void SpawnOneContractor(ContractorNpcConfig cfg, Vector3 pos, string npcName, Quaternion rot = default, List<string> clothing = null)
        {
            if (rot == default) rot = Quaternion.identity;

            // Nuke any 'ghost' contractors standing exactly on this deterministic anchor coordinate 
            // (e.g. ones that survived a previous reload due to a renamed config)
            var overlapping = new List<ScientistNPC>();
            Vis.Entities(pos, 0.5f, overlapping, Rust.Layers.Mask.Player_Server);
            foreach (var oldNpc in overlapping)
            {
                // Native guards always hold guns. Our contractors have their belts cleared. 
                var belt = oldNpc.inventory?.containerBelt;
                if (oldNpc != null && !oldNpc.IsDestroyed && (belt == null || belt.itemList == null || belt.itemList.Count == 0))
                {
                    oldNpc.Kill();
                    Puts($"[InfinitumQuests] Cleared overlapping ghost contractor at {pos}");
                }
            }

            var entity = GameManager.server.CreateEntity(CONTRACTOR_PREFAB, pos, rot);
            if (entity == null) { Puts("[InfinitumQuests]  → CreateEntity returned null"); return; }
            
            // Prevent them from persisting in the .sav file across server restarts
            entity.enableSaving = false;
            
            entity.Spawn();
            var npc = entity as ScientistNPC;
            if (npc == null) { entity.Kill(); Puts("[InfinitumQuests]  → Not ScientistNPC"); return; }

            npc.displayName = npcName;
            npc.startHealth = 500f;
            npc.InitializeHealth(500f, 500f);

            npc.inventory?.containerBelt?.Clear();
            npc.inventory?.containerMain?.Clear();
            var wearList = (clothing != null && clothing.Count > 0) ? clothing : cfg.Clothing;
            if (wearList != null && wearList.Count > 0)
            {
                npc.inventory?.containerWear?.Clear();
                foreach (var shortname in wearList)
                {
                    var item = ItemManager.CreateByName(shortname, 1);
                    if (item != null && !item.MoveToContainer(npc.inventory.containerWear))
                        item.Remove();
                }
            }

            if (npc.Brain != null)
            {
                npc.Brain.enabled = false;
                npc.Brain.Navigator?.Pause();
                if (npc.Brain.Navigator?.Agent != null)
                    npc.Brain.Navigator.Agent.enabled = false;
            }
            npc.CancelInvoke(npc.EquipTest);

            if (!string.IsNullOrEmpty(cfg.Gesture) && cfg.GestureInterval > 0f)
            {
                var capturedNpc     = npc;
                var capturedGesture = cfg.Gesture;
                _contractorTimers.Add(timer.Every(cfg.GestureInterval, () =>
                {
                    if (capturedNpc == null || capturedNpc.IsDestroyed) return;
                    capturedNpc.SignalBroadcast(BaseEntity.Signal.Gesture, capturedGesture);
                }));
            }

            _contractorNpcs.Add(npc);
            SpawnContractorMapMarker(npc, npcName);
            Puts($"[InfinitumQuests]  → Spawned '{npcName}' netID={npc.net.ID.Value}");
        }

        private void DespawnContractorNpcs()
        {
            foreach (var t in _contractorTimers) t?.Destroy();
            _contractorTimers.Clear();
            foreach (var npc in _contractorNpcs)
                if (npc != null && !npc.IsDestroyed) npc.Kill();
            _contractorNpcs.Clear();
            foreach (var m in _contractorMarkers)
                if (m != null && !m.IsDestroyed) m.Kill();
            _contractorMarkers.Clear();
        }

        private void SpawnContractorMapMarker(ScientistNPC npc, string label)
        {
            var marker = GameManager.server.CreateEntity(
                "assets/prefabs/tools/map/genericradiusmarker.prefab",
                npc.transform.position) as MapMarkerGenericRadius;
            if (marker == null) return;
            marker.radius = 0.05f;
            marker.alpha  = 1f;
            marker.color1 = new Color(1f, 0.78f, 0f);   // gold fill
            marker.color2 = new Color(1f, 1f,   1f);    // white border
            marker.Spawn();
            marker.SendUpdate();
            _contractorMarkers.Add(marker);
        }

        // ─────────────────────── Contractor NPC Hooks ────────────────────────
        // E-key interaction — proximity + facing check, no raycast layer issues.
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (_contractorNpcs.Count == 0 || player == null) return;
            if (!input.WasJustPressed(BUTTON.USE)) return;
            foreach (var npc in _contractorNpcs)
            {
                if (npc == null || npc.IsDestroyed) continue;
                if (Vector3.Distance(player.transform.position, npc.transform.position) > 3f) continue;
                Vector3 toNpc = (npc.transform.position - player.eyes.position).normalized;
                if (Vector3.Dot(player.eyes.HeadForward(), toNpc) < 0.4f) continue;

                // Rotate NPC to face the player — viewAngles is how Rust syncs player-entity facing
                Vector3 lookDir = player.transform.position - npc.transform.position;
                lookDir.y = 0f;
                if (lookDir != Vector3.zero)
                {
                    npc.viewAngles = Quaternion.LookRotation(lookDir.normalized).eulerAngles;
                    npc.transform.rotation = Quaternion.Euler(npc.viewAngles);
                    npc.SendNetworkUpdateImmediate();
                }

                // Greeting gesture (visible to all nearby)
                npc.SignalBroadcast(BaseEntity.Signal.Gesture, config.ContractorNpcs.Gesture);

                // Greeting sound — sent only to the interacting player's connection
                if (!string.IsNullOrEmpty(config.ContractorNpcs.GreetingSound) && player.net?.connection != null)
                {
                    var effect = new Effect();
                    effect.Init(Effect.Type.Generic, npc.transform.position, Vector3.zero);
                    effect.pooledString = config.ContractorNpcs.GreetingSound;
                    EffectNetwork.Send(effect, player.net.connection);
                }

                OpenDeliveryUI(player, npc);
                return;
            }
        }

        // Prevent contractor NPCs from targeting or attacking players.
        private object OnNpcPlayerTarget(NPCPlayer npc, BaseEntity target)
        {
            if (npc is ScientistNPC s && _contractorNpcs.Contains(s)) return true;
            return null;
        }

        private object CanNpcAttack(NPCPlayer npc, BaseEntity target)
        {
            if (npc is ScientistNPC s && _contractorNpcs.Contains(s)) return false;
            return null;
        }

        private void Unload()
        {
            DespawnContractorNpcs();
            foreach (var p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, UI_MAIN);
                CuiHelper.DestroyUi(p, UI_HUD);
                CuiHelper.DestroyUi(p, UI_TOAST);
                CuiHelper.DestroyUi(p, "IQ_Delivery");
                CuiHelper.DestroyUi(p, UI_REWARDLIST);
                CuiHelper.DestroyUi(p, UI_ADMIN);
            }
            foreach (var t in _toastTimers.Values) t?.Destroy();
            _toastTimers.Clear();
            _hudMoveMode.Clear();
            _hudMoveStep.Clear();
            _hudHash.Clear();
            _saveTimer?.Destroy();
            _hudTimer?.Destroy();
            if (_dataDirty) SavePlayerData();   // flush any pending changes on clean shutdown
            Instance = null;
        }

        private void OnServerSave() => SavePlayerData();

        // Called by Oxide when the server generates a new map (i.e. a wipe).
        private void OnNewSave()
        {
            if (!config.ContractorNpcs.AutoClearPositionsOnWipe) return;
            _contractorPos.Clear();
            SaveContractorPositions();
            Puts("[InfinitumQuests] New wipe detected — cleared saved contractor positions (auto-clear enabled).");
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            var data = GetOrCreate(player);
            EnsureDailyWindowFresh(data, player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            Timer t;
            if (_toastTimers.TryGetValue(player.userID, out t)) { t?.Destroy(); _toastTimers.Remove(player.userID); }
            _hudHash.Remove(player.userID);
            SavePlayerData();
        }

        // ─────────────────────── Objective Hooks ──────────────────────────────
        // kill
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            var attacker = info.InitiatorPlayer;
            if (attacker == null || attacker.IsNpc) return;

            // Barrels are LootContainers that can only be broken, never opened via E.
            // OnLootEntity never fires for them, so award loot progress here instead.
            if (entity is LootContainer && entity.OwnerID == 0)
            {
                string barrelPrefab = entity.ShortPrefabName?.ToLower() ?? "";
                AwardProgress(attacker, "loot", barrelPrefab, 1);
                return;
            }

            bool headshot = IsHeadshot(info);

            // Resolve category tags
            var tags = new List<string>();
            if (entity is BaseAnimalNPC || IsAnimalByPrefab(entity))           tags.Add("animal");
            else if (entity is NPCPlayer || (entity is BasePlayer ep && ep.IsNpc)) tags.Add("npc");
            else if (entity is BasePlayer bp && !bp.IsNpc)                     tags.Add("player");
            else if (entity is BradleyAPC)                                      tags.Add("bradley");
            else if (entity is PatrolHelicopter)                                tags.Add("heli");
            else if (entity is BaseHelicopter && !(entity is PatrolHelicopter)) tags.Add("attack_heli");
            else if (entity is CH47Helicopter)                                  tags.Add("ch47");

            string prefab = entity.ShortPrefabName?.ToLower() ?? "";
            if (!string.IsNullOrEmpty(prefab)) tags.Add(prefab);

            // ZombieHunter — tag zombie kills so "zombie" / "zombie_hunter" targets work in quests
            if (config.EventIntegrations.TrackZombieHunter && entity?.net != null
                && _zombieHunterIds.Contains(entity.net.ID.Value))
            {
                tags.Add("zombie");
                tags.Add("zombie_hunter");
                _zombieHunterIds.Remove(entity.net.ID.Value);
            }

            foreach (var tag in tags) AwardProgress(attacker, "kill", tag, 1, headshot);
        }

        // Fallback animal detection by prefab name for newer animals (wolves, crocs, panthers)
        // that are not subclasses of BaseAnimalNPC in older Rust builds.
        private static bool IsAnimalByPrefab(BaseEntity entity)
        {
            string p = entity?.ShortPrefabName?.ToLower() ?? "";
            return p.Contains("wolf")  || p.Contains("bear")   || p.Contains("boar")
                || p.Contains("stag")  || p.Contains("chicken")|| p.Contains("horse")
                || p.Contains("croc")  || p.Contains("panther") || p.Contains("deer");
        }

        // True when the killing blow landed on the head/skull bone.
        private static bool IsHeadshot(HitInfo info)
        {
            if (info == null) return false;
            string bone = StringPool.Get(info.HitBone)?.ToLower() ?? "";
            return bone.Contains("head") || bone.Contains("skull");
        }

        // chop / mine / skin / gather
        // GatherType.Tree  = chopping wood
        // GatherType.Ore   = mining stone/ore
        // GatherType.Flesh = skinning a corpse
        // Collectibles (OnCollectiblePickedup) = generic gather (hemp, mushrooms, etc.)
        private void OnDispenserGathered(ResourceDispenser disp, BasePlayer player, Item item)
        {
            if (player == null || item == null) return;
            var gt = disp != null ? disp.gatherType : ResourceDispenser.GatherType.Flesh;
            switch (gt)
            {
                case ResourceDispenser.GatherType.Tree:
                    AwardProgress(player, "chop", item.info.shortname, item.amount);
                    break;
                case ResourceDispenser.GatherType.Ore:
                    AwardProgress(player, "mine", item.info.shortname, item.amount);
                    break;
                case ResourceDispenser.GatherType.Flesh:
                    // Target = corpse prefab name (e.g. "wolf_corpse", "bear_corpse")
                    AwardProgress(player, "skin", disp.baseEntity?.ShortPrefabName?.ToLower() ?? "", item.amount);
                    break;
                default:
                    AwardProgress(player, "gather", item.info.shortname, item.amount);
                    break;
            }
        }

        private void OnCollectiblePickedup(CollectibleEntity col, BasePlayer player)
        {
            if (player == null || col?.itemList == null) return;
            foreach (var ia in col.itemList)
            {
                var def = ItemManager.FindItemDefinition(ia.itemid);
                if (def != null) AwardProgress(player, "gather", def.shortname, (int)ia.amount);
            }
        }

        // craft
        private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            var player = crafter?.owner;
            if (player == null || item == null) return;
            AwardProgress(player, "craft", item.info.shortname, item.amount);
        }

        // research
        private void OnTechTreeNodeUnlocked(Workbench bench, TechTreeData.NodeInstance node, BasePlayer player)
        {
            if (player == null || node?.itemDef == null) return;
            AwardProgress(player, "research", node.itemDef.shortname, 1);
        }

        // loot
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return;

            // Recyclers don't give loot progress, but we record who is using this one
            // so OnItemRecycle can attribute progress to the correct player.
            if (entity is Recycler)
            {
                _recyclerUser[entity.net?.ID.Value ?? 0] = player.userID;
                return;
            }

            // Skip containers owned by a real Steam account (own boxes, TC, team storage).
            // World-spawned entities (crates, barrels, NPC corpses) have OwnerID == 0.
            if (entity.OwnerID > 70000000000000000UL) return;

            AwardProgress(player, "loot", entity.ShortPrefabName?.ToLower() ?? "", 1);
        }

        // Clear recycler tracking when the player stops looting
        private void OnLootEntityEnd(BasePlayer player, BaseEntity entity)
        {
            if (player == null || !(entity is Recycler)) return;
            _recyclerUser.Remove(entity.net?.ID.Value ?? 0);
        }

        // hack crate — Oxide passes the initiating player as the second argument
        private void OnCrateHack(HackableLockedCrate crate, BasePlayer player)
        {
            if (player == null || player.IsNpc) return;
            AwardProgress(player, "hack_crate", "", 1);
        }

        // upgrade building
        private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade, ulong skin)
        {
            if (player == null) return;
            AwardProgress(player, "upgrade_building", grade.ToString().ToLower(), 1);
        }

        // recycle — look up who opened this recycler via _recyclerUser (populated by OnLootEntity)
        private void OnItemRecycle(Item item, Recycler recycler)
        {
            if (item?.info == null || recycler == null) return;
            ulong userId;
            if (!_recyclerUser.TryGetValue(recycler.net?.ID.Value ?? 0, out userId)) return;
            var player = BasePlayer.FindByID(userId);
            if (player == null) return;
            AwardProgress(player, "recycle", item.info.shortname, 1);
        }

        // fish
        private void OnFishCatch(Item fish, BaseFishingRod rod, BasePlayer player)
        {
            if (player == null || fish == null) return;
            AwardProgress(player, "fish", fish.info.shortname, 1);
        }

        // deploy — item placed in the world (sleeping bag, furnace, turret, etc.)
        // BuildingBlock entities (walls, foundations) are NOT deploy objectives; they belong
        // to upgrade_building. Filtering them prevents players from farming deploy counts
        // by spamming cheap building pieces.
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan?.GetOwnerPlayer();
            if (player == null || go == null) return;
            var ent = go.GetComponent<BaseEntity>();
            if (ent == null || ent is BuildingBlock) return;
            AwardProgress(player, "deploy", ent.ShortPrefabName?.ToLower() ?? "", 1);
        }

        // pickup — item retrieved from ground or container
        private void OnItemPickup(Item item, BasePlayer player)
        {
            if (player == null || item?.info == null || player.IsNpc) return;
            AwardProgress(player, "pickup", item.info.shortname, item.amount);
        }

        // heal — medical tool used (bandage, syringe, etc.)
        // The second parameter IS the player using it — healing is always self-targeted in Rust.
        private void OnHealingItemUse(MedicalTool tool, BasePlayer target)
        {
            if (target == null || target.IsNpc) return;
            AwardProgress(target, "heal", tool?.ShortPrefabName?.ToLower() ?? "", 1);
        }

        // harvest — crop collected from a fully-grown plant
        private void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (player == null || item?.info == null || player.IsNpc) return;
            AwardProgress(player, "harvest", item.info.shortname, item.amount);
        }

        // repair — building block repaired in-world
        // OnStructureRepair fires on every server tick while the hammer is held against the
        // surface, so a single repair session can fire dozens of times. We debounce per entity:
        // only the first hit within a 4-second window awards progress.
        private void OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (player == null || entity == null || player.IsNpc) return;
            ulong eid = entity.net?.ID.Value ?? 0;
            float now = Time.realtimeSinceStartup;
            float last;
            if (_lastRepair.TryGetValue(eid, out last) && now - last < 4f) return;
            _lastRepair[eid] = now;
            AwardProgress(player, "repair", entity.ShortPrefabName?.ToLower() ?? "", 1);
        }

        // purchase — item bought from a vending machine
        private void OnBuyItem(VendingMachine machine, BasePlayer buyer, ProtoBuf.VendingMachine.SellOrder order, int count)
        {
            if (buyer == null || order == null || buyer.IsNpc) return;
            var idef = ItemManager.FindItemDefinition(order.itemToSellID);
            AwardProgress(buyer, "purchase", idef?.shortname ?? "", count > 0 ? count : 1);
        }

        // ─────────────────────── External Plugin Event Hooks ──────────────────
        // Each plugin fires a hook with the winner's steamId.
        // All converge on HandleEventWin which maps to the "event_win" objective type.
        // Target string is a lowercase slug the quest designer uses to filter by event.

        private void OnConvoyEventWin(ulong id)             => HandleEventWin(id, "convoy");
        private void OnHarborEventWinner(ulong id)          => HandleEventWin(id, "harbor");
        private void OnAirEventWinner(ulong id)             => HandleEventWin(id, "air");
        private void OnArcticBaseEventWinner(ulong id)      => HandleEventWin(id, "arcticbase");
        private void OnGasStationEventWinner(ulong id)      => HandleEventWin(id, "gasstation");
        private void OnSupermarketEventWinner(ulong id)     => HandleEventWin(id, "supermarket");
        private void OnArmoredTrainEventWin(ulong id)       => HandleEventWin(id, "armoredtrain");
        private void OnJunkyardEventWinner(ulong id)        => HandleEventWin(id, "junkyard");

        private void HandleEventWin(ulong steamId, string eventTarget)
        {
            if (!config.EventIntegrations.TrackEventWins) return;
            var player = BasePlayer.FindByID(steamId);
            if (player == null) return;
            AwardProgress(player, "event_win", eventTarget, 1);
        }

        // BossMonster — boss entity killed by a player
        private void OnBossKilled(BaseEntity entity, BasePlayer killer)
        {
            if (!config.EventIntegrations.TrackBossKills || killer == null || killer.IsNpc) return;
            AwardProgress(killer, "boss_kill", entity?.ShortPrefabName?.ToLower() ?? "", 1);
        }

        // RaidableBases — fires once per player when their raid reward is issued
        // infoJson contains difficulty/level data from the base's options
        private void OnRaidableAwardGiven(string playerName, ulong userId, string infoJson)
        {
            if (!config.EventIntegrations.TrackRaidableBases) return;
            var player = BasePlayer.FindByID(userId);
            if (player == null) return;
            AwardProgress(player, "raidable_base", ParseRbDifficulty(infoJson), 1);
        }

        // DungeonEvents — personal dungeon cleared
        private void OnDungeonWin(ulong winnerId, string mapName)
        {
            if (!config.EventIntegrations.TrackDungeonEvents) return;
            var player = BasePlayer.FindByID(winnerId);
            if (player == null) return;
            AwardProgress(player, "dungeon_win", mapName?.ToLower() ?? "", 1);
        }

        // VirtualQuarries
        private void OnQuarryUpgraded(BasePlayer player, int level, string profile)
        {
            if (!config.EventIntegrations.TrackVirtualQuarries || player == null || player.IsNpc) return;
            AwardProgress(player, "quarry_upgrade", profile?.ToLower() ?? "", 1);
        }

        private void OnQuarryPlaced(BasePlayer player, string profile)
        {
            if (!config.EventIntegrations.TrackVirtualQuarries || player == null || player.IsNpc) return;
            AwardProgress(player, "quarry_place", profile?.ToLower() ?? "", 1);
        }

        // SkillTree — player reached a new level
        private void STOnPlayerLevel(BasePlayer player, int oldLevel, int newLevel)
        {
            if (!config.EventIntegrations.TrackSkillTreeLevels || player == null || player.IsNpc) return;
            AwardProgress(player, "skilltree_level", newLevel.ToString(), 1);
        }

        // InfinitumBradleyDrops — custom bradley profile killed
        private void OnBradleyTierKilled(BasePlayer player, string profileName)
        {
            if (!config.EventIntegrations.TrackBradleyTiers || player == null || player.IsNpc) return;
            AwardProgress(player, "bradley_tier", profileName?.ToLower() ?? "", 1);
        }

        // ZombieHunter — track active zombie entity net IDs so OnEntityDeath can tag them
        private void OnZombieActive(BaseEntity zombie)
        {
            if (!config.EventIntegrations.TrackZombieHunter || zombie?.net == null) return;
            _zombieHunterIds.Add(zombie.net.ID.Value);
        }

        private void OnZombieInactive(BaseEntity zombie)
        {
            if (zombie?.net == null) return;
            _zombieHunterIds.Remove(zombie.net.ID.Value);
        }

        // Extracts a difficulty label from the RaidableBases infoJson payload.
        // The JSON contains "difficulty" (int 0-4) or "difficultyName" (string).
        // We prefer the name if present; otherwise map the int ourselves.
        private static string ParseRbDifficulty(string infoJson)
        {
            if (string.IsNullOrEmpty(infoJson)) return "any";
            try
            {
                var obj = JObject.Parse(infoJson);
                // Some builds expose a pre-formatted string directly
                JToken nameToken;
                if (obj.TryGetValue("difficultyName", StringComparison.OrdinalIgnoreCase, out nameToken))
                {
                    string n = nameToken.ToString().Trim().ToLower();
                    if (!string.IsNullOrEmpty(n)) return n;
                }
                JToken lvlToken;
                if (obj.TryGetValue("difficulty", StringComparison.OrdinalIgnoreCase, out lvlToken))
                    return RbDifficultyName(lvlToken.Value<int>());
            }
            catch { /* malformed JSON — fall through */ }
            return "any";
        }

        // Maps RaidableBases integer difficulty (0-4) to a consistent quest target string.
        // Quest configs should use these values in the Target field: "easy", "medium", "hard", "expert", "nightmare".
        // Use "any" (or leave Target blank) to accept all difficulties.
        private static string RbDifficultyName(int level)
        {
            switch (level)
            {
                case 0: return "easy";
                case 1: return "medium";
                case 2: return "hard";
                case 3: return "expert";
                case 4: return "nightmare";
                default: return "any";
            }
        }

        // ─────────────────────── Quest Logic ──────────────────────────────────
        private void AwardProgress(BasePlayer player, string type, string target, int amount, bool headshot = false)
        {
            if (player == null || !player.IsConnected) return;
            var data = GetOrCreate(player);
            if (data.ActiveQuests.Count == 0) return;

            bool changed = false;
            for (int qi = data.ActiveQuests.Count - 1; qi >= 0; qi--)
            {
                var aq  = data.ActiveQuests[qi];
                var def = GetQuest(aq.Id);
                if (def == null) continue;

                // Check expiry
                if (!string.IsNullOrEmpty(aq.ExpiresAt) && DateTime.UtcNow > DateTime.Parse(aq.ExpiresAt))
                {
                    data.ActiveQuests.RemoveAt(qi);
                    SendReply(player, Msg("QuestExpired", player.UserIDString, def.Title));
                    changed = true;
                    continue;
                }

                bool questChanged = false;
                for (int oi = 0; oi < def.Objectives.Count; oi++)
                {
                    var obj = def.Objectives[oi];
                    if (obj.Type != type) continue;
                    if (aq.Progress[oi] >= obj.Count) continue;
                    if (!MatchTarget(obj, target)) continue;
                    if (!MatchConditions(obj, headshot)) continue;
                    aq.Progress[oi] = Math.Min(aq.Progress[oi] + amount, obj.Count);
                    questChanged = true;
                    changed = true;

                    // Progress toast — suppressed when the whole quest is about to complete
                    // (CompleteQuest fires a chat message that already signals success)
                    if (config.ShowProgressToasts && !IsComplete(aq, def))
                    {
                        bool objDone  = aq.Progress[oi] >= obj.Count;
                        string tLabel = string.IsNullOrEmpty(obj.Description)
                            ? $"{ObjectiveTypeDisplay(obj.Type)} {obj.Target}"
                            : obj.Description;
                        string tMsg = objDone
                            ? $"✓  {tLabel}"
                            : $"↑  {tLabel}  {aq.Progress[oi]} / {obj.Count}";
                        ShowProgressToast(player, tMsg, objDone);
                    }
                }

                if (questChanged && IsComplete(aq, def))
                {
                    OnQuestObjectivesMet(player, data, qi);
                    changed = true;
                }
            }

            if (changed)
            {
                _dataDirty = true;   // flushed to disk by the 60-second save timer
                UpdateHud(player);
                if (_ui.ContainsKey(player.userID)) OpenUI(player);
            }
        }

        private bool MatchTarget(ObjectiveDef obj, string hit)
        {
            if (string.IsNullOrEmpty(obj.Target)) return true;
            string t = obj.Target.ToLower();
            string h = hit?.ToLower() ?? "";
            // One-directional: the incoming hit value must contain the quest target keyword.
            // Bidirectional would let a short hit like "wolf" match a quest target "wolfpack_elite".
            return h == t || h.Contains(t);
        }

        // Validates per-objective modifiers: headshot requirement and time-of-day condition.
        private static bool MatchConditions(ObjectiveDef obj, bool headshot)
        {
            if (obj.HeadshotOnly && !headshot) return false;
            if (!string.IsNullOrEmpty(obj.TimeCondition))
            {
                bool isNight = TOD_Sky.Instance?.IsNight ?? false;
                string cond  = obj.TimeCondition.ToLower();
                if (cond == "night" && !isNight) return false;
                if (cond == "day"   &&  isNight) return false;
            }
            return true;
        }

        private bool IsComplete(ActiveQuest aq, QuestDefinition def)
        {
            if (def.ObjectiveLogic == "ANY")
            {
                for (int i = 0; i < def.Objectives.Count; i++)
                    if (aq.Progress[i] >= def.Objectives[i].Count) return true;
                return false;
            }
            for (int i = 0; i < def.Objectives.Count; i++)
                if (aq.Progress[i] < def.Objectives[i].Count) return false;
            return true;
        }

        // Phase 1 — all objectives met: park in ReadyToCollect, notify player.
        // Rewards are NOT delivered yet; the player must click "Collect Rewards" in the UI.
        private void OnQuestObjectivesMet(BasePlayer player, PlayerData data, int activeIdx)
        {
            var aq  = data.ActiveQuests[activeIdx];
            var def = GetQuest(aq.Id);
            data.ActiveQuests.RemoveAt(activeIdx);
            if (def == null) return;

            data.ReadyToCollect.Add(aq);
            SendReply(player, Msg("QuestReady", player.UserIDString, def.Title));

            if (config.PlayObjectivesSound && !string.IsNullOrEmpty(config.ObjectivesSoundPrefab))
                Effect.server.Run(config.ObjectivesSoundPrefab, player.transform.position);
        }

        // Phase 2 — player clicks collect: deliver rewards, record completion, announce.
        private void CollectQuestRewards(BasePlayer player, string questId)
        {
            var data = GetOrCreate(player);
            ActiveQuest aq = null;
            int idx = -1;
            for (int i = 0; i < data.ReadyToCollect.Count; i++)
            {
                if (data.ReadyToCollect[i].Id != questId) continue;
                aq = data.ReadyToCollect[i]; idx = i; break;
            }
            if (aq == null) return;

            var def = GetQuest(questId);
            if (def == null) { data.ReadyToCollect.RemoveAt(idx); return; }

            data.ReadyToCollect.RemoveAt(idx);
            FinalizeQuestRewards(player, data, def);

            UpdateHud(player);
            OpenUI(player);
            SavePlayerData();
        }

        // Shared finalisation: record, rewards, tier check, sound, announce, streak, chain.
        private void FinalizeQuestRewards(BasePlayer player, PlayerData data, QuestDefinition def)
        {
            // Record completion
            bool found = false;
            for (int i = 0; i < data.Completed.Count; i++)
            {
                if (data.Completed[i].Id != def.Id) continue;
                data.Completed[i].Times++;
                data.Completed[i].At = DateTime.UtcNow.ToString("o");
                found = true; break;
            }
            if (!found)
                data.Completed.Add(new CompletedRecord { Id = def.Id, At = DateTime.UtcNow.ToString("o"), Times = 1 });

            if (def.Repeatable && def.CooldownSeconds > 0)
            {
                bool isVip  = permission.UserHasPermission(player.UserIDString, PERM_VIP);
                int cdSecs  = (isVip && def.VipCooldownSeconds > 0) ? def.VipCooldownSeconds : def.CooldownSeconds;
                data.Cooldowns[def.Id] = DateTime.UtcNow.AddSeconds(cdSecs).ToString("o");
            }

            // ── Streak update BEFORE rewards so the bonus is reflected in AwardRewards ──
            if (def.Daily)
            {
                EnsureDailyWindowFresh(data, player); // roll the 24-hour window if needed
                if (!data.DailyCompleted.Contains(def.Id))
                {
                    data.DailyCompleted.Add(def.Id);
                    // First daily completion in this window — increment streak
                    if (data.DailyCompleted.Count == 1)
                    {
                        data.Streak++;
                        int bonus = Math.Min(data.Streak, config.MaxStreakDays) * config.StreakBonusPercent;
                        SendReply(player, Msg("StreakExtended", player.UserIDString, data.Streak, bonus));
                    }
                }
            }

            int tierBefore = GetTierIndex(data);
            AwardRewards(player, data, def);
            int tierAfter  = GetTierIndex(data);

            SendReply(player, Msg("QuestCollected", player.UserIDString, def.Title));
            if (tierAfter > tierBefore)
                SendReply(player, Msg("TierUp", player.UserIDString, TierNames[tierAfter]));

            if (config.PlayCompleteSound && !string.IsNullOrEmpty(config.CompleteSoundPrefab))
                Effect.server.Run(config.CompleteSoundPrefab, player.transform.position);

            if (config.AnnounceCompletions)
                Server.Broadcast(Msg("Announce", null, player.displayName, def.Title));

            if (!string.IsNullOrEmpty(config.DiscordWebhook))
                PostDiscord($"**{player.displayName}** completed **{def.Title}**");

            // Chain check
            CheckChainCompletion(player, data, def);
        }

        private bool AcceptQuest(BasePlayer player, string questId)
        {
            var def  = GetQuest(questId);
            var data = GetOrCreate(player);

            if (def == null) { SendReply(player, Msg("QuestNotFound", player.UserIDString)); return false; }
            if (IsActive(data, questId)) { SendReply(player, Msg("QuestAlreadyActive", player.UserIDString)); return false; }
            if (!def.Repeatable && IsCompleted(data, questId)) { SendReply(player, Msg("QuestCompleted", player.UserIDString)); return false; }

            if (data.Cooldowns.TryGetValue(questId, out string cdStr) && DateTime.UtcNow < DateTime.Parse(cdStr))
            {
                TimeSpan left = DateTime.Parse(cdStr) - DateTime.UtcNow;
                SendReply(player, Msg("QuestOnCooldown", player.UserIDString, FormatTime(left)));
                return false;
            }

            int playerTier = GetTierIndex(data);
            int questTier  = TierIndexFromName(def.Tier);
            if (questTier > playerTier) { SendReply(player, Msg("QuestTierLocked", player.UserIDString, def.Tier)); return false; }

            if (!PrereqsMet(data, def)) { SendReply(player, Msg("QuestPrereqMissing", player.UserIDString)); return false; }

            if (data.ActiveQuests.Count >= TierSlots[playerTier]) { SendReply(player, Msg("QuestSlotsFull", player.UserIDString)); return false; }

            if (!string.IsNullOrEmpty(def.Permission) && !permission.UserHasPermission(player.UserIDString, def.Permission))
            {
                SendReply(player, Msg("NoPermission", player.UserIDString)); return false;
            }

            var aq = new ActiveQuest
            {
                Id         = def.Id,
                AcceptedAt = DateTime.UtcNow.ToString("o"),
                ExpiresAt  = def.TimeLimitMinutes > 0 ? DateTime.UtcNow.AddMinutes(def.TimeLimitMinutes).ToString("o") : ""
            };
            for (int i = 0; i < def.Objectives.Count; i++) aq.Progress.Add(0);

            // Assign a specific Contractor NPC for each deliver objective
            foreach (var obj in def.Objectives)
            {
                if (obj.Type?.ToLower() != "deliver") continue;
                ScientistNPC target = AssignDeliveryNpc(obj.Location);  // Location = "outpost"/"bandit"/etc, empty = random
                if (target != null) aq.DeliveryNpcId = target.net.ID.Value;
            }

            data.ActiveQuests.Add(aq);

            SendReply(player, Msg("QuestAccepted", player.UserIDString, def.Title));
            UpdateHud(player);
            SavePlayerData();
            return true;
        }

        private void AbandonQuest(BasePlayer player, string questId)
        {
            var data = GetOrCreate(player);
            // Check in-progress quests
            for (int i = data.ActiveQuests.Count - 1; i >= 0; i--)
            {
                if (data.ActiveQuests[i].Id != questId) continue;
                var def = GetQuest(questId);
                data.ActiveQuests.RemoveAt(i);
                SendReply(player, Msg("QuestAbandoned", player.UserIDString, def?.Title ?? questId));
                UpdateHud(player);
                SavePlayerData();
                return;
            }
            // Also allow abandoning from ready-to-collect (player changed their mind)
            for (int i = data.ReadyToCollect.Count - 1; i >= 0; i--)
            {
                if (data.ReadyToCollect[i].Id != questId) continue;
                var def = GetQuest(questId);
                data.ReadyToCollect.RemoveAt(i);
                SendReply(player, Msg("QuestAbandoned", player.UserIDString, def?.Title ?? questId));
                UpdateHud(player);
                SavePlayerData();
                return;
            }
        }

        // ─────────────────────── Reward Delivery ──────────────────────────────
        private void AwardRewards(BasePlayer player, PlayerData data, QuestDefinition def)
        {
            AwardRewardList(player, data, def.Rewards, def.Daily, def.Daily ? data.Streak : 0);

            // VIP bonus rewards — only given if player has infinitumquests.vip permission
            if (def.VipRewards != null && def.VipRewards.Count > 0
                && permission.UserHasPermission(player.UserIDString, PERM_VIP))
            {
                AwardRewardList(player, data, def.VipRewards, def.Daily, def.Daily ? data.Streak : 0);
                SendReply(player, Msg("VipBonusCollected", player.UserIDString));
            }
        }

        private void AwardRewardList(BasePlayer player, PlayerData data, List<RewardDef> rewards, bool isDaily, int streak)
        {
            foreach (var r in rewards)
            {
                switch (r.Type.ToLower())
                {
                    case "item":
                    {
                        var itemDef = ItemManager.FindItemDefinition(r.Shortname);
                        if (itemDef == null) { PrintWarning($"[IQ] Unknown item shortname: {r.Shortname}"); break; }
                        var item = ItemManager.Create(itemDef, r.Amount, r.SkinId);
                        if (!string.IsNullOrEmpty(r.CustomName)) item.name = r.CustomName;
                        if (!player.inventory.GiveItem(item))
                            item.Drop(player.eyes.position, player.eyes.BodyForward() * 2f);
                        break;
                    }
                    case "blueprint":
                    {
                        var itemDef = ItemManager.FindItemDefinition(r.Shortname);
                        if (itemDef == null) break;
                        var bp = ItemManager.CreateByName("blueprintbase", 1);
                        if (bp != null) { bp.blueprintTarget = itemDef.itemid; if (!player.inventory.GiveItem(bp)) bp.Drop(player.eyes.position, Vector3.up); }
                        break;
                    }
                    case "command":
                        if (!string.IsNullOrEmpty(r.Command))
                            Server.Command(r.Command.Replace("%STEAMID%", player.UserIDString));
                        break;
                    case "tier_xp":
                    {
                        int streakBonus = isDaily
                            ? (int)(r.Amount * Math.Min(streak, config.MaxStreakDays) * config.StreakBonusPercent / 100f)
                            : 0;
                        data.TierXP += r.Amount + streakBonus;
                        // Also forward to SkillTree — tier_xp is the XP reward players see in the UI
                        if (config.UseSkillTree && SkillTree != null)
                            SkillTree.Call("AwardXP", player, (double)(r.Amount + streakBonus), Name);
                        break;
                    }
                    case "reputation":
                    {
                        int streakBonus = isDaily
                            ? (int)(r.Amount * Math.Min(streak, config.MaxStreakDays) * config.StreakBonusPercent / 100f)
                            : 0;
                        data.Reputation += r.Amount + streakBonus;
                        break;
                    }
                    case "economics":
                        if (config.UseEconomics && Economics != null)
                            Economics.Call("Deposit", player.UserIDString, (double)r.Amount);
                        break;
                    case "server_rewards":
                        if (config.UseServerRewards && ServerRewards != null)
                            ServerRewards.Call("AddPoints", player.userID, r.Amount);
                        break;
                    case "skill_xp":
                        if (config.UseSkillTree && SkillTree != null)
                            SkillTree.Call("AwardXP", player, (double)r.Amount, Name);
                        break;
                }
            }
        }

        // ─────────────────────── Per-Player Daily Window ───────────────────────
        // Rolling 24-hour window per player — no global UTC-midnight reset.
        // Called on connect, quest completion, and the HUD refresh timer.
        private void EnsureDailyWindowFresh(PlayerData data, BasePlayer player = null)
        {
            DateTime windowStart;
            bool expired = string.IsNullOrEmpty(data.DailyWindowStart)
                || !DateTime.TryParse(data.DailyWindowStart, out windowStart)
                || (DateTime.UtcNow - windowStart).TotalHours >= 24.0;
            if (!expired) return;

            // Previous window existed but player didn't complete any daily → break streak
            if (!string.IsNullOrEmpty(data.DailyWindowStart) && data.DailyCompleted.Count == 0 && data.Streak > 0)
                data.Streak = 0;

            data.DailyCompleted.Clear();
            data.DailyWindowStart = DateTime.UtcNow.ToString("o");
            _dataDirty = true;

            if (player != null && player.IsConnected)
                SendReply(player, Msg("DailyReset", player.UserIDString));
        }

        // ─────────────────────── Chain Completion ─────────────────────────────
        private void CheckChainCompletion(BasePlayer player, PlayerData data, QuestDefinition justCompleted)
        {
            if (string.IsNullOrEmpty(justCompleted.ChainId)) return;
            if (data.CompletedChains.Contains(justCompleted.ChainId)) return;

            // Get all quests in this chain
            var chain = new List<QuestDefinition>();
            foreach (var q in _quests)
                if (q.ChainId == justCompleted.ChainId) chain.Add(q);
            if (chain.Count == 0) return;

            // Check if all chain quests are completed
            foreach (var q in chain)
                if (!IsCompleted(data, q.Id)) return;

            // All done — award chain bonus from the quest with highest ChainOrder
            chain.Sort((a, b) => b.ChainOrder.CompareTo(a.ChainOrder));
            var last = chain[0];
            if (last.ChainBonusRewards != null && last.ChainBonusRewards.Count > 0)
            {
                // Temporarily create a dummy QuestDefinition wrapper to reuse AwardRewards
                var dummy = new QuestDefinition { Rewards = last.ChainBonusRewards };
                AwardRewards(player, data, dummy);
                SendReply(player, Msg("ChainBonusAwarded", player.UserIDString));
            }

            data.CompletedChains.Add(justCompleted.ChainId);

            // Prefer the last quest's ChainTitle (the definitive series name).
            // Fall back to the completed quest's title, then the raw ChainId.
            string chainName = !string.IsNullOrEmpty(last.ChainTitle)          ? last.ChainTitle
                             : !string.IsNullOrEmpty(justCompleted.ChainTitle) ? justCompleted.ChainTitle
                             : justCompleted.ChainId;
            if (config.AnnounceChains)
                foreach (var p in BasePlayer.activePlayerList)
                    SendReply(p, Msg("ChainComplete", p.UserIDString, player.displayName, chainName));
        }

        // ─────────────────────── Tier Helpers ─────────────────────────────────
        private int GetTierIndex(PlayerData data)
        {
            // Tier advancement is driven by TierXP accumulated through "tier_xp" rewards.
            int xp = data.TierXP;
            for (int i = TierThresholds.Length - 1; i >= 0; i--)
                if (xp >= TierThresholds[i]) return i;
            return 0;
        }

        private int TierIndexFromName(string name)
        {
            for (int i = 0; i < TierNames.Length; i++)
                if (TierNames[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        private bool PrereqsMet(PlayerData data, QuestDefinition def)
        {
            if (def.RequiredIds == null) return true;
            foreach (var id in def.RequiredIds)
                if (!IsCompleted(data, id)) return false;
            return true;
        }

        private bool IsActive(PlayerData data, string id)
        {
            for (int i = 0; i < data.ActiveQuests.Count; i++)
                if (data.ActiveQuests[i].Id == id) return true;
            for (int i = 0; i < data.ReadyToCollect.Count; i++)
                if (data.ReadyToCollect[i].Id == id) return true;
            return false;
        }

        private ActiveQuest GetReadyQuest(PlayerData data, string id)
        {
            for (int i = 0; i < data.ReadyToCollect.Count; i++)
                if (data.ReadyToCollect[i].Id == id) return data.ReadyToCollect[i];
            return null;
        }

        private bool IsCompleted(PlayerData data, string id)
        {
            for (int i = 0; i < data.Completed.Count; i++)
                if (data.Completed[i].Id == id) return true;
            return false;
        }

        private bool IsOnCooldown(PlayerData data, string id, out string remaining)
        {
            remaining = "";
            if (!data.Cooldowns.TryGetValue(id, out string cdStr)) return false;
            var expires = DateTime.Parse(cdStr);
            if (DateTime.UtcNow >= expires) { data.Cooldowns.Remove(id); return false; }
            remaining = FormatTime(expires - DateTime.UtcNow);
            return true;
        }

        private QuestDefinition GetQuest(string id)
        {
            for (int i = 0; i < _quests.Count; i++)
                if (_quests[i].Id == id) return _quests[i];
            return null;
        }

        private List<QuestDefinition> FilteredQuests(BasePlayer player, UiState state)
        {
            var data = GetOrCreate(player);
            string search = state.Search?.Trim().ToLower() ?? "";
            var result = new List<QuestDefinition>();
            foreach (var q in _quests)
            {
                if (!string.IsNullOrEmpty(state.Tier) && !q.Tier.Equals(state.Tier, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(state.Cat)  && !q.Category.Equals(state.Cat,  StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(q.Permission) && !permission.UserHasPermission(player.UserIDString, q.Permission)) continue;
                if (!q.Repeatable && IsCompleted(data, q.Id)) continue;
                if (!string.IsNullOrEmpty(search) &&
                    !q.Title.ToLower().Contains(search) &&
                    !q.Description.ToLower().Contains(search) &&
                    !q.Category.ToLower().Contains(search)) continue;
                result.Add(q);
            }
            return result;
        }

        // ─────────────────────── CUI — Main ───────────────────────────────────
        private void OpenUI(BasePlayer player, string tab = null, int page = 0)
        {
            var data = GetOrCreate(player);

            if (!_ui.TryGetValue(player.userID, out var state))
            { state = new UiState(); _ui[player.userID] = state; }
            state.Mode = data.UiMode; // always sync from persisted preference

            if (tab != null) { state.Tab = tab; state.Page = page; if (tab != "ranks") { state.SelectedPlayer = 0; state.Detail = ""; } }

            CuiHelper.DestroyUi(player, UI_MAIN);
            var c = new CuiElementContainer();

            bool fullscreen = data.UiMode == "fullscreen";

            // Backdrop — dim + cursor
            c.Add(new CuiElement
            {
                Name = UI_MAIN, Parent = "Overlay",
                Components = {
                    new CuiImageComponent { Color = fullscreen ? "0 0 0 0" : "0 0 0 0.22" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiNeedsCursorComponent()
                }
            });

            int tier = GetTierIndex(data);

            // Main panel — window (1080×640 centred) or fullscreen (2% margin on all sides)
            string P = "IQ_P";
            var panelRect = fullscreen
                ? new CuiRectTransformComponent { AnchorMin = "0.02 0.03", AnchorMax = "0.98 0.97" }
                : new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-540 -320", OffsetMax = "540 320" };

            c.Add(new CuiElement
            {
                Name = P, Parent = UI_MAIN,
                Components = {
                    new CuiImageComponent { Color = C_BG1, Material = "assets/content/ui/uibackgroundblur.mat" },
                    panelRect
                }
            });

            // Tier accent left bar (3px) — drawn on IQ_P so it sits above the blur
            UIPanel(c, P, "", TierColors[tier], "0 0", "0 1", 0, 0, 3, 0);

            // Premium border — thin accent lines drawn ON IQ_P (not behind, avoids blur tint bleed)
            string borderCol = UIColor(config.ThemeColor, 0.60f);
            UIPanel(c, P, "", borderCol, "0 1", "1 1", 0, -1,  0,  0);  // top
            UIPanel(c, P, "", borderCol, "0 0", "1 0", 0,  0,  0,  1);  // bottom
            UIPanel(c, P, "", borderCol, "1 0", "1 1", -1, 0,  0,  0);  // right

            // Header 56px
            DrawHeader(c, P, player, data, tier, state);

            // Footer strip 34px
            UIPanel(c, P, "", C_BG2, "0 0", "1 0", 3, 0, 0, 34);
            UILabel(c, P, C_TXT_MD, $"INFINITUM CONTRACTOR NETWORK  ·  v{Version}", 8, "0.2 0", "0.8 0", 0, 0, 0, 34, TextAnchor.MiddleCenter);

            // Left sidebar 260px
            string L = "IQ_L";
            UIPanel(c, P, L, C_BG3, "0 0", "0 1", 3, 34, 263, -56);

            // Divider
            UIGlowLine(c, P, "0 0", "0 1", 264, 34, 265, -56);

            // Right panel ~635px
            string R = "IQ_R";
            UIPanel(c, P, R, C_BG1, "0 0", "1 1", 265, 34, 0, -56);

            DrawLeftSidebar(c, L, player, state, data);
            DrawRightPanel(c, R, player, state, data);

            CuiHelper.AddUi(player, c);
        }

        private void CloseUI(BasePlayer player)
        {
            _ui.Remove(player.userID);
            CuiHelper.DestroyUi(player, UI_MAIN);
            CuiHelper.DestroyUi(player, UI_REWARDLIST);
        }

        // Partial redraw — only replaces sidebar + right panel, keeps header/footer/background intact (no full-screen flicker)
        private void RefreshPanels(BasePlayer player)
        {
            if (!_ui.TryGetValue(player.userID, out var state)) return;
            var data = GetOrCreate(player);

            CuiHelper.DestroyUi(player, "IQ_L");
            CuiHelper.DestroyUi(player, "IQ_R");
            CuiHelper.DestroyUi(player, UI_REWARDLIST);

            var c = new CuiElementContainer();
            string P = "IQ_P";

            string L = "IQ_L";
            UIPanel(c, P, L, C_BG3, "0 0", "0 1", 3, 34, 263, -56);

            string R = "IQ_R";
            UIPanel(c, P, R, C_BG1, "0 0", "1 1", 265, 34, 0, -56);

            DrawLeftSidebar(c, L, player, state, data);
            DrawRightPanel(c, R, player, state, data);

            CuiHelper.AddUi(player, c);
        }

        // ─────────────────────── CUI — Header ─────────────────────────────────
        private void DrawHeader(CuiElementContainer c, string P, BasePlayer player, PlayerData data, int tier, UiState state)
        {
            // Header bg with subtle bottom border
            UIPanel(c, P, "", C_BG2, "0 1", "1 1", 3, -56, 0, 0);
            UIGlowLine(c, P, "0 1", "1 1", 3, -57, 0, -56);

            // Wordmark — centered: "INFINITUM" right-aligned + "QUESTS" left-aligned so they sit flush
            UILabel(c, P, C_TXT_HI, "INFINITUM", 17, "0.5 1", "0.5 1", -160, -44, -6, -8, TextAnchor.MiddleRight, true);
            UILabel(c, P, UIColor(config.ThemeColor, 1f), "QUESTS", 17, "0.5 1", "0.5 1", 6, -44, 160, -8, TextAnchor.MiddleLeft, true);

            // Tier button — "Tier: RECRUIT" pill, top-left below wordmark area
            string tn  = TierNames[tier];
            float pw   = "Tier: ".Length * 6.0f + tn.Length * 7.2f + 22f;
            UIPanel(c, P, "", TierBg[tier],    "0 1", "0 1", 14, -42, 14 + pw, -14);
            UIPanel(c, P, "", TierColors[tier], "0 1", "0 1", 14, -42, 17,      -14);
            UIButton(c, P, "0 0 0 0", TierColors[tier],
                $"Tier: {tn}", 9, "0 1", "0 1", 20, -42, 14 + pw - 2, -14,
                "iq.ui tab ranks", true);

            // Stats row
            string streakStr = data.Streak > 0 ? $"  ·  +{data.Streak}d streak" : "";
            string stats = $"{data.TotalCompletions()} completed  ·  {data.Reputation:N0} rep  ·  {data.ActiveQuests.Count}/{TierSlots[tier]} slots{streakStr}";
            UILabel(c, P, C_TXT_MD, stats, 9, "0 1", "0.72 1", 14, -56, 0, -44, TextAnchor.MiddleLeft);

            // Mode toggle button — distinct background so it's clearly a button
            bool isFullscreen = state.Mode == "fullscreen";
            string modeLabel  = isFullscreen ? "⊟ WIN" : "⊞ FULL";
            string modeTarget = isFullscreen ? "window" : "fullscreen";
            // Subtle dark bg panel + theme-colour text makes it pop against the header
            UIPanel(c, P, "", C_BTN, "1 1", "1 1", -100, -48, -48, -10);
            UIButton(c, P, "0 0 0 0", UIColor(config.ThemeColor, 0.90f), modeLabel, 8, "1 1", "1 1", -100, -48, -48, -10, $"iq.ui mode {modeTarget}", true);

            // Close button
            UIButton(c, P, C_ERR_BG, C_ERR, "✕", 12, "1 1", "1 1", -44, -48, -6, -10, "iq.ui close", true);
        }

        // ─────────────────────── CUI — Tier Progression (Ranks right panel) ──
        private void DrawTierProgression(CuiElementContainer c, string parent, PlayerData data)
        {
            int curTier = GetTierIndex(data);
            int curXp   = data.TierXP;

            const float ROW = 54f;

            // Header
            UILabel(c, parent, UIColor(config.ThemeColor, 1f), "TIER PROGRESSION", 13,
                "0 1", "1 1", 20, -50, -20, -10, TextAnchor.MiddleLeft, true);
            UILabel(c, parent, C_TXT_MD, "Earn XP from quest rewards to advance tiers and unlock more contract slots.",
                9, "0 1", "1 1", 20, -72, -20, -50, TextAnchor.MiddleLeft);
            UIGlowLine(c, parent, "0 1", "1 1", 20, -74, -20, -73);

            // Tier rows — Legend at top, Recruit at bottom
            float ry = -80f;
            for (int i = TierNames.Length - 1; i >= 0; i--)
            {
                bool isCur   = i == curTier;
                bool isDone  = i < curTier;
                bool isLocked = !isCur && !isDone;

                // Row bg: full tier-color tint (active = full TierBg, others = lighter TierRowBg)
                string rowBg   = isCur ? TierBg[i] : TierRowBg[i];
                string nameCol = isCur ? TierColors[i] : (isDone ? C_OK : C_TXT_MD);
                string xpText;

                if (i == 0)
                    xpText = "Starting tier";
                else if (isDone)
                    xpText = $"✓  {TierThresholds[i]:N0} XP";
                else if (isCur && i < TierThresholds.Length - 1)
                    xpText = $"{curXp:N0} / {TierThresholds[i + 1]:N0} XP";
                else if (i >= TierThresholds.Length - 1)
                    xpText = isCur ? $"{curXp:N0} XP  — MAX" : $"{TierThresholds[i]:N0} XP";
                else
                    xpText = $"{TierThresholds[i]:N0} XP required";

                // Row background: blurred glass base + subtle tier-color tint
                UIPanel(c, parent, "", isCur ? "0 0 0 0.35" : "0 0 0 0.12", "0 1", "1 1", 0, ry - ROW, 0, ry, MAT_BLUR);
                UIPanel(c, parent, "", rowBg, "0 1", "1 1", 0, ry - ROW, 0, ry);
                // Thin separator between rows
                UIPanel(c, parent, "", C_DIV, "0 1", "1 1", 6, ry - ROW, 0, ry - ROW + 1f);
                // Thick left accent bar
                UIPanel(c, parent, "", TierColors[i],  "0 1", "0 1", 0,  ry - ROW, 6,  ry);

                // Current tier: active arrow indicator
                if (isCur)
                    UILabel(c, parent, TierColors[i], "▶", 11, "0 1", "0 1", 8, ry - ROW, 24, ry, TextAnchor.MiddleCenter, true);

                // Tier name — always bold
                UILabel(c, parent, nameCol, TierNames[i], 13,
                    "0 1", "0.45 1", 26, ry - ROW, 0, ry, TextAnchor.MiddleLeft, true);

                // Locked badge — vertically centered beside the tier name
                if (isLocked)
                {
                    float mid = ry - ROW * 0.5f;  // vertical center of this row
                    UIPanel(c, parent, "", "0.150 0.018 0.018 0.80", "0 1", "0 1", 152, mid - 8, 210, mid + 8);
                    UILabel(c, parent, C_ERR, "LOCKED", 7, "0 1", "0 1", 152, mid - 8, 210, mid + 8, TextAnchor.MiddleCenter, true);
                }

                UILabel(c, parent, C_TXT_HI, $"{TierSlots[i]} slots", 9,
                    "0.45 1", "0.65 1", 0, ry - ROW, 0, ry, TextAnchor.MiddleCenter);

                UILabel(c, parent, isCur ? C_TXT_HI : (isDone ? C_OK : C_TXT_MD), xpText, 9,
                    "0.65 1", "1 1", 0, ry - ROW, -16, ry, TextAnchor.MiddleRight);

                ry -= ROW;
            }

            // XP progress bar
            if (curTier < TierThresholds.Length - 1)
            {
                int prev  = TierThresholds[curTier];
                int next  = TierThresholds[curTier + 1];
                float pct = Mathf.Clamp01((float)(curXp - prev) / (next - prev));
                float barY = ry - 14f;

                // Progress text — split so tier name renders in its tier color
                string pctLabel  = $"{(int)(pct * 100)}%  to  ";
                string tierLabel = TierNames[curTier + 1];
                string barId = $"{parent}_TPBar";
                UIPanel(c, parent, barId, C_BTN, "0 1", "1 1", 20, barY - 10, -20, barY);
                // Progress text below the bar
                UILabel(c, parent, C_TXT_HI,               pctLabel,  9, "0 1", "0.5 1", 20,  barY - 24, 0,   barY - 12, TextAnchor.MiddleRight);
                UILabel(c, parent, TierColors[curTier + 1], tierLabel, 9, "0.5 1", "1 1",  0,  barY - 24, -20, barY - 12, TextAnchor.MiddleLeft);
                if (pct > 0f)
                    UIPanel(c, barId, "", TierColors[curTier], "0 0", $"{pct:F3} 1", 0, 0, 0, 0);
            }
        }

        // ─────────────────────── CUI — Left Sidebar ───────────────────────────
        private void DrawLeftSidebar(CuiElementContainer c, string L, BasePlayer player, UiState state, PlayerData data)
        {
            // Tab row 30px at top
            DrawSideTab(c, L, "BOARD",    "board",    state.Tab, 0f,       0.25f);
            DrawSideTab(c, L, "ACTIVE",   "active",   state.Tab, 0.25f,    0.5f);
            DrawSideTab(c, L, "ARCHIVES", "archives", state.Tab, 0.5f,     0.75f);
            DrawSideTab(c, L, "RANKS",    "ranks",    state.Tab, 0.75f,    1f);
            UIGlowLine(c, L, "0 1", "1 1", 0, -31, 0, -30);

            bool isBoard = state.Tab == "board";

            // Category filter chips (board tab only, 24px row)
            var catSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (isBoard)
                foreach (var q in _quests)
                    if (!string.IsNullOrEmpty(q.Category)) catSet.Add(q.Category);

            bool showCatRow = isBoard && catSet.Count > 1;
            if (showCatRow)
            {
                UIPanel(c, L, "", C_BG0, "0 1", "1 1", 0, -55, 0, -31);
                var cats = new List<string>(catSet);
                cats.Sort();
                cats.Insert(0, "");
                float cx = 4f;
                foreach (var cat in cats)
                {
                    string label = string.IsNullOrEmpty(cat) ? "ALL" : cat.ToUpper();
                    bool sel  = string.Equals(state.Cat, cat, StringComparison.OrdinalIgnoreCase);
                    float cw  = label.Length * 6.2f + 10f;
                    string bg  = sel ? UIColor(config.ThemeColor, 0.75f) : "0.180 0.178 0.210 0.90";
                    string txt = sel ? "0.04 0.04 0.04 1" : C_TXT_HI;
                    UIButton(c, L, bg, txt, label, 7,
                        "0 1", "0 1", cx, -53, cx + cw, -33, $"iq.ui filter cat {cat}");
                    cx += cw + 3f;
                    if (cx > 240f) break;
                }
            }

            // Search bar (board tab only, 26px below category row)
            float catH = showCatRow ? 24f : 0f;
            if (isBoard)
            {
                float sbTop    = -(31f + catH);
                float sbBot    = sbTop - 26f;
                bool  hasSearch = !string.IsNullOrEmpty(state.Search);
                // Search bar — C_BTN panel IS the named container so it captures mouse events
                string SB = "IQ_SrchIn";
                UIPanel(c, L, SB, C_BTN, "0 1", "1 1", 4, sbBot + 2, hasSearch ? -26 : -4, sbTop - 2);
                // Left accent and magnifier inside the container
                UIPanel(c, SB, "", UIColor(config.ThemeColor, 0.55f), "0 0", "0 1", 0, 0, 2, 0);
                UILabel(c, SB, C_TXT_MD, "⌕", 12, "0 0", "0 1", 2, 0, 22, 0, TextAnchor.MiddleCenter);
                // Placeholder text BEFORE input field so the input is on top (later = higher in canvas order = captures clicks)
                if (!hasSearch)
                    UILabel(c, SB, C_TXT_DM, "search quests...", 9, "0 0", "1 1", 26, 0, 0, 0, TextAnchor.MiddleLeft);
                // Input fills container — added last so it is topmost and receives clicks
                c.Add(new CuiElement
                {
                    Parent = SB,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text          = state.Search ?? "",
                            FontSize      = 9,
                            Font          = "robotocondensed-regular.ttf",
                            Color         = C_TXT_HI,
                            Align         = TextAnchor.MiddleLeft,
                            Command       = "iq.ui search",
                            NeedsKeyboard = true,
                            CharsLimit    = 64,
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "24 2", OffsetMax = "-2 -2"
                        }
                    }
                });
                if (hasSearch)
                    UIButton(c, L, C_BTN, C_ERR, "✕", 8, "1 1", "1 1", -22, sbBot + 4, -4, sbTop - 4, "iq.ui search");
                UIPanel(c, L, "", C_DIV, "0 1", "1 1", 0, sbBot, 0, sbBot + 1);
            }

            // Compute list
            float tabsH = 31f + catH + (isBoard ? 26f : 0f);
            List<QuestDefinition> boardList = null;
            int itemCount;
            switch (state.Tab)
            {
                // Ready-to-collect quests appear at top of active list
                case "active":   itemCount = data.ReadyToCollect.Count + data.ActiveQuests.Count; break;
                case "archives": itemCount = data.Completed.Count;    break;
                case "ranks":    itemCount = 0; break;  // ranks draws its own list, no pagination
                default:
                    boardList = FilteredQuests(player, state);
                    itemCount = boardList.Count;
                    break;
            }

            int rowsPerPage = state.Mode == "fullscreen" ? RowsPerPageFs : RowsPerPageWin;
            int totalPages = Math.Max(1, (itemCount + rowsPerPage - 1) / rowsPerPage);
            int page  = Math.Min(state.Page, totalPages - 1);
            int start = page * rowsPerPage;
            int end   = Math.Min(start + rowsPerPage, itemCount);

            // List area panel — bottom offset 38 (not 34) to leave a 4px gap above the pagination bar
            string LL = "IQ_LL";
            UIPanel(c, L, LL, "0 0 0 0", "0 0", "1 1", 0, 38, 0, -tabsH);

            if (itemCount == 0)
            {
                string empty = state.Tab == "active"   ? "No active contracts." :
                               state.Tab == "archives" ? "No completed contracts." :
                                                         "No contracts match filters.";
                UILabel(c, LL, C_TXT_DM, empty, 10, "0.05 0.38", "0.95 0.62", 0, -12, 0, 12, TextAnchor.MiddleCenter);
            }
            else
            {
                switch (state.Tab)
                {
                    case "board":
                        for (int i = start; i < end; i++)
                            DrawListRow(c, LL, boardList[i], i - start, state.Detail == boardList[i].Id, data);
                        break;
                    case "active":
                        for (int i = start; i < end; i++)
                        {
                            if (i < data.ReadyToCollect.Count)
                            {
                                // Ready-to-collect shown at top with green badge
                                var rcDef = GetQuest(data.ReadyToCollect[i].Id);
                                if (rcDef != null)
                                    DrawListRow(c, LL, rcDef, i - start, state.Detail == rcDef.Id, data,
                                        isReady: true);
                            }
                            else
                            {
                                int ai = i - data.ReadyToCollect.Count;
                                var aqDef = GetQuest(data.ActiveQuests[ai].Id);
                                if (aqDef != null)
                                    DrawListRow(c, LL, aqDef, i - start, state.Detail == aqDef.Id, data,
                                        isActive: true, activeQuest: data.ActiveQuests[ai]);
                            }
                        }
                        break;
                    case "archives":
                        for (int i = start; i < end; i++)
                        {
                            int ri = data.Completed.Count - 1 - (start + (i - start));
                            if (ri < 0) continue;
                            var arDef = GetQuest(data.Completed[ri].Id);
                            if (arDef != null)
                                DrawListRow(c, LL, arDef, i - start, state.Detail == arDef.Id, data,
                                    isArchive: true, archiveRecord: data.Completed[ri]);
                        }
                        break;
                    case "ranks":
                        DrawRanksRows(c, LL, player, state);
                        break;
                }
            }

            // Pagination 34px at bottom
            UIPanel(c, L, "", C_BG2, "0 0", "1 0", 0, 0, 0, 34);
            UIGlowLine(c, L, "0 0", "1 0", 0, 33, 0, 34);
            if (page > 0)
                UIButton(c, L, UIColor(config.ThemeColor, 0.80f), C_BG0, "◀", 11,
                    "0 0", "0 0", 4, 5, 28, 29, $"iq.ui page {page - 1}", true);
            UILabel(c, L, C_TXT_DM, $"{page + 1} / {totalPages}", 9,
                "0 0", "1 0", 28, 0, -28, 34, TextAnchor.MiddleCenter);
            if (page < totalPages - 1)
                UIButton(c, L, UIColor(config.ThemeColor, 0.80f), C_BG0, "▶", 11,
                    "1 0", "1 0", -28, 5, -4, 29, $"iq.ui page {page + 1}", true);
        }

        private void DrawSideTab(CuiElementContainer c, string parent, string label, string tab,
            string current, float aL, float aR)
        {
            bool active = current == tab;
            string bg  = active ? UIColor(config.ThemeColor, 0.95f) : "0.055 0.053 0.062 0.98";
            string txt = active ? C_BG0 : C_TXT_MD;
            c.Add(new CuiButton
            {
                Button = { Color = bg, Command = $"iq.ui tab {tab}" },
                RectTransform = { AnchorMin = $"{aL:F4} 1", AnchorMax = $"{aR:F4} 1", OffsetMin = "1 -29", OffsetMax = "-1 -1" },
                Text = { Text = label, FontSize = 9, Color = txt,
                    Align = TextAnchor.MiddleCenter, Font = active ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf" }
            }, parent);
            // Active tab: accent bottom highlight strip
            if (active)
                UIPanel(c, parent, "", UIColor(config.ThemeColor, 0.7f),
                    $"{aL:F4} 1", $"{aR:F4} 1", 1, -30, -1, -28);
        }

        private void DrawListRow(CuiElementContainer c, string parent, QuestDefinition def, int idx,
            bool selected, PlayerData data, bool isActive = false, ActiveQuest activeQuest = null,
            bool isArchive = false, CompletedRecord archiveRecord = null, bool isReady = false)
        {
            float top = -(idx * ROW_H + 1f);
            float bot = top - (ROW_H - 2f);
            int tier = TierIndexFromName(def.Tier);

            // Compute active progress once
            float rowPct = -1f;
            int rowPctInt = 0;
            if (isActive && activeQuest != null && def.Objectives.Count > 0)
            {
                float prog = 0f;
                int oc = def.Objectives.Count;
                for (int oi = 0; oi < oc; oi++)
                {
                    int cur = oi < activeQuest.Progress.Count ? activeQuest.Progress[oi] : 0;
                    prog += Mathf.Clamp01((float)cur / def.Objectives[oi].Count);
                }
                rowPct    = oc > 0 ? prog / oc : 0f;
                rowPctInt = (int)(rowPct * 100f);
            }

            string rowBg = isReady  ? C_OK_BG :
                           selected ? C_SEL :
                           idx % 2 == 0 ? C_BG4 : C_BG5;
            UIPanel(c, parent, "", rowBg, "0 1", "1 1", 0, bot, 0, top);
            UIPanel(c, parent, "", isReady ? C_OK : TierColors[tier], "0 1", "0 1", 0, bot, 3, top);
            if (selected) UIPanel(c, parent, "", UIColor(config.ThemeColor, 0.95f), "1 1", "1 1", -3, bot, 0, top);

            // Bottom divider
            UIPanel(c, parent, "", C_DIV, "0 1", "1 1", 3, bot, 0, bot + 1f);


            string titleColor = isReady ? C_OK : C_TXT_HI;
            string title = def.Title.Length > 24 ? def.Title.Substring(0, 22) + ".." : def.Title;
            UILabel(c, parent, titleColor, title, 10,
                "0 1", "1 1", 7, bot + 14f, -36, top - 2f, TextAnchor.MiddleLeft, selected || isReady);

            string questTypeBadge = def.Weekly ? "✦ WEEKLY  ·  " : def.Daily ? "↻ DAILY  ·  " : "";
            string meta;
            if (isReady)
                meta = "★ COLLECT";
            else if (isActive && activeQuest != null)
                meta = $"{questTypeBadge}{def.Category}  ·  {rowPctInt}%";
            else if (isArchive && archiveRecord != null)
                meta = $"{questTypeBadge}{def.Category}  ·  x{archiveRecord.Times}";
            else
                meta = $"{questTypeBadge}{def.Category}  ·  {Stars(def.DifficultyStars)}";

            string metaColor = isReady ? C_OK : (selected ? UIColor(config.ThemeColor, 0.90f) : C_TXT_MD);
            UILabel(c, parent, metaColor, meta, 8,
                "0 1", "1 1", 7, bot + 2f, -36, bot + 16f, TextAnchor.MiddleLeft);

            // VIP badge (top-right corner, shown when quest has VIP rewards)
            bool hasVipRewards = def.VipRewards != null && def.VipRewards.Count > 0;
            float iconRightEdge = hasVipRewards ? -68f : -6f;
            if (hasVipRewards)
            {
                UIPanel(c, parent, "", "0.35 0.25 0.02 0.90", "1 1", "1 1", -65, bot + 24, -4, top - 4);
                UILabel(c, parent, "0.980 0.820 0.200 1", "★ VIP", 7, "1 1", "1 1", -65, bot + 24, -4, top - 4, TextAnchor.MiddleCenter, true);
            }

            // Objective icon (right edge, 22×22)
            if (def.Objectives.Count > 0)
            {
                var firstObj = def.Objectives[0];
                string fType = firstObj.Type?.ToLower() ?? "";
                if (fType == "kill")
                {
                    string killImg = GetKillObjectiveIcon(firstObj);
                    if (killImg != null)
                        UIImage(c, parent, killImg, "1 1", "1 1", iconRightEdge - 30, bot + 6, iconRightEdge, top - 6);
                }
                else
                {
                    int iconId = GetObjectiveIconItemId(firstObj);
                    if (iconId != 0)
                        UIItemIcon(c, parent, iconId, "1 1", "1 1", iconRightEdge - 30, bot + 6, iconRightEdge, top - 6);
                }
            }

            UIButton(c, parent, "0 0 0 0", "0 0 0 0", "", 1, "0 1", "1 1", 0, bot, 0, top, $"iq.ui detail {def.Id}");
        }

        private void DrawRanksRows(CuiElementContainer c, string parent, BasePlayer viewer, UiState state)
        {
            // Build sorted leaderboard
            var board = new List<KeyValuePair<ulong, PlayerData>>(_players);
            board.Sort((a, b) => b.Value.TotalCompletions().CompareTo(a.Value.TotalCompletions()));
            int count = Math.Min(board.Count, config.LeaderboardSize);

            for (int i = 0; i < count; i++)
            {
                var kv  = board[i];
                var pd  = kv.Value;
                bool me = kv.Key == viewer.userID;
                bool sel = state.SelectedPlayer == kv.Key;

                float top = -(i * ROW_H + 1f);
                float bot = top - (ROW_H - 2f);
                int t = GetTierIndex(pd);

                string bg = sel ? C_SEL : me ? "0.055 0.085 0.055 1" : i % 2 == 0 ? C_BG4 : C_BG5;
                UIPanel(c, parent, "", bg, "0 1", "1 1", 0, bot, 0, top);
                UIPanel(c, parent, "", TierColors[t], "0 1", "0 1", 0, bot, 3, top);
                if (sel) UIPanel(c, parent, "", UIColor(config.ThemeColor, 0.95f), "1 1", "1 1", -3, bot, 0, top);

                // Rank number
                string rankColor = i == 0 ? TierColors[3] : i == 1 ? C_TXT_MD : i == 2 ? C_WRN : C_TXT_DM;
                UILabel(c, parent, rankColor, $"#{i + 1}", 9, "0 1", "0 1", 5, bot + 13, 30, top - 2, TextAnchor.MiddleLeft, i < 3);

                // Name
                string nameColor = me ? C_OK : (sel ? C_TXT_HI : C_TXT_MD);
                string dname = pd.DisplayName.Length > 18 ? pd.DisplayName.Substring(0, 16) + ".." : pd.DisplayName;
                UILabel(c, parent, nameColor, dname, 10, "0 1", "1 1", 32, bot + 14, -5, top - 2, TextAnchor.MiddleLeft, sel || me);

                // Stats
                string meta = $"{TierNames[t]}  ·  {pd.TotalCompletions()} done  ·  {pd.Reputation:N0} rep";
                UILabel(c, parent, sel ? UIColor(config.ThemeColor, 0.85f) : C_TXT_DM, meta, 8,
                    "0 1", "1 1", 32, bot + 2, -5, bot + 16, TextAnchor.MiddleLeft);

                // Click target
                UIButton(c, parent, "0 0 0 0", "0 0 0 0", "", 1, "0 1", "1 1", 0, bot, 0, top,
                    $"iq.ui rank {kv.Key}");
            }

            if (count == 0)
                UILabel(c, parent, C_TXT_DM, "No ranked players yet.", 10, "0.05 0.38", "0.95 0.62", 0, -12, 0, 12, TextAnchor.MiddleCenter);
        }

        // ─────────────────────── CUI — Right Panel ────────────────────────────
        private void DrawRightPanel(CuiElementContainer c, string R, BasePlayer player, UiState state, PlayerData data)
        {
            // Ranks profile view
            if (state.Tab == "ranks")
            {
                if (state.SelectedPlayer != 0 && _players.TryGetValue(state.SelectedPlayer, out var rp))
                    DrawRankProfile(c, R, rp, state.SelectedPlayer == player.userID);
                else
                    DrawTierProgression(c, R, data);
                return;
            }

            if (string.IsNullOrEmpty(state.Detail))
            {
                bool noneActive = data.ActiveQuests.Count == 0 && data.ReadyToCollect.Count == 0;
                string hint = state.Tab == "active" && noneActive
                    ? "No active contracts.\nOpen BOARD to accept one."
                    : "← Select a contract";
                UILabel(c, R, C_TXT_DM, hint, 13, "0.1 0.32", "0.9 0.68", 0, -20, 0, 20, TextAnchor.MiddleCenter);
                return;
            }

            var def = GetQuest(state.Detail);
            if (def == null) { state.Detail = ""; DrawRightPanel(c, R, player, state, data); return; }

            var activeQ = GetActiveQuest(data, def.Id);
            var readyQ  = GetReadyQuest(data, def.Id);
            int tier = TierIndexFromName(def.Tier);

            // Title bar (66px) — green tint when ready to collect, otherwise tier-tinted
            string titleBg  = readyQ != null ? C_OK_BG  : TierBg[tier];
            string titleBar = readyQ != null ? C_OK      : TierColors[tier];
            UIPanel(c, R, "", titleBg,  "0 1", "1 1", 0, -66, 0, 0);
            UIPanel(c, R, "", titleBar, "0 1", "0 1", 0, -66, 3, 0);
            UIGlowLine(c, R, "0 1", "1 1", 0, -67, 0, -66);

            // Title + meta info (left side, narrowed for action button)
            UILabel(c, R, readyQ != null ? C_OK : C_TXT_HI,
                def.Title, 15, "0 1", "1 1", 12, -42, -122, -4, TextAnchor.MiddleLeft, true);
            bool   detailIsVip = permission.UserHasPermission(player.UserIDString, PERM_VIP);
            int    displayCd   = (detailIsVip && def.VipCooldownSeconds > 0) ? def.VipCooldownSeconds : def.CooldownSeconds;
            string cdFmt  = displayCd > 0 ? FormatTime(TimeSpan.FromSeconds(displayCd)) : "—";
            if (detailIsVip && def.VipCooldownSeconds > 0 && def.CooldownSeconds > 0) cdFmt += " ★";
            string tlFmt  = def.TimeLimitMinutes > 0 ? FormatTime(TimeSpan.FromMinutes(def.TimeLimitMinutes)) : "—";
            string detailBadge = def.Weekly ? "✦ WEEKLY  ·  " : def.Daily ? "↻ DAILY  ·  " : "";
            string metaLine = $"{detailBadge}{TierNames[tier]}  ·  {def.Category}  ·  {Stars(def.DifficultyStars)}  ·  Repeat  {(def.Repeatable ? "Yes" : "No")}  ·  CD  {cdFmt}  ·  Time  {tlFmt}";
            UILabel(c, R, C_TXT_DM, metaLine, 8, "0 1", "1 1", 12, -66, -122, -44, TextAnchor.MiddleLeft);

            // Action button — top-right of header (112 × 44px)
            if (readyQ != null)
            {
                UIButton(c, R, C_OK, C_BG0, "★  COLLECT", 12,
                    "1 1", "1 1", -120, -58, -8, -14, $"iq.ui collect {def.Id}", true);
            }
            else if (state.Tab == "board")
            {
                string hBtnTxt, hBtnBg, hBtnTxtCol, hBtnCmd;
                string hCd;
                if (IsActive(data, def.Id))
                { hBtnTxt = "ACTIVE";        hBtnBg = C_OK_BG;                          hBtnTxtCol = C_OK;      hBtnCmd = ""; }
                else if (!def.Repeatable && IsCompleted(data, def.Id))
                { hBtnTxt = "COMPLETED";     hBtnBg = C_BTN;                            hBtnTxtCol = C_TXT_DM;  hBtnCmd = ""; }
                else if (IsOnCooldown(data, def.Id, out hCd))
                { hBtnTxt = $"CD  {hCd}";   hBtnBg = C_BTN;                            hBtnTxtCol = C_WRN;     hBtnCmd = ""; }
                else if (TierIndexFromName(def.Tier) > GetTierIndex(data))
                { hBtnTxt = "TIER LOCKED";   hBtnBg = C_ERR_BG;                        hBtnTxtCol = C_ERR;     hBtnCmd = ""; }
                else if (!PrereqsMet(data, def))
                { hBtnTxt = "LOCKED";        hBtnBg = C_BTN;                            hBtnTxtCol = C_TXT_DM;  hBtnCmd = ""; }
                else if (data.ActiveQuests.Count >= TierSlots[GetTierIndex(data)])
                { hBtnTxt = "SLOTS FULL";    hBtnBg = C_BTN;                            hBtnTxtCol = C_WRN;     hBtnCmd = ""; }
                else
                { hBtnTxt = "TAKE";          hBtnBg = UIColor(config.ThemeColor, 0.92f); hBtnTxtCol = C_BG0;   hBtnCmd = $"iq.ui accept {def.Id}"; }

                if (!string.IsNullOrEmpty(hBtnCmd))
                    UIButton(c, R, hBtnBg, hBtnTxtCol, hBtnTxt, 13,
                        "1 1", "1 1", -120, -58, -8, -14, hBtnCmd, true);
                else
                {
                    UIPanel(c, R, "", hBtnBg, "1 1", "1 1", -120, -58, -8, -14);
                    UILabel(c, R, hBtnTxtCol, hBtnTxt, 11,
                        "1 1", "1 1", -120, -58, -8, -14, TextAnchor.MiddleCenter, true);
                }
            }
            else if (state.Tab == "active" && activeQ != null)
            {
                UIPanel(c, R, "", C_OK_BG, "1 1", "1 1", -120, -58, -8, -14);
                UIPanel(c, R, "", C_OK,    "1 1", "1 1", -120, -58, -117, -14);
                UILabel(c, R, C_OK, "IN PROGRESS", 10,
                    "1 1", "1 1", -120, -58, -8, -14, TextAnchor.MiddleCenter, true);
                if (!string.IsNullOrEmpty(activeQ.ExpiresAt))
                {
                    TimeSpan left = DateTime.Parse(activeQ.ExpiresAt) - DateTime.UtcNow;
                    if (left.TotalSeconds > 0)
                        UILabel(c, R, C_ERR, $"Expires {FormatTime(left)}", 8,
                            "1 1", "1 1", -120, -72, -8, -58, TextAnchor.MiddleCenter);
                }
            }

            // Description
            UILabel(c, R, C_TXT_MD, def.Description, 10, "0 1", "1 1", 12, -136, -12, -70, TextAnchor.UpperLeft);

            // Objectives section header
            UILabel(c, R, UIColor(config.ThemeColor, 0.88f), "OBJECTIVES", 9, "0 1", "1 1", 12, -154, 100, -136, TextAnchor.MiddleLeft, true);
            UIGlowLine(c, R, "0 1", "1 1", 12, -157, -12, -156);

            // Objective cards — icon card (same as rewards) + description label + bar below
            const float OBJ_W      = 88f;   // card width  — matches reward boxW
            const float OBJ_CARD_H = 80f;   // card height — matches reward boxH
            const float OBJ_BAR_H  = 5f;    // progress bar below card
            const float OBJ_LBL_H  = 16f;   // description label below bar
            const float OBJ_GAP    = 10f;
            const float OBJ_ROW_H  = OBJ_CARD_H + OBJ_BAR_H + OBJ_LBL_H + OBJ_GAP; // 111px per row of cards
            float ocx = 12f, ocRowY = -166f;

            for (int i = 0; i < def.Objectives.Count; i++)
            {
                var obj = def.Objectives[i];
                int cur = readyQ != null ? obj.Count :
                          (activeQ != null && i < activeQ.Progress.Count) ? activeQ.Progress[i] : 0;
                float pct  = obj.Count > 0 ? Mathf.Clamp01((float)cur / obj.Count) : 1f;
                bool  done = cur >= obj.Count;

                // Wrap to next row if out of space
                if (ocx + OBJ_W > 620f) { ocx = 12f; ocRowY -= OBJ_ROW_H; }

                // ── Card (icon + count, identical structure to reward cards) ──────
                string accentCol = done ? C_OK : (pct > 0f ? UIColor(config.ThemeColor, 1f) : C_DIV);
                UIPanel(c, R, "", C_BG4,    "0 1", "0 1", ocx,     ocRowY - OBJ_CARD_H, ocx + OBJ_W, ocRowY);
                UIPanel(c, R, "", accentCol, "0 1", "0 1", ocx,     ocRowY - 2f,         ocx + OBJ_W, ocRowY);
                UIPanel(c, R, "", accentCol, "0 1", "0 1", ocx,     ocRowY - 2f,         ocx + OBJ_W, ocRowY - 1f);

                // Icon — same proportions as reward item icon
                string objType = obj.Type?.ToLower() ?? "";
                if (objType == "kill")
                {
                    string killImg = GetKillObjectiveIcon(obj);
                    if (killImg != null)
                        UIImage(c, R, killImg, "0 1", "0 1",
                            ocx + 6, ocRowY - OBJ_CARD_H + 16, ocx + OBJ_W - 6, ocRowY - 16);
                }
                else
                {
                    int iconItemId = GetObjectiveIconItemId(obj);
                    if (iconItemId != 0)
                        UIItemIcon(c, R, iconItemId, "0 1", "0 1",
                            ocx + 6, ocRowY - OBJ_CARD_H + 16, ocx + OBJ_W - 6, ocRowY - 16);
                }

                // Count label at bottom of card (same position as reward qty label)
                string cntTxt = done ? "✓ Done" : $"{cur} / {obj.Count}";
                string cntCol = done ? C_OK : C_TXT_MD;
                UILabel(c, R, cntCol, cntTxt, 9,
                    "0 1", "0 1", ocx, ocRowY - OBJ_CARD_H, ocx + OBJ_W, ocRowY - OBJ_CARD_H + 16,
                    TextAnchor.MiddleCenter, done);

                // ── Progress bar below card ───────────────────────────────────────
                float barY = ocRowY - OBJ_CARD_H - 2f;
                string obBarId = $"IQ_OB{i}";
                UIPanel(c, R, obBarId, C_BTN, "0 1", "0 1", ocx, barY - OBJ_BAR_H, ocx + OBJ_W, barY);
                if (pct > 0f)
                    UIPanel(c, R, "", done ? C_OK : UIColor(config.ThemeColor, 0.90f),
                        "0 1", "0 1", ocx, barY - OBJ_BAR_H, ocx + (OBJ_W * pct), barY);

                // ── Description label below bar ───────────────────────────────────
                float lblY = barY - OBJ_BAR_H - 2f;
                string shortLabel = string.IsNullOrEmpty(obj.Description)
                    ? $"{ObjectiveTypeDisplay(obj.Type)} {obj.Target}"
                    : obj.Description;
                UILabel(c, R, done ? C_OK : C_TXT_DM, shortLabel, 8,
                    "0 1", "0 1", ocx, lblY - OBJ_LBL_H, ocx + OBJ_W, lblY,
                    TextAnchor.MiddleCenter);

                ocx += OBJ_W + OBJ_GAP;
            }
            float objY = ocRowY - OBJ_ROW_H; // bottom of last objective row for reward section positioning

            // Rewards section — icon grid
            float ry = objY - 10f;
            UILabel(c, R, UIColor(config.ThemeColor, 0.88f), "REWARDS", 9, "0 1", "1 1", 12, ry - 18, 80, ry, TextAnchor.MiddleLeft, true);
            UIGlowLine(c, R, "0 1", "1 1", 12, ry - 21, -12, ry - 20);

            string S_CARD  = UIColor("#38393F", 1f);
            string S_BLUE  = UIColor("#71B8ED", 1f);
            string S_BLUE2 = UIColor("#71B8ED", 0.18f);
            string S_TEXT  = UIColor("#E2DBD3", 1f);
            string S_XP    = UIColor("#F0C040", 1f);
            string S_ECO   = UIColor("#5BC85B", 1f);
            string S_RP    = UIColor("#C890F0", 1f);

            float boxW = 88f, boxH = 80f, boxGap = 10f;
            float rx = 12f, boxRowY = ry - 25f;
            const int REWARD_CARD_MAX = 5;
            bool stdCapped = def.Rewards.Count > REWARD_CARD_MAX;
            int  stdVisible = stdCapped ? REWARD_CARD_MAX - 1 : def.Rewards.Count;
            for (int i = 0; i < stdVisible; i++)
            {
                var rw = def.Rewards[i];
                bool isItem = rw.Type.ToLower() == "item" || rw.Type.ToLower() == "blueprint";
                bool isBp   = rw.Type.ToLower() == "blueprint";

                if (rx + boxW > 600f) { rx = 12f; boxRowY -= boxH + 8f; }

                // Card bg + accent top border
                UIPanel(c, R, "", S_CARD,  "0 1", "0 1", rx,       boxRowY - boxH, rx + boxW, boxRowY);
                UIPanel(c, R, "", S_BLUE2, "0 1", "0 1", rx,       boxRowY - 2f,   rx + boxW, boxRowY);
                UIPanel(c, R, "", S_BLUE,  "0 1", "0 1", rx,       boxRowY - 2f,   rx + boxW, boxRowY - 1f);

                if (isItem)
                {
                    var itemDef = ItemManager.FindItemDefinition(rw.Shortname);
                    if (itemDef != null)
                    {
                        string skinPng = rw.SkinId != 0 ? GetImage(rw.Shortname, rw.SkinId) : null;
                        if (!string.IsNullOrEmpty(skinPng))
                            UIImage(c, R, skinPng, "0 1", "0 1", rx + 6, boxRowY - boxH + 16, rx + boxW - 6, boxRowY - 16);
                        else
                            UIItemIcon(c, R, itemDef.itemid, "0 1", "0 1",
                                rx + 6, boxRowY - boxH + 16, rx + boxW - 6, boxRowY - 16, rw.SkinId);
                        if (isBp)
                            UILabel(c, R, S_BLUE, "BP", 7, "0 1", "0 1",
                                rx + 2, boxRowY - 14, rx + boxW - 2, boxRowY - 4, TextAnchor.MiddleRight);
                    }
                    // Qty label for item rewards
                    string qtyStr = string.IsNullOrEmpty(rw.CustomName) ? $"x{rw.Amount}" : rw.CustomName;
                    UILabel(c, R, S_TEXT, qtyStr, 9, "0 1", "0 1",
                        rx, boxRowY - boxH, rx + boxW, boxRowY - boxH + 16, TextAnchor.MiddleCenter);
                }
                else
                {
                    string typeCol, typeLabel, amtLabel;
                    switch (rw.Type.ToLower())
                    {
                        case "tier_xp":
                            typeCol = S_XP;   typeLabel = "XP";  amtLabel = $"+{rw.Amount} XP";  break;
                        case "reputation":
                            typeCol = S_RP;   typeLabel = "REP"; amtLabel = $"+{rw.Amount} Rep";  break;
                        case "economics":
                            typeCol = S_ECO;  typeLabel = "$";   amtLabel = $"${rw.Amount}";       break;
                        case "server_rewards":
                            typeCol = S_RP;   typeLabel = "RP";  amtLabel = $"{rw.Amount} RP";    break;
                        case "skill_xp":
                            typeCol = S_XP;   typeLabel = "SKL"; amtLabel = $"+{rw.Amount} XP";  break;
                        case "command":
                            typeCol = S_TEXT; typeLabel = "CMD"; amtLabel = "Custom";             break;
                        default:
                            typeCol = S_TEXT;
                            typeLabel = rw.Type.Length > 4 ? rw.Type.ToUpper().Substring(0, 4) : rw.Type.ToUpper();
                            amtLabel  = $"x{rw.Amount}";
                            break;
                    }
                    UILabel(c, R, typeCol, typeLabel, 22, "0 1", "0 1",
                        rx, boxRowY - boxH + 16, rx + boxW, boxRowY - 18, TextAnchor.MiddleCenter, true);
                    UILabel(c, R, S_TEXT, amtLabel, 9, "0 1", "0 1",
                        rx, boxRowY - boxH, rx + boxW, boxRowY - boxH + 16, TextAnchor.MiddleCenter);
                }

                rx += boxW + boxGap;
            }
            if (stdCapped)
            {
                if (rx + boxW > 600f) { rx = 12f; boxRowY -= boxH + 8f; }
                int moreN = def.Rewards.Count - stdVisible;
                UIButton(c, R, S_CARD, S_BLUE, $"+{moreN} more", 10,
                    "0 1", "0 1", rx, boxRowY - boxH, rx + boxW, boxRowY,
                    $"iq.ui rewardlist {def.Id}", true);
                rx += boxW + boxGap;
            }
            float chipY = boxRowY - boxH - 6f;

            // VIP Rewards section
            if (def.VipRewards != null && def.VipRewards.Count > 0)
            {
                bool isVip = permission.UserHasPermission(player.UserIDString, PERM_VIP);
                float vy = chipY - 8f;
                string S_VIP_HDR  = "0.980 0.820 0.200 1";  // gold text
                string S_VIP_LINE = "0.340 0.280 0.020 0.70";

                // Section header
                UIPanel(c, R, "", S_VIP_LINE, "0 1", "1 1", 12, vy - 1, -12, vy);
                UILabel(c, R, S_VIP_HDR, "★  VIP EXCLUSIVE REWARDS", 9, "0 1", "1 1", 12, vy - 18, 220, vy, TextAnchor.MiddleLeft, true);

                if (!isVip)
                {
                    // Locked banner for non-VIP players
                    UIPanel(c, R, "", "0.14 0.10 0.02 0.88", "0 1", "1 1", 12, vy - 50, -12, vy - 20);
                    UILabel(c, R, S_VIP_HDR, "⊘  VIP MEMBERS RECEIVE BONUS REWARDS  ⊘", 9,
                        "0 1", "1 1", 12, vy - 50, -12, vy - 20, TextAnchor.MiddleCenter, true);
                    chipY = vy - 54f;
                }
                else
                {
                    // Show VIP reward cards with gold accent
                    float vx = 12f;
                    float vboxRowY = vy - 25f;
                    bool vipCapped  = def.VipRewards.Count > REWARD_CARD_MAX;
                    int  vipVisible = vipCapped ? REWARD_CARD_MAX - 1 : def.VipRewards.Count;
                    for (int vi = 0; vi < vipVisible; vi++)
                    {
                        var vr = def.VipRewards[vi];
                        bool vIsItem = vr.Type.ToLower() == "item" || vr.Type.ToLower() == "blueprint";
                        bool vIsBp   = vr.Type.ToLower() == "blueprint";
                        if (vx + boxW > 600f) { vx = 12f; vboxRowY -= boxH + 8f; }

                        // Gold-tinted card
                        UIPanel(c, R, "", "0.26 0.22 0.04 1",  "0 1", "0 1", vx, vboxRowY - boxH, vx + boxW, vboxRowY);
                        UIPanel(c, R, "", "0.60 0.46 0.04 0.20","0 1", "0 1", vx, vboxRowY - 2f,   vx + boxW, vboxRowY);
                        UIPanel(c, R, "", S_VIP_HDR,             "0 1", "0 1", vx, vboxRowY - 2f,   vx + boxW, vboxRowY - 1f);

                        if (vIsItem)
                        {
                            var vItemDef = ItemManager.FindItemDefinition(vr.Shortname);
                            if (vItemDef != null)
                            {
                                string vPng = vr.SkinId != 0 ? GetImage(vr.Shortname, vr.SkinId) : null;
                                if (!string.IsNullOrEmpty(vPng))
                                    UIImage(c, R, vPng, "0 1", "0 1", vx + 6, vboxRowY - boxH + 16, vx + boxW - 6, vboxRowY - 16);
                                else
                                    UIItemIcon(c, R, vItemDef.itemid, "0 1", "0 1",
                                        vx + 6, vboxRowY - boxH + 16, vx + boxW - 6, vboxRowY - 16, vr.SkinId);
                                if (vIsBp)
                                    UILabel(c, R, S_VIP_HDR, "BP", 7, "0 1", "0 1",
                                        vx + 2, vboxRowY - 14, vx + boxW - 2, vboxRowY - 4, TextAnchor.MiddleRight);
                            }
                            string vQtyStr = string.IsNullOrEmpty(vr.CustomName) ? $"x{vr.Amount}" : vr.CustomName;
                            UILabel(c, R, S_VIP_HDR, vQtyStr, 9, "0 1", "0 1",
                                vx, vboxRowY - boxH, vx + boxW, vboxRowY - boxH + 16, TextAnchor.MiddleCenter);
                        }
                        else
                        {
                            string vTypeLabel, vAmtLabel;
                            switch (vr.Type.ToLower())
                            {
                                case "tier_xp":        vTypeLabel = "XP";  vAmtLabel = $"+{vr.Amount} XP";  break;
                                case "reputation":     vTypeLabel = "REP"; vAmtLabel = $"+{vr.Amount} Rep"; break;
                                case "economics":      vTypeLabel = "$";   vAmtLabel = $"${vr.Amount}";     break;
                                case "server_rewards": vTypeLabel = "RP";  vAmtLabel = $"{vr.Amount} RP";   break;
                                case "skill_xp":       vTypeLabel = "SKL"; vAmtLabel = $"+{vr.Amount} XP"; break;
                                case "command":        vTypeLabel = "CMD"; vAmtLabel = "Custom";            break;
                                default:
                                    vTypeLabel = vr.Type.Length > 4 ? vr.Type.ToUpper().Substring(0, 4) : vr.Type.ToUpper();
                                    vAmtLabel  = $"x{vr.Amount}"; break;
                            }
                            UILabel(c, R, S_VIP_HDR, vTypeLabel, 22, "0 1", "0 1",
                                vx, vboxRowY - boxH + 16, vx + boxW, vboxRowY - 18, TextAnchor.MiddleCenter, true);
                            UILabel(c, R, S_VIP_HDR, vAmtLabel, 9, "0 1", "0 1",
                                vx, vboxRowY - boxH, vx + boxW, vboxRowY - boxH + 16, TextAnchor.MiddleCenter);
                        }
                        vx += boxW + boxGap;
                    }
                    if (vipCapped)
                    {
                        if (vx + boxW > 600f) { vx = 12f; vboxRowY -= boxH + 8f; }
                        int vMoreN = def.VipRewards.Count - vipVisible;
                        UIButton(c, R, "0.26 0.22 0.04 1", S_VIP_HDR, $"+{vMoreN} more", 10,
                            "0 1", "0 1", vx, vboxRowY - boxH, vx + boxW, vboxRowY,
                            $"iq.ui rewardlist {def.Id}", true);
                        vx += boxW + boxGap;
                    }
                    chipY = vboxRowY - boxH - 6f;
                }
            }

            // Prereqs
            if (def.RequiredIds != null && def.RequiredIds.Count > 0)
            {
                float py = chipY - 38f;
                UILabel(c, R, C_TXT_DM, "Requires:", 8, "0 1", "0 1", 12, py - 18, 78, py, TextAnchor.MiddleLeft);
                float px = 80f;
                foreach (var rid in def.RequiredIds)
                {
                    var req = GetQuest(rid);
                    bool met = IsCompleted(data, rid);
                    string rl = req?.Title ?? rid;
                    float rw = rl.Length * 6.5f + 12f;
                    UIPanel(c, R, "", met ? C_OK_BG : C_ERR_BG, "0 1", "0 1", px, py - 18, px + rw, py);
                    UILabel(c, R, met ? C_OK : C_ERR, rl, 8,
                        "0 1", "0 1", px + 4, py - 18, px + rw - 4, py, TextAnchor.MiddleCenter);
                    px += rw + 4f;
                }
            }

            // Chain timeline
            if (!string.IsNullOrEmpty(def.ChainId))
            {
                var chain = new List<QuestDefinition>();
                foreach (var q in _quests)
                    if (q.ChainId == def.ChainId) chain.Add(q);
                chain.Sort((a, b) => a.ChainOrder.CompareTo(b.ChainOrder));

                if (chain.Count > 1)
                {
                    float cy = chipY - (def.RequiredIds != null && def.RequiredIds.Count > 0 ? 42f : 12f);
                    string chainLabel = !string.IsNullOrEmpty(def.ChainTitle) ? def.ChainTitle : $"Chain: {def.ChainId}";
                    UILabel(c, R, UIColor(config.ThemeColor, 0.82f), chainLabel.ToUpper(), 9, "0 1", "1 1", 12, cy - 18, 300, cy, TextAnchor.MiddleLeft, true);
                    UIGlowLine(c, R, "0 1", "1 1", 12, cy - 21, -12, cy - 20);

                    float dotX = 12f;
                    float dotY = cy - 22f;
                    float dotR = cy - 38f;
                    bool chainDone = data.CompletedChains.Contains(def.ChainId);

                    for (int ci = 0; ci < chain.Count; ci++)
                    {
                        var cq = chain[ci];
                        bool cDone = IsCompleted(data, cq.Id);
                        bool cCur  = cq.Id == def.Id;
                        bool isLast = ci == chain.Count - 1;

                        // Dot
                        string dotCol = cDone ? C_OK : cCur ? UIColor(config.ThemeColor, 1f) : C_BTN_HI;
                        UIPanel(c, R, "", dotCol, "0 1", "0 1", dotX + 2, dotY - 2, dotX + 12, dotR + 2);

                        // Label below dot
                        string cLabel = cq.Title.Length > 12 ? cq.Title.Substring(0, 10) + ".." : cq.Title;
                        UILabel(c, R, cDone ? C_OK : cCur ? C_TXT_HI : C_TXT_DM, cLabel, 7,
                            "0 1", "0 1", dotX - 4, dotR - 12, dotX + 58, dotR, TextAnchor.MiddleCenter);

                        dotX += 64f;

                        // Connector line to next
                        if (!isLast)
                            UIPanel(c, R, "", cDone ? C_OK : C_DIV, "0 1", "0 1", dotX - 52, dotY - 7, dotX - 2, dotY - 9);
                    }
                }
            }

            // Bottom actions
            if (readyQ != null)
            {
                // COLLECT moved to header; just show abandon link
                UIButton(c, R, "0 0 0 0", C_TXT_DM, "abandon", 8,
                    "0 0", "1 0", 12, 8, -12, 24, $"iq.ui abandon {def.Id}");
                return;
            }

            if (state.Tab == "active" && activeQ != null)
            {
                UIButton(c, R, C_ERR_BG, C_ERR, "ABANDON CONTRACT", 12,
                    "0 0", "1 0", 12, 8, -12, 48, $"iq.ui abandon {def.Id}", true);
                return;
            }

            if (state.Tab == "archives")
            {
                var rec = GetCompletedRecord(data, def.Id);
                string cdRemaining = "";
                bool onCd = def.Repeatable && def.CooldownSeconds > 0 && IsOnCooldown(data, def.Id, out cdRemaining);

                UIPanel(c, R, "", C_BG2, "0 0", "1 0", 12, 8, -12, 48);
                if (onCd)
                {
                    UILabel(c, R, C_WRN, $"AVAILABLE IN  {cdRemaining}", 11, "0 0", "1 0", 12, 8, -12, 48, TextAnchor.MiddleCenter, true);
                }
                else if (def.Repeatable)
                {
                    string timesStr = rec != null ? $"  ·  {rec.Times}×" : "";
                    UILabel(c, R, C_OK, $"AVAILABLE NOW{timesStr}", 11, "0 0", "1 0", 12, 8, -12, 48, TextAnchor.MiddleCenter, true);
                }
                else
                {
                    string dateStr = rec != null ? $"  {DateTime.Parse(rec.At).ToLocalTime():yyyy-MM-dd}" : "";
                    UILabel(c, R, C_TXT_DM, $"COMPLETED{dateStr}", 11, "0 0", "1 0", 12, 8, -12, 48, TextAnchor.MiddleCenter, true);
                }
            }
            // Board: TAKE button is in the header — no bottom button needed
        }

        private ActiveQuest GetActiveQuest(PlayerData data, string id)
        {
            for (int i = 0; i < data.ActiveQuests.Count; i++)
                if (data.ActiveQuests[i].Id == id) return data.ActiveQuests[i];
            return null;
        }

        private CompletedRecord GetCompletedRecord(PlayerData data, string id)
        {
            for (int i = 0; i < data.Completed.Count; i++)
                if (data.Completed[i].Id == id) return data.Completed[i];
            return null;
        }

        private void DrawRankProfile(CuiElementContainer c, string R, PlayerData pd, bool isMe)
        {
            int tier = GetTierIndex(pd);

            // Profile header
            UIPanel(c, R, "", TierBg[tier], "0 1", "1 1", 0, -66, 0, 0);
            UIPanel(c, R, "", TierColors[tier], "0 1", "0 1", 0, -66, 3, 0);
            UIPanel(c, R, "", C_DIV, "0 1", "1 1", 0, -67, 0, -66);
            UILabel(c, R, isMe ? C_OK : C_TXT_HI, pd.DisplayName, 15, "0 1", "0.78 1", 12, -44, 0, -2, TextAnchor.MiddleLeft, true);
            UILabel(c, R, TierColors[tier], TierNames[tier], 9, "0 1", "0.4 1", 12, -66, 0, -46, TextAnchor.MiddleLeft);
            if (isMe) UILabel(c, R, C_OK, "YOU", 9, "0.4 1", "0.7 1", 0, -66, 0, -46, TextAnchor.MiddleLeft, true);

            // Stats grid
            float sy = -86f;
            DrawStatBlock(c, R, "COMPLETIONS", pd.TotalCompletions().ToString(), 12, sy);
            DrawStatBlock(c, R, "REPUTATION",  pd.Reputation.ToString("N0"),     148, sy);
            DrawStatBlock(c, R, "STREAK",      $"{pd.Streak}d",                  284, sy);
            DrawStatBlock(c, R, "ACTIVE",      pd.ActiveQuests.Count.ToString(), 420, sy);

            // Tier progress bar — driven by TierXP
            float ry2 = sy - 56f;
            UILabel(c, R, UIColor(config.ThemeColor, 0.82f), "TIER PROGRESS", 9, "0 1", "1 1", 12, ry2 - 18, 130, ry2, TextAnchor.MiddleLeft, true);
            int totalXp    = pd.TierXP;
            int nextThresh = tier < TierThresholds.Length - 1 ? TierThresholds[tier + 1] : TierThresholds[tier];
            int prevThresh = TierThresholds[tier];
            float tierPct  = tier >= TierThresholds.Length - 1 ? 1f :
                nextThresh > prevThresh ? Mathf.Clamp01((float)(totalXp - prevThresh) / (nextThresh - prevThresh)) : 1f;

            string barProg = "IQ_TP";
            UIPanel(c, R, barProg, C_BTN, "0 1", "1 1", 12, ry2 - 36, -12, ry2 - 20);
            if (tierPct > 0f)
                UIPanel(c, barProg, "", TierColors[tier], "0 0", $"{tierPct:F3} 1", 0, 0, 0, 0);
            string nextTierName = tier < TierNames.Length - 1 ? TierNames[tier + 1] : "MAX";
            UILabel(c, R, C_TXT_DM, tier >= TierThresholds.Length - 1
                ? "MAX TIER REACHED"
                : $"{totalXp} / {nextThresh} XP  →  {nextTierName}",
                8, "0 1", "1 1", 12, ry2 - 50, -12, ry2 - 36, TextAnchor.MiddleCenter);

            // Recent completions
            float recY = ry2 - 64f;
            UILabel(c, R, UIColor(config.ThemeColor, 0.82f), "RECENT CONTRACTS", 9, "0 1", "1 1", 12, recY - 18, 170, recY, TextAnchor.MiddleLeft, true);
            UIGlowLine(c, R, "0 1", "1 1", 12, recY - 21, -12, recY - 20);
            int recCount = Math.Min(pd.Completed.Count, 5);
            for (int i = 0; i < recCount; i++)
            {
                var rec = pd.Completed[pd.Completed.Count - 1 - i];
                var def = GetQuest(rec.Id);
                float rt = recY - 24f - i * 22f;
                string qTitle = def?.Title ?? rec.Id;
                int qt = def != null ? TierIndexFromName(def.Tier) : 0;
                UIPanel(c, R, "", TierColors[qt], "0 1", "0 1", 12, rt - 16, 14, rt);
                UILabel(c, R, C_TXT_MD, qTitle, 9, "0 1", "1 1", 18, rt - 18, -90, rt, TextAnchor.MiddleLeft);
                UILabel(c, R, C_TXT_DM, DateTime.Parse(rec.At).ToLocalTime().ToString("MM-dd"), 8,
                    "1 1", "1 1", -88, rt - 18, -8, rt, TextAnchor.MiddleRight);
            }
        }

        private void DrawStatBlock(CuiElementContainer c, string parent, string label, string value, float x, float y)
        {
            float w = 128f;
            UIPanel(c, parent, "", C_BG3, "0 1", "0 1", x, y - 44, x + w, y);
            UIPanel(c, parent, "", C_DIV, "0 1", "0 1", x, y - 44, x + w, y - 42);
            UILabel(c, parent, C_TXT_HI, value, 18, "0 1", "0 1", x + 4, y - 32, x + w - 4, y - 2, TextAnchor.MiddleCenter, true);
            UILabel(c, parent, C_TXT_DM, label, 8, "0 1", "0 1", x + 4, y - 44, x + w - 4, y - 32, TextAnchor.MiddleCenter);
        }

        // ─────────────────────── Reward List Overlay ─────────────────────────
        private const string UI_REWARDLIST = "IQ_RewardList";

        private void DrawRewardList(BasePlayer player, QuestDefinition def)
        {
            bool isVip = permission.UserHasPermission(player.UserIDString, PERM_VIP);

            var rows = new List<(RewardDef r, bool vip)>();
            foreach (var r in def.Rewards) rows.Add((r, false));
            if (isVip && def.VipRewards != null)
                foreach (var r in def.VipRewards) rows.Add((r, true));

            CuiHelper.DestroyUi(player, UI_REWARDLIST);
            var c = new CuiElementContainer();

            string S_VIP_GOLD = "0.980 0.820 0.200 1";

            c.Add(new CuiElement
            {
                Name = UI_REWARDLIST, Parent = "IQ_P",
                Components = {
                    new CuiImageComponent { Color = "0.07 0.07 0.09 0.97" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "265 34", OffsetMax = "0 -56" }
                }
            });

            // Header bar
            UIPanel(c, UI_REWARDLIST, "", C_BG2, "0 1", "1 1", 0, -36, 0, 0);
            UILabel(c, UI_REWARDLIST, C_TXT_HI, "ALL REWARDS", 10, "0 1", "1 1", 10, -34, -44, -2, TextAnchor.MiddleLeft, true);
            UIButton(c, UI_REWARDLIST, C_ERR_BG, C_ERR, "✕", 11, "1 1", "1 1", -40, -34, -4, -4, "iq.ui closerewardlist", true);

            // Reward rows
            float rowY = -40f;
            for (int i = 0; i < rows.Count; i++)
            {
                var (rw, isVipRow) = rows[i];

                // Alternating row bg
                if (i % 2 == 0)
                    UIPanel(c, UI_REWARDLIST, "", "0.12 0.12 0.14 1", "0 1", "1 1", 4, rowY - 24, -4, rowY);

                float iconX = 6f;

                // VIP star marker on left edge
                if (isVipRow)
                {
                    UIPanel(c, UI_REWARDLIST, "", "0.34 0.28 0.02 0.90", "0 1", "0 1", 4, rowY - 24, 26, rowY);
                    UILabel(c, UI_REWARDLIST, S_VIP_GOLD, "★", 8, "0 1", "0 1", 4, rowY - 24, 26, rowY, TextAnchor.MiddleCenter, true);
                    iconX = 28f;
                }

                // Item icon
                bool isItemRow = rw.Type.ToLower() == "item" || rw.Type.ToLower() == "blueprint";
                if (isItemRow)
                {
                    var itemDef = ItemManager.FindItemDefinition(rw.Shortname);
                    if (itemDef != null)
                    {
                        string listPng = rw.SkinId != 0 ? GetImage(rw.Shortname, rw.SkinId) : null;
                        if (!string.IsNullOrEmpty(listPng))
                            UIImage(c, UI_REWARDLIST, listPng, "0 1", "0 1", iconX, rowY - 22, iconX + 20, rowY - 2);
                        else
                            UIItemIcon(c, UI_REWARDLIST, itemDef.itemid, "0 1", "0 1", iconX, rowY - 22, iconX + 20, rowY - 2, rw.SkinId);
                    }
                }

                // Label
                string lbl = GetRewardRowLabel(rw);
                string lblCol = isVipRow ? S_VIP_GOLD : C_TXT_MD;
                UILabel(c, UI_REWARDLIST, lblCol, lbl, 9, "0 1", "1 1", iconX + 24, rowY - 24, -8, rowY, TextAnchor.MiddleLeft);

                rowY -= 26f;
            }

            CuiHelper.AddUi(player, c);
        }

        private string GetRewardRowLabel(RewardDef rw)
        {
            switch (rw.Type.ToLower())
            {
                case "item":
                case "blueprint":
                {
                    var itemDef = ItemManager.FindItemDefinition(rw.Shortname);
                    string name = itemDef != null ? itemDef.displayName.english : rw.Shortname;
                    string bpSuffix = rw.Type.ToLower() == "blueprint" ? " (BP)" : "";
                    string qty = !string.IsNullOrEmpty(rw.CustomName) ? rw.CustomName : $"×{rw.Amount}";
                    return $"{name}{bpSuffix}  {qty}";
                }
                case "tier_xp":       return $"+{rw.Amount} Tier XP";
                case "reputation":    return $"+{rw.Amount} Reputation";
                case "economics":     return $"${rw.Amount} Economics";
                case "server_rewards":return $"{rw.Amount} RP";
                case "skill_xp":      return $"+{rw.Amount} Skill XP";
                case "command":       return "Custom Reward";
                default:              return $"{rw.Type}  ×{rw.Amount}";
            }
        }

        // ─────────────────────── Delivery UI ─────────────────────────────────
        private const string UI_DELIVERY = "IQ_Delivery";

        private void OpenDeliveryUI(BasePlayer player, ScientistNPC atNpc = null)
        {
            CuiHelper.DestroyUi(player, UI_DELIVERY);
            var data = GetOrCreate(player);
            ulong npcNetId = atNpc?.net?.ID.Value ?? 0;

            // Collect deliver quests assigned to THIS specific NPC (or unassigned ones)
            var deliverQuests = new List<(QuestDefinition def, ActiveQuest aq)>();
            foreach (var aq in data.ActiveQuests)
            {
                var def = _quests.Find(q => q.Id == aq.Id);
                if (def == null) continue;
                bool hasDeliver = false;
                foreach (var o in def.Objectives) if (o.Type?.ToLower() == "deliver") { hasDeliver = true; break; }
                if (!hasDeliver) continue;
                // If a specific NPC was assigned, only show at that NPC
                if (aq.DeliveryNpcId != 0 && npcNetId != 0 && aq.DeliveryNpcId != npcNetId) continue;
                deliverQuests.Add((def, aq));
            }

            var c = new CuiElementContainer();
            // Backdrop — needs cursor so player can click buttons
            c.Add(new CuiElement
            {
                Name = UI_DELIVERY, Parent = "Overlay",
                Components = {
                    new CuiImageComponent { Color = C_BG0 },
                    new CuiRectTransformComponent { AnchorMin = "0.3 0.25", AnchorMax = "0.7 0.75" },
                    new CuiNeedsCursorComponent()
                }
            });
            // Header
            UIPanel(c, UI_DELIVERY, "", C_BG2, "0 1", "1 1", 0, -40, 0, 0);
            string delivHdrName = (atNpc?.displayName ?? config.ContractorNpcs.DisplayName).ToUpper();
            UILabel(c, UI_DELIVERY, UIColor(config.ThemeColor, 1f), delivHdrName, 13, "0 1", "0.6 1", 10, -38, 0, -2, TextAnchor.MiddleLeft, true);
            UILabel(c, UI_DELIVERY, C_TXT_MD, "Delivery drop-off — hand over your goods", 9, "0 1", "1 1", 10, -56, 0, -40, TextAnchor.MiddleLeft);
            UIButton(c, UI_DELIVERY, C_ERR_BG, C_ERR, "✕", 12, "1 1", "1 1", -36, -38, -4, -4, "iq.delivery close", true);

            if (deliverQuests.Count == 0)
            {
                UILabel(c, UI_DELIVERY, C_TXT_DM, "No active delivery contracts.", 11, "0.05 0.3", "0.95 0.7", 0, -10, 0, 10, TextAnchor.MiddleCenter);
            }
            else
            {
                float rowY = -60f;
                foreach (var (def, aq) in deliverQuests)
                {
                    // Find deliver objective(s)
                    for (int oi = 0; oi < def.Objectives.Count; oi++)
                    {
                        var obj = def.Objectives[oi];
                        if (obj.Type?.ToLower() != "deliver") continue;

                        int have   = player.inventory.GetAmount(ItemManager.FindItemDefinition(obj.Target)?.itemid ?? 0);
                        int need   = obj.Count - aq.Progress[oi];
                        bool ready = have >= need;

                        // Row bg
                        UIPanel(c, UI_DELIVERY, "", ready ? C_OK_BG : C_BG4, "0 1", "1 1", 6, rowY - 48, -6, rowY - 2);

                        // Quest title + item info
                        UILabel(c, UI_DELIVERY, C_TXT_HI, def.Title.Length > 28 ? def.Title.Substring(0, 26) + ".." : def.Title,
                            10, "0 1", "0.7 1", 12, rowY - 22, 0, rowY - 4, TextAnchor.MiddleLeft, true);
                        string itemLabel = string.IsNullOrEmpty(obj.Description) ? $"Deliver {need}x {obj.Target}" : obj.Description;
                        UILabel(c, UI_DELIVERY, ready ? C_OK : C_TXT_MD, itemLabel, 9,
                            "0 1", "0.7 1", 12, rowY - 44, 0, rowY - 24, TextAnchor.MiddleLeft);

                        // Have / need
                        UILabel(c, UI_DELIVERY, ready ? C_OK : C_WRN, $"{have}/{need}", 10,
                            "0.7 1", "0.88 1", 0, rowY - 44, 0, rowY - 2, TextAnchor.MiddleCenter, true);

                        // Deliver button
                        string btnBg  = ready ? C_OK_BG  : C_BTN;
                        string btnTxt = ready ? C_OK     : C_TXT_DM;
                        string btnLbl = ready ? "DELIVER" : "MISSING";
                        string btnCmd = ready ? $"iq.delivery confirm {def.Id} {oi}" : "";
                        UIButton(c, UI_DELIVERY, btnBg, btnTxt, btnLbl, 9,
                            "0.88 1", "1 1", 0, rowY - 44, -6, rowY - 4, btnCmd, true);

                        rowY -= 56f;
                    }
                }
            }

            // Footer close
            UIButton(c, UI_DELIVERY, C_BTN, C_TXT_MD, "CLOSE", 9, "0.25 0", "0.75 0", 0, 4, 0, 30, "iq.delivery close", false);
            CuiHelper.AddUi(player, c);
        }

        [ConsoleCommand("iq.delivery")]
        private void ConCmdDelivery(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            string sub = arg.GetString(0).ToLower();

            if (sub == "close") { CuiHelper.DestroyUi(player, UI_DELIVERY); return; }

            if (sub == "confirm" && arg.HasArgs(3))
            {
                string questId = arg.GetString(1);
                int    objIdx  = arg.GetInt(2);
                var data = GetOrCreate(player);
                var aq   = data.ActiveQuests.Find(q => q.Id == questId);
                var def  = _quests.Find(q => q.Id == questId);
                if (aq == null || def == null || objIdx < 0 || objIdx >= def.Objectives.Count) return;

                var obj  = def.Objectives[objIdx];
                if (obj.Type?.ToLower() != "deliver") return;

                int need     = obj.Count - aq.Progress[objIdx];
                var itemDef  = ItemManager.FindItemDefinition(obj.Target);
                if (itemDef == null) { SendReply(player, Msg("DeliveryUnknownItem", player.UserIDString)); return; }

                int have = player.inventory.GetAmount(itemDef.itemid);
                if (have < need) { SendReply(player, Msg("DeliveryNeedItems", player.UserIDString, need, itemDef.displayName.english, have)); return; }

                // Take items from player inventory
                player.inventory.Take(null, itemDef.itemid, need);
                // Award progress
                AwardProgress(player, "deliver", obj.Target, need);
                CuiHelper.DestroyUi(player, UI_DELIVERY);
                SendReply(player, Msg("DeliveryAccepted", player.UserIDString, need, itemDef.displayName.english));
            }
        }

        // ─────────────────────── HUD ───────────────────────────────────────────
        private string ComputeHudHash(PlayerData data)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(data.HudVisible).Append('|').Append(data.HudX).Append(',').Append(data.HudY).Append('|');
            foreach (var aq in data.ReadyToCollect) sb.Append("R:").Append(aq.Id).Append(';');
            foreach (var aq in data.ActiveQuests)
            {
                sb.Append(aq.Id).Append(':');
                if (aq.Progress != null) foreach (var p in aq.Progress) sb.Append(p).Append(',');
                sb.Append(';');
            }
            return sb.ToString();
        }

        private void UpdateHud(BasePlayer player)
        {
            if (!config.HudEnabled) return;
            var data  = GetOrCreate(player);
            bool move = _hudMoveMode.Contains(player.userID);
            if (!data.HudVisible && !move) return;
            int totalHudQuests = data.ReadyToCollect.Count + data.ActiveQuests.Count;
            if (totalHudQuests == 0 && !move) { CuiHelper.DestroyUi(player, UI_HUD); _hudHash.Remove(player.userID); return; }

            // Skip redraw if nothing changed (only in non-move mode)
            if (!move)
            {
                string hash = ComputeHudHash(data);
                if (_hudHash.TryGetValue(player.userID, out var cached) && cached == hash) return;
                _hudHash[player.userID] = hash;
            }

            CuiHelper.DestroyUi(player, UI_HUD);

            int   show    = Math.Min(totalHudQuests, 3);
            float headerH = move ? 62f : 28f;
            // Each quest row: 16px title + 13px objective summary + 4px gap + 8px bar + 4px gap = 45px
            const float ROW_H_HUD = 49f;
            float questsH = show * ROW_H_HUD + (totalHudQuests > 3 ? 16f : 0f);
            float panelH  = headerH + questsH;

            var    c = new CuiElementContainer();
            string H = "IQ_H";

            var root = new CuiElement
            {
                Name = UI_HUD, Parent = "Hud",
                Components = {
                    new CuiImageComponent { Color = "0 0 0 0" },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 1", AnchorMax = "1 1",
                        OffsetMin = $"{-268 + data.HudX} {-8 - panelH + data.HudY}",
                        OffsetMax = $"{data.HudX} {-8 + data.HudY}"
                    }
                }
            };
            if (move) root.Components.Add(new CuiNeedsCursorComponent());
            c.Add(root);

            UIPanel(c, UI_HUD, H, C_BG1, "0 0", "1 1", 0, 0, 0, 0);
            // Top accent line
            UIPanel(c, H, "", UIColor(config.ThemeColor, 1f), "0 1", "1 1", 0, -2, 0, 0);

            if (move)
                DrawHudMoveControls(c, H, player, data);
            else
            {
                // Normal header
                UIPanel(c, H, "", C_BG2, "0 1", "1 1", 0, -28, 0, -2);
                UILabel(c, H, UIColor(config.ThemeColor, 1f), "CONTRACTS", 9, "0 1", "0.50 1", 6, -28, 0, -2, TextAnchor.MiddleLeft, true);
                // Open board shortcut
                UIButton(c, H, "0 0 0 0", C_TXT_DM, "/quest", 8, "0.50 1", "0.72 1", 0, -28, -2, -2, "iq.ui open", false);
                // Move-mode toggle button (⊕)
                UIButton(c, H, C_BTN, C_TXT_DM, "⊕", 10, "0.72 1", "0.87 1", 2, -26, -2, -4, "iq.hud toggle", false);
                // Hide HUD button (×)
                UIButton(c, H, C_BTN, C_ERR, "×", 11, "0.87 1", "1 1", 2, -26, -2, -4, "iq.hud hide", false);
                // Separator
                UIPanel(c, H, "", C_DIV, "0 1", "1 1", 0, -29, 0, -28);
            }

            // Quest rows — ready-to-collect appear first (green), then in-progress
            for (int i = 0; i < show; i++)
            {
                bool isReady = i < data.ReadyToCollect.Count;
                string qId  = isReady
                    ? data.ReadyToCollect[i].Id
                    : data.ActiveQuests[i - data.ReadyToCollect.Count].Id;
                var aq  = isReady ? data.ReadyToCollect[i] : data.ActiveQuests[i - data.ReadyToCollect.Count];
                var def = GetQuest(qId);
                if (def == null) continue;

                float rowTop = -(headerH + i * ROW_H_HUD);
                float rowBot = rowTop - 45f; // 45px content: 16 title + 13 obj + 8 bar + 8 padding

                UIPanel(c, H, "", isReady ? C_OK_BG : (i % 2 == 0 ? C_BG4 : C_BG5), "0 1", "1 1", 0, rowBot, 0, rowTop);

                // Title (top 16px)
                string title = def.Title.Length > 26 ? def.Title.Substring(0, 24) + ".." : def.Title;
                UILabel(c, H, isReady ? C_OK : C_TXT_MD, title, 9, "0 1", "1 1", 6, rowTop - 17, -50, rowTop - 1, TextAnchor.MiddleLeft);

                if (isReady)
                {
                    // Status text (middle)
                    UILabel(c, H, C_OK, "✓ READY TO COLLECT", 7, "0 1", "1 1", 6, rowTop - 32, -6, rowTop - 18, TextAnchor.MiddleLeft, true);
                    // Full-width green bar (bottom 8px)
                    UIPanel(c, H, $"IQ_HB{i}", C_OK, "0 1", "1 1", 0, rowBot + 1, 0, rowBot + 9);
                }
                else
                {
                    // Per-objective summary (middle 13px, starts at rowTop-18, ends at rowTop-31)
                    // Use Target/Type — Description already contains count ("Mine 2000 stone")
                    int   objCount  = def.Objectives.Count;
                    float totalProg = 0f;
                    var objParts = new List<string>();
                    for (int oi = 0; oi < objCount; oi++)
                    {
                        int cur   = oi < aq.Progress.Count ? aq.Progress[oi] : 0;
                        int total = def.Objectives[oi].Count;
                        totalProg += (float)cur / total;
                        bool done = cur >= total;
                        string lbl = !string.IsNullOrEmpty(def.Objectives[oi].Target)
                            ? def.Objectives[oi].Target
                            : def.Objectives[oi].Type;
                        if (lbl.Length > 12) lbl = lbl.Substring(0, 11) + "…";
                        objParts.Add(done ? $"✓ {lbl}" : $"{lbl}  {cur}/{total}");
                        if (oi >= 2 && objCount > 3) { objParts.Add($"+{objCount - 3}"); break; }
                    }
                    string objLine = string.Join("  ·  ", objParts);
                    UILabel(c, H, C_TXT_MD, objLine, 8, "0 1", "1 1", 6, rowTop - 31, -6, rowTop - 18, TextAnchor.MiddleLeft);

                    // Progress bar (bottom 8px, with 4px gap below obj text)
                    float pct    = objCount > 0 ? totalProg / objCount : 1f;
                    string barId = $"IQ_HB{i}";
                    UIPanel(c, H, barId, C_BTN, "0 1", "1 1", 0, rowBot + 1, 0, rowBot + 9);
                    if (pct > 0f)
                        UIPanel(c, barId, "", pct >= 1f ? C_OK : UIColor(config.ThemeColor, 0.90f),
                            "0 0", $"{pct:F3} 1", 0, 0, 0, 0);
                }
            }

            if (totalHudQuests > 3)
                UILabel(c, H, C_TXT_DM, $"... +{totalHudQuests - 3} more", 8,
                    "0 0", "1 0", 6, 2, 0, 16, TextAnchor.MiddleLeft);

            CuiHelper.AddUi(player, c);
        }

        // ─────────────────────── Progress Toast ───────────────────────────────
        // Small non-intrusive CUI notification that appears bottom-centre and
        // auto-dismisses. Replaces itself if another progress event fires before
        // the previous one expires (single-slot per player, no stacking needed).
        private void ShowProgressToast(BasePlayer player, string message, bool completed = false)
        {
            if (player == null || !player.IsConnected) return;
            CuiHelper.DestroyUi(player, UI_TOAST);

            // Cancel any pending dismiss timer
            Timer prev;
            if (_toastTimers.TryGetValue(player.userID, out prev)) prev?.Destroy();

            string accentCol = completed ? C_OK : UIColor(config.ThemeColor, 1f);
            string bgCol     = completed ? C_OK_BG : C_BG2;
            string txtCol    = completed ? C_OK : C_TXT_HI;

            var c = new CuiElementContainer();

            // Anchored to bottom-centre, just above the hotbar
            c.Add(new CuiElement
            {
                Name = UI_TOAST, Parent = "Hud",
                Components = {
                    new CuiImageComponent { Color = bgCol },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                        OffsetMin = "-185 118", OffsetMax = "185 152"
                    }
                }
            });

            // Top highlight line
            UIPanel(c, UI_TOAST, "", accentCol, "0 1", "1 1", 0, -2, 0, 0);
            // Left accent bar
            UIPanel(c, UI_TOAST, "", accentCol, "0 0", "0 1", 0, 2, 3, -2);
            // Message text
            UILabel(c, UI_TOAST, txtCol, message, 11,
                "0 0", "1 1", 8, 0, -6, 0, TextAnchor.MiddleLeft, completed);

            CuiHelper.AddUi(player, c);

            float dur = Mathf.Max(1f, config.ToastDuration);
            _toastTimers[player.userID] = timer.Once(dur, () =>
            {
                CuiHelper.DestroyUi(player, UI_TOAST);
                _toastTimers.Remove(player.userID);
            });
        }

        private void DrawHudMoveControls(CuiElementContainer c, string H, BasePlayer player, PlayerData data)
        {
            int step = _hudMoveStep.TryGetValue(player.userID, out var sv) ? sv : 5;

            // Move mode header bg — dark red tint to visually distinguish
            UIPanel(c, H, "", C_ERR_BG, "0 1", "1 1", 0, -62, 0, -2);
            UILabel(c, H, C_ERR, "MOVE HUD", 8, "0 1", "0 1", 5, -22, 68, -4, TextAnchor.MiddleLeft, true);

            // Arrow cross  row1: [▲]   row2: [◀][▼][▶]
            UIButton(c, H, C_BTN_HI, C_TXT_HI, "▲", 11, "0 1", "0 1", 72, -30, 94,  -8,  $"iq.hud move 0 {step}");
            UIButton(c, H, C_BTN_HI, C_TXT_HI, "◀", 11, "0 1", "0 1", 50, -58, 72,  -34, $"iq.hud move -{step} 0");
            UIButton(c, H, C_BTN_HI, C_TXT_HI, "▼", 11, "0 1", "0 1", 72, -58, 94,  -34, $"iq.hud move 0 -{step}");
            UIButton(c, H, C_BTN_HI, C_TXT_HI, "▶", 11, "0 1", "0 1", 94, -58, 116, -34, $"iq.hud move {step} 0");

            // Position readout
            UILabel(c, H, C_TXT_DM, $"X:{data.HudX}  Y:{data.HudY}", 7,
                "0 1", "0 1", 5, -60, 48, -38, TextAnchor.MiddleLeft);

            // Step size toggle
            UIButton(c, H, step == 5  ? UIColor(config.ThemeColor, 0.9f) : C_BTN,
                step == 5  ? C_BG0 : C_TXT_DM,
                "5",  8, "0 1", "0 1", 118, -46, 136, -28, "iq.hud step 5");
            UIButton(c, H, step == 20 ? UIColor(config.ThemeColor, 0.9f) : C_BTN,
                step == 20 ? C_BG0 : C_TXT_DM,
                "20", 8, "0 1", "0 1", 137, -46, 158, -28, "iq.hud step 20");
            UILabel(c, H, C_TXT_DM, "STEP", 7, "0 1", "0 1", 118, -60, 158, -48, TextAnchor.MiddleCenter);

            // DONE — green confirm
            UIButton(c, H, C_OK_BG, C_OK, "DONE", 9, "1 1", "1 1", -68, -30, -4,  -6,  "iq.hud toggle");
            // RESET — muted
            UIButton(c, H, C_BTN,   C_TXT_DM, "RESET", 7, "1 1", "1 1", -68, -58, -4, -34, "iq.hud reset");
        }

        // ─────────────────────── Commands ─────────────────────────────────────
        private void CmdQuestOpen(IPlayer iPlayer, string command, string[] args)
        {
            var player = iPlayer.Object as BasePlayer;
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                SendReply(player, Msg("NoPermission", player.UserIDString)); return;
            }
            // /quest hud — toggle the mini HUD bar
            if (args != null && args.Length > 0 && args[0].ToLower() == "hud")
            {
                var data = GetOrCreate(player);
                data.HudVisible = !data.HudVisible;
                _dataDirty = true;
                UpdateHud(player);
                SendReply(player, Msg(data.HudVisible ? "HudShown" : "HudHidden", player.UserIDString));
                return;
            }
            // /quest admin — open admin panel (separate UI)
            if (args != null && args.Length > 0 && args[0].ToLower() == "admin")
            {
                if (!iPlayer.IsAdmin && !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
                { SendReply(player, Msg("NoPermission", player.UserIDString)); return; }
                OpenAdminUI(player);
                return;
            }
            // /quest stats — admin completion analytics (same as iq.stats console command)
            if (args != null && args.Length > 0 && args[0].ToLower() == "stats")
            {
                if (!iPlayer.IsAdmin && !permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
                { SendReply(player, Msg("NoPermission", player.UserIDString)); return; }
                var totals  = new Dictionary<string, int>();
                var uniques = new Dictionary<string, HashSet<ulong>>();
                foreach (var kv in _players)
                    foreach (var rec in kv.Value.Completed)
                    {
                        if (!totals.ContainsKey(rec.Id)) { totals[rec.Id] = 0; uniques[rec.Id] = new HashSet<ulong>(); }
                        totals[rec.Id] += rec.Times;
                        uniques[rec.Id].Add(kv.Key);
                    }
                var sb2 = new System.Text.StringBuilder("<color=#E8912B>[IQ Stats]</color> Quest completions (most → least):\n");
                foreach (var q in _quests.OrderByDescending(q => totals.TryGetValue(q.Id, out var n) ? n : 0))
                {
                    int t = totals.TryGetValue(q.Id, out var tt) ? tt : 0;
                    int u = uniques.TryGetValue(q.Id, out var uu) ? uu.Count : 0;
                    sb2.AppendLine($"  <color=#fff>{q.Id}</color>  {t} completions  {u} players  —  {q.Title}");
                }
                SendReply(player, sb2.ToString());
                return;
            }
            // /quest mode <window|fullscreen> — set UI display mode
            if (args != null && args.Length >= 2 && args[0].ToLower() == "mode")
            {
                string newMode = args[1].ToLower();
                if (newMode != "window" && newMode != "fullscreen") newMode = "window";
                var data = GetOrCreate(player);
                data.UiMode = newMode;
                _dataDirty = true;
                if (_ui.TryGetValue(player.userID, out var ms)) ms.Mode = newMode;
                SendReply(player, $"<color=#E8912B>[Quests]</color> UI mode set to <color=#fff>{newMode}</color>.");
                if (_ui.ContainsKey(player.userID)) OpenUI(player);
                return;
            }
            OpenUI(player, "board");
        }

        [ConsoleCommand("iq.ui")]
        private void ConCmdUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            string action = arg.GetString(0);
            switch (action)
            {
                case "open":
                    OpenUI(player, "board");
                    break;
                case "close":
                    CloseUI(player);
                    break;
                case "tab":
                {
                    if (!_ui.TryGetValue(player.userID, out var ts)) return;
                    string tab = arg.GetString(1, "board");
                    ts.Tab = tab; ts.Page = 0; ts.Detail = ""; ts.Search = "";
                    if (tab != "ranks") ts.SelectedPlayer = 0;
                    RefreshPanels(player);
                    break;
                }
                case "page":
                {
                    if (!_ui.TryGetValue(player.userID, out var ps)) return;
                    ps.Page = arg.GetInt(1, 0);
                    RefreshPanels(player);
                    break;
                }
                case "filter":
                {
                    if (!_ui.TryGetValue(player.userID, out var fs)) return;
                    string ftype = arg.GetString(1);
                    string fval  = arg.GetString(2, "");
                    if (ftype == "tier")     { fs.Tier = fval; fs.Page = 0; }
                    else if (ftype == "cat") { fs.Cat  = fval; fs.Page = 0; }
                    RefreshPanels(player);
                    break;
                }
                case "detail":
                {
                    if (!_ui.TryGetValue(player.userID, out var ds)) return;
                    ds.Detail = arg.GetString(1, "");
                    RefreshPanels(player);
                    break;
                }
                case "back":
                {
                    if (!_ui.TryGetValue(player.userID, out var bs)) return;
                    bs.Detail = "";
                    RefreshPanels(player);
                    break;
                }
                case "accept":
                {
                    string qid = arg.GetString(1);
                    bool accepted = AcceptQuest(player, qid);
                    // Only switch to active tab when acceptance succeeded; on failure the player
                    // stays on the board detail view so they can see why they're blocked.
                    if (accepted && _ui.TryGetValue(player.userID, out var aqs))
                    { aqs.Tab = "active"; aqs.Detail = qid; aqs.Page = 0; }
                    OpenUI(player);
                    break;
                }
                case "abandon":
                {
                    string qid = arg.GetString(1);
                    AbandonQuest(player, qid);
                    if (_ui.TryGetValue(player.userID, out var abs)) { abs.Detail = ""; }
                    OpenUI(player, "active");
                    break;
                }
                case "rank":
                {
                    if (!ulong.TryParse(arg.GetString(1, "0"), out ulong rid)) return;
                    if (!_ui.TryGetValue(player.userID, out var rs)) return;
                    rs.SelectedPlayer = rid;
                    rs.Tab = "ranks";
                    RefreshPanels(player);
                    break;
                }
                case "collect":
                {
                    string qid = arg.GetString(1);
                    CollectQuestRewards(player, qid);
                    break;
                }
                case "rewardlist":
                {
                    string qid = arg.GetString(1);
                    var def = GetQuest(qid);
                    if (def != null) DrawRewardList(player, def);
                    break;
                }
                case "closerewardlist":
                {
                    CuiHelper.DestroyUi(player, UI_REWARDLIST);
                    break;
                }
                case "mode":
                {
                    string newMode = arg.GetString(1, "window");
                    if (newMode != "window" && newMode != "fullscreen") newMode = "window";
                    var mdata = GetOrCreate(player);
                    mdata.UiMode = newMode;
                    _dataDirty = true;
                    if (_ui.TryGetValue(player.userID, out var ms)) ms.Mode = newMode;
                    OpenUI(player);
                    break;
                }
                case "search":
                {
                    if (!_ui.TryGetValue(player.userID, out var ss)) return;
                    // Reconstruct full search text (may be multiple words from input field)
                    var parts = new System.Text.StringBuilder();
                    for (int ai = 1; ; ai++)
                    {
                        string part = arg.GetString(ai);
                        if (string.IsNullOrEmpty(part)) break;
                        if (parts.Length > 0) parts.Append(' ');
                        parts.Append(part);
                    }
                    ss.Search = parts.ToString().Trim();
                    ss.Page   = 0;
                    RefreshPanels(player);
                    break;
                }
            }
        }

        [ConsoleCommand("iq.reload")]
        private void ConCmdReload(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !arg.IsAdmin) return;
            LoadQuestDefinitions();
            arg.ReplyWith($"[InfinitumQuests] Reloaded {_quests.Count} quests.");
        }

        [ConsoleCommand("iq.give")]
        private void ConCmdGive(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !arg.IsAdmin) return;
            string sid = arg.GetString(0);
            string qid = arg.GetString(1);
            var player = BasePlayer.Find(sid);
            if (player == null) { arg.ReplyWith("Player not found."); return; }
            AcceptQuest(player, qid);
            arg.ReplyWith($"Assigned quest {qid} to {player.displayName}.");
        }

        // ─────────────────────── Admin UI ─────────────────────────────────────
        private void OpenAdminUI(BasePlayer player)
        {
            if (!_ui.TryGetValue(player.userID, out var state))
            { state = new UiState(); _ui[player.userID] = state; }

            CuiHelper.DestroyUi(player, UI_ADMIN);
            var c = new CuiElementContainer();

            // Backdrop + cursor
            c.Add(new CuiElement
            {
                Name = UI_ADMIN, Parent = "Overlay",
                Components = {
                    new CuiImageComponent { Color = "0 0 0 0.40" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiNeedsCursorComponent()
                }
            });

            // Main panel 960×560
            string P = "IQ_AP";
            c.Add(new CuiElement
            {
                Name = P, Parent = UI_ADMIN,
                Components = {
                    new CuiImageComponent { Color = C_BG1, Material = "assets/content/ui/uibackgroundblur.mat" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-480 -280", OffsetMax = "480 280" }
                }
            });

            // Borders
            string borderCol = UIColor(config.ThemeColor, 0.60f);
            UIPanel(c, P, "", borderCol, "0 1", "1 1", 0, -1, 0, 0);
            UIPanel(c, P, "", borderCol, "0 0", "1 0", 0, 0, 0, 1);
            UIPanel(c, P, "", borderCol, "1 0", "1 1", -1, 0, 0, 0);
            UIPanel(c, P, "", borderCol, "0 0", "0 1", 0, 0, 1, 0);

            // Header 44px
            UIPanel(c, P, "", C_BG2, "0 1", "1 1", 0, -44, 0, 0);
            UILabel(c, P, UIColor(config.ThemeColor, 0.90f), "⚙", 16, "0 1", "0 1", 10, -40, 34, -4, TextAnchor.MiddleCenter, true);
            UILabel(c, P, C_TXT_HI, "QUEST ADMIN PANEL", 13, "0 1", "1 1", 0, -40, 0, -6, TextAnchor.MiddleCenter, true);
            UIButton(c, P, C_ERR_BG, C_ERR, "✕  CLOSE", 9, "1 1", "1 1", -88, -38, -6, -6, "iq.adm close");

            // Tab row 28px
            UIPanel(c, P, "", C_BG3, "0 1", "1 1", 0, -72, 0, -44);
            DrawAdminTab(c, P, "STATS",   "stats",   state.AdminTab, 0f,    0.14f);
            DrawAdminTab(c, P, "PLAYERS", "players", state.AdminTab, 0.14f, 0.30f);
            UIGlowLine(c, P, "0 1", "1 1", 0, -72, 0, -71);

            // Content
            if (state.AdminTab == "players")
                DrawAdminPlayersTab(c, P, player, state);
            else
                DrawAdminStatsTab(c, P, player, state);

            CuiHelper.AddUi(player, c);
        }

        private void DrawAdminTab(CuiElementContainer c, string parent, string label, string tab, string current, float aL, float aR)
        {
            bool active = current == tab;
            string bg  = active ? UIColor(config.ThemeColor, 0.90f) : "0.055 0.053 0.062 0.98";
            string txt = active ? C_BG0 : C_TXT_MD;
            c.Add(new CuiButton
            {
                Button = { Color = bg, Command = $"iq.adm tab {tab}" },
                RectTransform = { AnchorMin = $"{aL:F4} 1", AnchorMax = $"{aR:F4} 1", OffsetMin = "1 -27", OffsetMax = "-1 -1" },
                Text = { Text = label, FontSize = 9, Color = txt, Align = TextAnchor.MiddleCenter,
                    Font = active ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf" }
            }, parent);
            if (active)
                UIPanel(c, parent, "", UIColor(config.ThemeColor, 0.7f), $"{aL:F4} 1", $"{aR:F4} 1", 1, -28, -1, -26);
        }

        private void DrawAdminStatsTab(CuiElementContainer c, string P, BasePlayer admin, UiState state)
        {
            // Build stats data
            var totals  = new Dictionary<string, int>();
            var uniques = new Dictionary<string, HashSet<ulong>>();
            var active  = new Dictionary<string, int>();
            foreach (var kv in _players)
            {
                foreach (var rec in kv.Value.Completed)
                {
                    if (!totals.ContainsKey(rec.Id)) { totals[rec.Id] = 0; uniques[rec.Id] = new HashSet<ulong>(); }
                    totals[rec.Id] += rec.Times;
                    uniques[rec.Id].Add(kv.Key);
                }
                foreach (var aq in kv.Value.ActiveQuests)
                { if (!active.ContainsKey(aq.Id)) active[aq.Id] = 0; active[aq.Id]++; }
            }
            var sorted = _quests.OrderByDescending(q => totals.TryGetValue(q.Id, out var n) ? n : 0).ToList();

            const int ADMIN_ROWS = 14;
            int totalPages = Math.Max(1, (sorted.Count + ADMIN_ROWS - 1) / ADMIN_ROWS);
            int page  = Math.Min(state.AdminPage, totalPages - 1);
            int start = page * ADMIN_ROWS;
            int end   = Math.Min(start + ADMIN_ROWS, sorted.Count);

            // Column header row 24px  (all labels use "0 1","0 1" = absolute px from top-left)
            float hy = -72f;
            UIPanel(c, P, "", C_BG3, "0 1", "1 1", 0, hy - 24, 0, hy);
            UILabel(c, P, C_TXT_DM, "QUEST TITLE",    8, "0 1", "0 1",  12, hy - 22,  380, hy, TextAnchor.MiddleLeft,   true);
            UILabel(c, P, C_TXT_DM, "CATEGORY",       8, "0 1", "0 1", 384, hy - 22,  510, hy, TextAnchor.MiddleLeft,   true);
            UILabel(c, P, C_TXT_DM, "COMPLETIONS",    8, "0 1", "0 1", 514, hy - 22,  660, hy, TextAnchor.MiddleCenter, true);
            UILabel(c, P, C_TXT_DM, "UNIQUE PLAYERS", 8, "0 1", "0 1", 664, hy - 22,  810, hy, TextAnchor.MiddleCenter, true);
            UILabel(c, P, C_TXT_DM, "ACTIVE NOW",     8, "0 1", "0 1", 814, hy - 22,  956, hy, TextAnchor.MiddleCenter, true);

            // Data rows 30px each
            for (int i = start; i < end; i++)
            {
                var q = sorted[i];
                int t = totals.TryGetValue(q.Id, out var tt) ? tt : 0;
                int u = uniques.TryGetValue(q.Id, out var uu) ? uu.Count : 0;
                int a = active.TryGetValue(q.Id, out var aa) ? aa : 0;
                int row = i - start;
                float ry = hy - 24f - row * 30f;
                string rowBg = row % 2 == 0 ? C_BG4 : "0 0 0 0";
                UIPanel(c, P, "", rowBg, "0 1", "1 1", 0, ry - 30, 0, ry);
                UILabel(c, P, C_TXT_HI, q.Title, 9, "0 1", "0 1",  12, ry - 28,  380, ry, TextAnchor.MiddleLeft);
                UILabel(c, P, C_TXT_MD, string.IsNullOrEmpty(q.Category) ? "—" : q.Category, 9,
                    "0 1", "0 1", 384, ry - 28, 510, ry, TextAnchor.MiddleLeft);
                UILabel(c, P, t > 0 ? C_OK : C_TXT_DM, t.ToString(), 9,
                    "0 1", "0 1", 514, ry - 28, 660, ry, TextAnchor.MiddleCenter);
                UILabel(c, P, C_TXT_MD, u.ToString(), 9,
                    "0 1", "0 1", 664, ry - 28, 810, ry, TextAnchor.MiddleCenter);
                UILabel(c, P, a > 0 ? UIColor(config.ThemeColor, 0.90f) : C_TXT_DM, a > 0 ? a.ToString() : "—", 9,
                    "0 1", "0 1", 814, ry - 28, 956, ry, TextAnchor.MiddleCenter);
            }

            // Footer
            UIPanel(c, P, "", C_BG2, "0 0", "1 0", 0, 0, 0, 28);
            UILabel(c, P, C_TXT_DM, $"{sorted.Count} quest(s)  ·  {_players.Count} tracked player(s)", 8,
                "0 0", "1 0", 12, 0, -120, 28, TextAnchor.MiddleLeft);
            if (totalPages > 1)
            {
                if (page > 0) UIButton(c, P, C_BTN, C_TXT_HI, "◀", 10, "1 0", "1 0", -120, 4, -82, 24, $"iq.adm page {page - 1}");
                UILabel(c, P, C_TXT_HI, $"{page + 1} / {totalPages}", 9, "1 0", "1 0", -82, 4, -34, 24, TextAnchor.MiddleCenter);
                if (page < totalPages - 1) UIButton(c, P, C_BTN, C_TXT_HI, "▶", 10, "1 0", "1 0", -34, 4, 4, 24, $"iq.adm page {page + 1}");
            }
        }

        private void DrawAdminPlayersTab(CuiElementContainer c, string P, BasePlayer admin, UiState state)
        {
            var online = BasePlayer.activePlayerList;

            // Left: player list 260px
            string LP = "IQ_APL";
            UIPanel(c, P, LP, C_BG3, "0 1", "0 1", 0, -532, 260, -72);
            UILabel(c, LP, C_TXT_DM, "ONLINE PLAYERS", 8, "0 1", "1 1", 8, -22, 0, 0, TextAnchor.MiddleLeft, true);
            UIGlowLine(c, LP, "0 1", "1 1", 0, -23, 0, -22);

            for (int i = 0; i < online.Count && i < 17; i++)
            {
                var p = online[i];
                bool sel = state.AdminSelPlayer == p.userID;
                string bg  = sel ? UIColor(config.ThemeColor, 0.75f) : (i % 2 == 0 ? C_BG4 : "0 0 0 0");
                string col = sel ? "0.04 0.04 0.04 1" : C_TXT_HI;
                UIButton(c, LP, bg, col, p.displayName, 9,
                    "0 1", "1 1", 0, -24 - (i + 1) * 27, 0, -24 - i * 27, $"iq.adm selplayer {p.userID}");
            }

            // Divider
            UIGlowLine(c, P, "0 1", "0 1", 260, -532, 261, -72);

            // Right: selected player panel
            string RP = "IQ_APR";
            UIPanel(c, P, RP, C_BG1, "0 1", "1 1", 262, -532, 0, -72);

            if (state.AdminSelPlayer == 0)
            {
                UILabel(c, RP, C_TXT_DM, "Select a player on the left to manage their quests.", 10,
                    "0 0", "1 1", 0, 0, 0, 0, TextAnchor.MiddleCenter);
            }
            else if (!_players.TryGetValue(state.AdminSelPlayer, out var pd))
            {
                UILabel(c, RP, C_TXT_DM, "No quest data found for this player.", 10,
                    "0 0", "1 1", 0, 0, 0, 0, TextAnchor.MiddleCenter);
            }
            else
            {
                // Player header 56px
                UIPanel(c, RP, "", C_BG3, "0 1", "1 1", 0, -56, 0, 0);
                UILabel(c, RP, C_TXT_HI, pd.DisplayName, 12, "0 1", "1 1", 12, -36, -12, 0, TextAnchor.MiddleLeft, true);
                UILabel(c, RP, C_TXT_DM,
                    $"Tier XP: {pd.TierXP}  ·  Reputation: {pd.Reputation}  ·  Completed: {pd.TotalCompletions()}  ·  Active: {pd.ActiveQuests.Count}",
                    9, "0 1", "1 1", 12, -54, -12, -36, TextAnchor.MiddleLeft);
                UIGlowLine(c, RP, "0 1", "1 1", 8, -57, -8, -56);

                float ry = -62f;

                // Active quests section
                if (pd.ActiveQuests.Count > 0)
                {
                    UILabel(c, RP, UIColor(config.ThemeColor, 0.80f), "ACTIVE QUESTS", 8,
                        "0 1", "1 1", 12, ry - 18, 200, ry, TextAnchor.MiddleLeft, true);
                    ry -= 22f;
                    foreach (var aq in pd.ActiveQuests)
                    {
                        if (ry < -455f) break;
                        var def = GetQuest(aq.Id);
                        string title = def?.Title ?? aq.Id;
                        float objPct = 0f;
                        if (def != null && def.Objectives.Count > 0)
                        {
                            int done = 0;
                            for (int oi = 0; oi < def.Objectives.Count; oi++)
                            {
                                int cur = oi < aq.Progress.Count ? aq.Progress[oi] : 0;
                                done += Math.Min(cur, def.Objectives[oi].Count);
                            }
                            int total = def.Objectives.Sum(o => o.Count);
                            if (total > 0) objPct = (float)done / total;
                        }
                        UIPanel(c, RP, "", C_BG4, "0 1", "1 1", 8, ry - 30, -8, ry - 2);
                        UILabel(c, RP, C_TXT_HI, title, 9, "0 1", "1 1", 14, ry - 22, -170, ry - 6, TextAnchor.MiddleLeft);
                        if (objPct > 0f)
                        {
                            UIPanel(c, RP, "", C_BTN, "0 1", "1 1", 14, ry - 27, -170, ry - 24);
                            UIPanel(c, RP, "", UIColor(config.ThemeColor, 0.80f), "0 1", "1 1",
                                14, ry - 27, 14 + (int)(156f * Mathf.Clamp01(objPct)), ry - 24);
                        }
                        UIButton(c, RP, C_OK_BG, C_OK, "✓ Complete", 8,
                            "1 1", "1 1", -164, ry - 26, -82, ry - 6, $"iq.adm complete {state.AdminSelPlayer} {aq.Id}");
                        UIButton(c, RP, C_ERR_BG, C_ERR, "✕ Abandon", 8,
                            "1 1", "1 1", -80, ry - 26, 0, ry - 6, $"iq.adm abandon {state.AdminSelPlayer} {aq.Id}");
                        ry -= 34f;
                    }
                }

                // Ready to collect section
                if (pd.ReadyToCollect.Count > 0 && ry >= -438f)
                {
                    UILabel(c, RP, C_OK, "READY TO COLLECT", 8,
                        "0 1", "1 1", 12, ry - 18, 200, ry, TextAnchor.MiddleLeft, true);
                    ry -= 22f;
                    foreach (var aq in pd.ReadyToCollect)
                    {
                        if (ry < -455f) break;
                        var def = GetQuest(aq.Id);
                        string title = def?.Title ?? aq.Id;
                        UIPanel(c, RP, "", C_OK_BG, "0 1", "1 1", 8, ry - 30, -8, ry - 2);
                        UILabel(c, RP, C_OK, title, 9, "0 1", "1 1", 14, ry - 22, -170, ry - 6, TextAnchor.MiddleLeft);
                        UIButton(c, RP, C_OK_BG, C_OK, "✓ Collect", 8,
                            "1 1", "1 1", -164, ry - 26, -82, ry - 6, $"iq.adm collect {state.AdminSelPlayer} {aq.Id}");
                        UIButton(c, RP, C_ERR_BG, C_ERR, "✕ Abandon", 8,
                            "1 1", "1 1", -80, ry - 26, 0, ry - 6, $"iq.adm abandon {state.AdminSelPlayer} {aq.Id}");
                        ry -= 34f;
                    }
                }

                if (pd.ActiveQuests.Count == 0 && pd.ReadyToCollect.Count == 0)
                    UILabel(c, RP, C_TXT_DM, "This player has no active quests.", 10,
                        "0 1", "1 1", 0, ry - 40, 0, ry, TextAnchor.MiddleCenter);

                // Footer with full-reset button
                UIPanel(c, RP, "", C_BG2, "0 0", "1 0", 0, 0, 0, 28);
                UIButton(c, RP, C_ERR_BG, C_ERR, "⚠ FULL DATA RESET", 8,
                    "0 0", "0 0", 8, 4, 148, 24, $"iq.adm fullreset {state.AdminSelPlayer}");
                UILabel(c, RP, C_TXT_DM, "Wipes ALL quest data for this player (active, completed, XP, reputation).", 8,
                    "0 0", "1 0", 156, 4, -8, 24, TextAnchor.MiddleLeft);
            }

            // Shared footer for no-player-selected case
            if (state.AdminSelPlayer == 0 || !_players.ContainsKey(state.AdminSelPlayer))
                UIPanel(c, P, "", C_BG2, "0 0", "1 0", 0, 0, 0, 28);
        }

        [ConsoleCommand("iq.adm")]
        private void ConCmdAdm(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (!arg.IsAdmin && !permission.UserHasPermission(player.UserIDString, PERM_ADMIN)) return;

            if (!_ui.TryGetValue(player.userID, out var state))
            { state = new UiState(); _ui[player.userID] = state; }

            string action = arg.GetString(0);
            switch (action)
            {
                case "open":
                    OpenAdminUI(player);
                    break;
                case "close":
                    CuiHelper.DestroyUi(player, UI_ADMIN);
                    break;
                case "tab":
                    state.AdminTab  = arg.GetString(1, "stats");
                    state.AdminPage = 0;
                    OpenAdminUI(player);
                    break;
                case "page":
                    state.AdminPage = arg.GetInt(1, 0);
                    OpenAdminUI(player);
                    break;
                case "selplayer":
                    if (ulong.TryParse(arg.GetString(1), out ulong selUid))
                    {
                        state.AdminSelPlayer = state.AdminSelPlayer == selUid ? 0 : selUid; // toggle
                        state.AdminTab = "players";
                    }
                    OpenAdminUI(player);
                    break;
                case "complete":
                {
                    string sid = arg.GetString(1);
                    string qid = arg.GetString(2);
                    var target = BasePlayer.Find(sid) ?? BasePlayer.FindSleeping(sid);
                    if (target == null && ulong.TryParse(sid, out ulong tuid))
                        target = BasePlayer.FindByID(tuid);
                    if (target == null) { SendReply(player, "[IQ Admin] Player not found."); break; }
                    var td = GetOrCreate(target);
                    for (int i = td.ActiveQuests.Count - 1; i >= 0; i--)
                        if (td.ActiveQuests[i].Id == qid) { OnQuestObjectivesMet(target, td, i); break; }
                    var def = GetQuest(qid);
                    if (def != null && GetReadyQuest(td, qid) != null)
                    {
                        td.ReadyToCollect.RemoveAll(r => r.Id == qid);
                        FinalizeQuestRewards(target, td, def);
                        UpdateHud(target);
                        SavePlayerData();
                        SendReply(player, $"[IQ Admin] Force-completed <{qid}> for {target.displayName}.");
                    }
                    OpenAdminUI(player);
                    break;
                }
                case "collect":
                {
                    string sid = arg.GetString(1);
                    string qid = arg.GetString(2);
                    var target = BasePlayer.Find(sid) ?? BasePlayer.FindSleeping(sid);
                    if (target == null && ulong.TryParse(sid, out ulong tuid2))
                        target = BasePlayer.FindByID(tuid2);
                    if (target == null) { SendReply(player, "[IQ Admin] Player not found."); break; }
                    var td = GetOrCreate(target);
                    var def = GetQuest(qid);
                    if (def != null && GetReadyQuest(td, qid) != null)
                    {
                        td.ReadyToCollect.RemoveAll(r => r.Id == qid);
                        FinalizeQuestRewards(target, td, def);
                        UpdateHud(target);
                        SavePlayerData();
                        SendReply(player, $"[IQ Admin] Collected rewards for <{qid}> for {target.displayName}.");
                    }
                    OpenAdminUI(player);
                    break;
                }
                case "abandon":
                {
                    string sid = arg.GetString(1);
                    string qid = arg.GetString(2);
                    if (!ulong.TryParse(sid, out ulong tuid3) || !_players.TryGetValue(tuid3, out var td3))
                    { SendReply(player, "[IQ Admin] Player data not found."); break; }
                    int removed = td3.ActiveQuests.RemoveAll(q => q.Id == qid)
                                + td3.ReadyToCollect.RemoveAll(q => q.Id == qid);
                    if (removed > 0) { _dataDirty = true; SavePlayerData(); }
                    var targetOnline = BasePlayer.FindByID(tuid3);
                    if (targetOnline != null) UpdateHud(targetOnline);
                    SendReply(player, removed > 0
                        ? $"[IQ Admin] Abandoned <{qid}> for {sid}."
                        : $"[IQ Admin] Quest <{qid}> not found in active list.");
                    OpenAdminUI(player);
                    break;
                }
                case "fullreset":
                {
                    string sid = arg.GetString(1);
                    if (!ulong.TryParse(sid, out ulong tuid4) || !_players.TryGetValue(tuid4, out var td4))
                    { SendReply(player, "[IQ Admin] Player data not found."); break; }
                    _players[tuid4] = new PlayerData { DisplayName = td4.DisplayName };
                    _dataDirty = true;
                    SavePlayerData();
                    var targetOnline = BasePlayer.FindByID(tuid4);
                    if (targetOnline != null) UpdateHud(targetOnline);
                    SendReply(player, $"[IQ Admin] Full data reset for {td4.DisplayName} ({sid}).");
                    state.AdminSelPlayer = 0;
                    OpenAdminUI(player);
                    break;
                }
            }
        }

        [ConsoleCommand("iq.complete")]
        private void ConCmdComplete(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !arg.IsAdmin) return;
            string sid = arg.GetString(0);
            string qid = arg.GetString(1);
            var player = BasePlayer.Find(sid);
            if (player == null) { arg.ReplyWith("Player not found."); return; }
            var data = GetOrCreate(player);
            // Search active quests first, then ready-to-collect
            for (int i = data.ActiveQuests.Count - 1; i >= 0; i--)
            {
                if (data.ActiveQuests[i].Id != qid) continue;
                OnQuestObjectivesMet(player, data, i);
                break;
            }
            // Now finalise immediately (bypasses the collect-click step for admin)
            var def = GetQuest(qid);
            if (def != null && GetReadyQuest(data, qid) != null)
            {
                data.ReadyToCollect.RemoveAll(r => r.Id == qid);
                FinalizeQuestRewards(player, data, def);
                UpdateHud(player);
                SavePlayerData();
                arg.ReplyWith($"Force-completed {qid} for {player.displayName}.");
                return;
            }
            arg.ReplyWith("Quest not found in player's active or ready list.");
        }

        [ConsoleCommand("iq.reset")]
        private void ConCmdReset(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !arg.IsAdmin) return;
            string sid = arg.GetString(0);
            bool keepTier = arg.GetBool(1, true);
            if (ulong.TryParse(sid, out ulong uid) && _players.TryGetValue(uid, out var d))
            {
                int xp = d.TierXP;
                _players[uid] = new PlayerData { DisplayName = d.DisplayName, TierXP = keepTier ? xp : 0 };
                SavePlayerData();
                arg.ReplyWith($"Reset quest data for {sid}. Tier XP {(keepTier ? "kept" : "cleared")}.");
            }
            else arg.ReplyWith("Player not found in data.");
        }

        [ConsoleCommand("iq.stats")]
        private void ConCmdStats(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !arg.IsAdmin) return;
            // Count completions per quest across all player records
            var totals   = new Dictionary<string, int>();
            var uniques  = new Dictionary<string, HashSet<ulong>>();
            foreach (var kv in _players)
            {
                foreach (var rec in kv.Value.Completed)
                {
                    if (!totals.ContainsKey(rec.Id))
                    { totals[rec.Id] = 0; uniques[rec.Id] = new HashSet<ulong>(); }
                    totals[rec.Id]  += rec.Times;
                    uniques[rec.Id].Add(kv.Key);
                }
            }
            var sb = new System.Text.StringBuilder("[IQ Stats] Quest completions (most → least):\n");
            foreach (var q in _quests.OrderByDescending(q => totals.TryGetValue(q.Id, out var n) ? n : 0))
            {
                int t = totals.TryGetValue(q.Id, out var tt) ? tt : 0;
                int u = uniques.TryGetValue(q.Id, out var uu) ? uu.Count : 0;
                sb.AppendLine($"  {q.Id,-30} {t,4} completions  {u,4} players  —  {q.Title}");
            }
            arg.ReplyWith(sb.ToString());
        }

        // ─────────────────────── Contractor Admin Commands ───────────────────
        [ConsoleCommand("iq.contractor")]
        private void ConCmdContractor(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && !arg.IsAdmin) return;
            var player = arg.Player();
            string sub = arg.GetString(0, "status");
            var sb = new System.Text.StringBuilder();

            switch (sub)
            {
                // iq.contractor setpos <filter>
                // Stand where you want the NPC, run this — saves foot position to data file.
                case "setpos":
                {
                    if (player == null) { arg.ReplyWith("Must run in-game."); return; }
                    string filter = arg.GetString(1, "").ToLower();
                    if (string.IsNullOrEmpty(filter)) { arg.ReplyWith("Usage: iq.contractor setpos <filter>"); return; }
                    if (!_contractorPos.TryGetValue(filter, out var sp)) sp = new ContractorSavedPos();
                    sp.X         = player.transform.position.x;
                    sp.Y         = player.transform.position.y;
                    sp.Z         = player.transform.position.z;
                    sp.RotationY = player.eyes.rotation.eulerAngles.y;
                    _contractorPos[filter] = sp;
                    SaveContractorPositions();
                    arg.ReplyWith($"Saved position {player.transform.position} facing {sp.RotationY:F0}° for '{filter}'.");
                    break;
                }

                // iq.contractor setname <filter> <name>
                case "setname":
                {
                    string filter = arg.GetString(1, "").ToLower();
                    string name   = arg.GetString(2, "");
                    if (string.IsNullOrEmpty(filter) || string.IsNullOrEmpty(name))
                    { arg.ReplyWith("Usage: iq.contractor setname <filter> <name>"); return; }
                    if (!_contractorPos.TryGetValue(filter, out var sp)) sp = new ContractorSavedPos();
                    sp.Name = name;
                    _contractorPos[filter] = sp;
                    SaveContractorPositions();
                    arg.ReplyWith($"Name for '{filter}' set to '{name}'.");
                    break;
                }

                // iq.contractor clear [filter]  — clear one or all saved positions
                case "clear":
                {
                    string filter = arg.GetString(1, "").ToLower();
                    if (string.IsNullOrEmpty(filter))
                    {
                        _contractorPos.Clear();
                        SaveContractorPositions();
                        arg.ReplyWith("Cleared all saved contractor positions.");
                    }
                    else if (_contractorPos.Remove(filter))
                    { SaveContractorPositions(); arg.ReplyWith($"Cleared saved position for '{filter}'."); }
                    else arg.ReplyWith($"No saved position for '{filter}'.");
                    break;
                }

                // iq.contractor list
                case "list":
                {
                    if (_contractorPos.Count == 0) { arg.ReplyWith("No saved positions."); return; }
                    sb.AppendLine("Saved contractor positions:");
                    foreach (var kv in _contractorPos)
                        sb.AppendLine($"  [{kv.Key}]  pos=({kv.Value.X:F1}, {kv.Value.Y:F1}, {kv.Value.Z:F1})  name='{kv.Value.Name}'");
                    arg.ReplyWith(sb.ToString());
                    break;
                }

                // iq.contractor tp <filter>  — teleport to saved position to verify it
                case "tp":
                {
                    if (player == null) { arg.ReplyWith("Must run in-game."); return; }
                    string filter = arg.GetString(1, "").ToLower();
                    if (!_contractorPos.TryGetValue(filter, out var sp))
                    { arg.ReplyWith($"No saved position for '{filter}'."); return; }
                    player.Teleport(sp.Position);
                    arg.ReplyWith($"Teleported to saved position for '{filter}'.");
                    break;
                }

                // iq.contractor monuments
                case "monuments":
                {
                    if (TerrainMeta.Path?.Monuments == null) { arg.ReplyWith("Monuments not available."); return; }
                    sb.AppendLine($"Total monuments: {TerrainMeta.Path.Monuments.Count}");
                    foreach (var m in TerrainMeta.Path.Monuments)
                        sb.AppendLine($"  display='{m.displayPhrase?.english}'  go='{m.name}'  pos={m.transform.position}");
                    arg.ReplyWith(sb.ToString());
                    break;
                }

                default:
                    arg.ReplyWith(
                        $"NPCs active: {_contractorNpcs.Count}  |  Saved positions: {_contractorPos.Count}\n" +
                        "Commands:\n" +
                        "  iq.contractor setpos <filter>         — save your current foot position\n" +
                        "  iq.contractor setname <filter> <name> — override NPC name for this spot\n" +
                        "  iq.contractor tp <filter>             — teleport to a saved position\n" +
                        "  iq.contractor list                    — show all saved positions\n" +
                        "  iq.contractor clear [filter]          — clear one or all saved positions\n" +
                        "  iq.contractor monuments               — dump all monument names");
                    break;
            }
        }

        // ─────────────────────── HUD Move Command ─────────────────────────────
        [ConsoleCommand("iq.hud")]
        private void ConCmdHud(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var data = GetOrCreate(player);

            string action = arg.GetString(0);
            switch (action)
            {
                case "toggle":
                    if (_hudMoveMode.Contains(player.userID))
                    {
                        _hudMoveMode.Remove(player.userID);
                        SavePlayerData();           // persist new position
                    }
                    else
                    {
                        _hudMoveMode.Add(player.userID);
                    }
                    UpdateHud(player);
                    break;

                case "hide":
                    data.HudVisible = !data.HudVisible;
                    _dataDirty = true;
                    UpdateHud(player);
                    SendReply(player, data.HudVisible
                        ? "<color=#E8912B>[Quests]</color> Contract HUD <color=#2BC259>shown</color>."
                        : "<color=#E8912B>[Quests]</color> Contract HUD <color=#D73333>hidden</color>.  Type <color=#fff>/quest hud</color> to show again.");
                    break;

                case "move":
                    int dx = arg.GetInt(1, 0);
                    int dy = arg.GetInt(2, 0);
                    data.HudX = Mathf.Clamp(data.HudX + dx, -800, 200);
                    data.HudY = Mathf.Clamp(data.HudY + dy, -500, 100);
                    UpdateHud(player);
                    break;

                case "step":
                    int s = arg.GetInt(1, 5);
                    _hudMoveStep[player.userID] = s;
                    UpdateHud(player);
                    break;

                case "reset":
                    data.HudX = 0;
                    data.HudY = 0;
                    UpdateHud(player);
                    break;
            }
        }

        // iq.event progress <steamid> <type> <target> <amount>
        // Lets any external plugin award objective progress without a direct plugin reference.
        // Example: iq.event progress 76561198000000000 event_win convoy 1
        [ConsoleCommand("iq.event")]
        private void ConCmdEvent(ConsoleSystem.Arg arg)
        {
            // Only server console or admin players may call this
            if (arg.Player() != null && !arg.Player().IsAdmin) return;

            string sub = arg.GetString(0, "").ToLower();
            if (sub != "progress")
            {
                arg.ReplyWith("Usage: iq.event progress <steamid> <type> <target> <amount>");
                return;
            }

            string steamIdStr = arg.GetString(1, "");
            string evType     = arg.GetString(2, "").ToLower();
            string evTarget   = arg.GetString(3, "").ToLower();
            int    evAmount   = arg.GetInt(4, 1);

            if (!ulong.TryParse(steamIdStr, out ulong steamId) || steamId < 70000000000000000UL)
            {
                arg.ReplyWith("Invalid SteamID.");
                return;
            }
            if (string.IsNullOrEmpty(evType))
            {
                arg.ReplyWith("Usage: iq.event progress <steamid> <type> <target> <amount>");
                return;
            }

            var player = BasePlayer.FindByID(steamId);
            if (player == null)
            {
                arg.ReplyWith($"Player {steamId} not found or not online.");
                return;
            }

            AwardProgress(player, evType, evTarget, evAmount);
            arg.ReplyWith($"Awarded {evAmount}x {evType}/{evTarget} progress to {player.displayName}.");
        }

        // ─────────────────────── Helpers ──────────────────────────────────────
        private string BuildRewardPreview(QuestDefinition def)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < def.Rewards.Count && i < 3; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append(BuildRewardLabel(def.Rewards[i]));
            }
            if (def.Rewards.Count > 3) sb.Append($"  +{def.Rewards.Count - 3}");
            return sb.ToString();
        }

        // Short label for card reward row (no shortname — just amount)
        private static string RewardShortLabel(RewardDef r)
        {
            switch (r.Type.ToLower())
            {
                case "item":           return $"x{r.Amount}";
                case "blueprint":      return $"BP";
                case "tier_xp":        return $"+{r.Amount}XP";
                case "reputation":     return $"+{r.Amount}Rep";
                case "economics":      return $"${r.Amount}";
                case "server_rewards": return $"{r.Amount}RP";
                case "command":        return "Cmd";
                default:               return r.Type;
            }
        }

        private string BuildRewardLabel(RewardDef r)
        {
            switch (r.Type.ToLower())
            {
                case "item":         return $"{r.Shortname} x{r.Amount}";
                case "blueprint":    return $"BP:{r.Shortname}";
                case "tier_xp":      return $"+{r.Amount} XP";
                case "reputation":   return $"+{r.Amount} Rep";
                case "economics":    return $"${r.Amount}";
                case "server_rewards": return $"{r.Amount} RP";
                case "command":      return "Custom";
                default:             return r.Type;
            }
        }

        private string Stars(int count, int max = 5)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < max; i++) sb.Append(i < count ? "★" : "☆");
            return sb.ToString();
        }

        private string FormatTime(TimeSpan t)
        {
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m";
            if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes}m {t.Seconds}s";
            return $"{t.Seconds}s";
        }

        private void PostDiscord(string message)
        {
            if (string.IsNullOrEmpty(config.DiscordWebhook)) return;
            string payload = $"{{\"content\":\"{message.Replace("\"", "\\\"").Replace("*", "").Replace("<", "").Replace(">", "")}\"}}";
            webrequest.Enqueue(config.DiscordWebhook, payload, (code, resp) => { }, this,
                Oxide.Core.Libraries.RequestMethod.POST,
                new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        }

        private string GetImage(string shortname, ulong skinId = 0)
        {
            if (!config.UseImageLibrary || ImageLibrary == null) return null;
            bool has = (bool)(ImageLibrary.Call("HasImage", shortname, skinId) ?? false);
            if (!has) return null;
            return (string)ImageLibrary.Call("GetImage", shortname, skinId);
        }

        // Returns the ImageLibrary icon for a kill objective target, or null if unavailable.
        // Always looks up the "iq_kill_<target>" key — PrefetchQuestImages registers all of them
        // (either from a custom URL in config, or the item-icon fallback).
        private string GetKillObjectiveIcon(ObjectiveDef obj)
        {
            if (obj == null || (obj.Type?.ToLower() ?? "") != "kill") return null;
            string target = obj.Target?.ToLower() ?? "";
            if (string.IsNullOrEmpty(target)) return null;
            return GetImage(KillIconKey(target), 0);
        }

        // Returns the native Rust item ID for non-kill objective types, or 0 if unresolvable.
        // Uses ItemManager directly — no external plugin dependency.
        private static int GetObjectiveIconItemId(ObjectiveDef obj)
        {
            if (obj == null) return 0;
            string type   = obj.Type?.ToLower()   ?? "";
            string target = obj.Target?.ToLower() ?? "";

            switch (type)
            {
                case "craft":
                case "deploy":
                case "research":
                case "recycle":
                case "gather":
                case "pickup":
                case "harvest":
                case "purchase":
                case "deliver":
                {
                    if (string.IsNullOrEmpty(target)) return 0;
                    var idef = ItemManager.FindItemDefinition(target);
                    return idef?.itemid ?? 0;
                }
                case "chop":
                {
                    // Use the target shortname if set (e.g. "wood"), else fall back to hatchet icon
                    if (!string.IsNullOrEmpty(target)) { var td = ItemManager.FindItemDefinition(target); if (td != null) return td.itemid; }
                    return ItemManager.FindItemDefinition("hatchet")?.itemid ?? 0;
                }
                case "mine":
                {
                    // Use the target shortname if set (e.g. "sulfur.ore"), else fall back to pickaxe icon
                    if (!string.IsNullOrEmpty(target)) { var td = ItemManager.FindItemDefinition(target); if (td != null) return td.itemid; }
                    return ItemManager.FindItemDefinition("pickaxe")?.itemid ?? 0;
                }
                case "skin":
                {
                    var idef = ItemManager.FindItemDefinition("fat.animal");
                    return idef?.itemid ?? 0;
                }
                case "fish":
                {
                    string fishSn = string.IsNullOrEmpty(target) ? "fish.troutsmall" : target;
                    var idef = ItemManager.FindItemDefinition(fishSn);
                    return idef?.itemid ?? 0;
                }
                case "heal":
                {
                    string healSn = string.IsNullOrEmpty(target) ? "bandage" : target;
                    var idef = ItemManager.FindItemDefinition(healSn);
                    return idef?.itemid ?? 0;
                }
                case "repair":
                {
                    // Generic repair icon — hammer
                    var idef = ItemManager.FindItemDefinition("hammer");
                    return idef?.itemid ?? 0;
                }
                // Event-type objectives: use a recognisable item as a visual stand-in
                case "event_win":
                case "bradley_tier":
                {
                    // Supply signal — represents a major action/event drop
                    var idef = ItemManager.FindItemDefinition("supply.signal");
                    return idef?.itemid ?? 0;
                }
                case "boss_kill":
                {
                    var idef = ItemManager.FindItemDefinition("skull.human");
                    return idef?.itemid ?? 0;
                }
                case "raidable_base":
                {
                    var idef = ItemManager.FindItemDefinition("explosive.timed");
                    return idef?.itemid ?? 0;
                }
                case "dungeon_win":
                {
                    var idef = ItemManager.FindItemDefinition("keycard_green");
                    return idef?.itemid ?? 0;
                }
                case "hack_crate":
                    return ItemManager.FindItemDefinition("keycard_green")?.itemid ?? 0;
                case "loot":
                {
                    if (!string.IsNullOrEmpty(target))
                    {
                        if (target.Contains("elite"))        return ItemManager.FindItemDefinition("keycard_red")?.itemid    ?? 0;
                        if (target.Contains("bradley"))      return ItemManager.FindItemDefinition("ammo.rocket.basic")?.itemid ?? 0;
                        if (target.Contains("heli"))         return ItemManager.FindItemDefinition("ammo.rocket.hv")?.itemid ?? 0;
                        if (target.Contains("supply"))       return ItemManager.FindItemDefinition("supply.signal")?.itemid  ?? 0;
                        if (target.Contains("barrel"))       return ItemManager.FindItemDefinition("oil_barrel")?.itemid     ?? 0;
                        if (target.Contains("tool"))         return ItemManager.FindItemDefinition("toolgun")?.itemid        ?? 0;
                        if (target.Contains("food"))         return ItemManager.FindItemDefinition("can.beans")?.itemid      ?? 0;
                        if (target.Contains("crate_normal")) return ItemManager.FindItemDefinition("keycard_blue")?.itemid   ?? 0;
                        if (target.Contains("crate_basic"))  return ItemManager.FindItemDefinition("box.wooden")?.itemid     ?? 0;
                    }
                    return ItemManager.FindItemDefinition("box.wooden.large")?.itemid ?? 0;
                }
                case "quarry_upgrade":
                case "quarry_place":
                {
                    var idef = ItemManager.FindItemDefinition("mining.quarry");
                    return idef?.itemid ?? 0;
                }
                case "skilltree_level":
                {
                    var idef = ItemManager.FindItemDefinition("book.safezone");
                    return idef?.itemid ?? 0;
                }
                default:
                    return 0;
            }
        }

        // Renders a native Rust item icon using the game engine — no ImageLibrary required.
        private static void UIItemIcon(CuiElementContainer c, string parent, int itemId,
            string aMin, string aMax, float oMinX, float oMinY, float oMaxX, float oMaxY,
            ulong skinId = 0)
        {
            if (itemId == 0) return;
            c.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = itemId, SkinId = skinId },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = aMin, AnchorMax = aMax,
                        OffsetMin = $"{oMinX} {oMinY}", OffsetMax = $"{oMaxX} {oMaxY}"
                    }
                }
            });
        }

        private static void UIImage(CuiElementContainer c, string parent, string png,
            string aMin, string aMax, float oMinX, float oMinY, float oMaxX, float oMaxY)
        {
            c.Add(new CuiElement
            {
                Parent = parent,
                Components = {
                    new CuiRawImageComponent { Png = png },
                    new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = $"{oMinX} {oMinY}", OffsetMax = $"{oMaxX} {oMaxY}" }
                }
            });
        }

        private static string ObjectiveTypeDisplay(string type)
        {
            switch (type?.ToLower())
            {
                case "kill":             return "Kill";
                case "chop":             return "Chop";
                case "mine":             return "Mine";
                case "skin":             return "Skin";
                case "gather":           return "Gather";
                case "craft":            return "Craft";
                case "research":         return "Research";
                case "loot":             return "Loot";
                case "hack_crate":       return "Hack Crate";
                case "upgrade_building": return "Upgrade";
                case "recycle":          return "Recycle";
                case "fish":             return "Catch";
                case "deploy":           return "Deploy";
                case "pickup":           return "Pick Up";
                case "heal":             return "Heal";
                case "harvest":          return "Harvest";
                case "repair":           return "Repair";
                case "purchase":         return "Purchase";
                case "deliver":          return "Deliver";
                case "event_win":        return "Win Event";
                case "boss_kill":        return "Kill Boss";
                case "raidable_base":    return "Raid Base";
                case "dungeon_win":      return "Complete Dungeon";
                case "quarry_upgrade":   return "Upgrade Quarry";
                case "quarry_place":     return "Place Quarry";
                case "bradley_tier":     return "Kill Bradley";
                case "skilltree_level":  return "Reach Level";
                default:                 return type ?? "";
            }
        }

        // ─────────────────────── UI Helpers ───────────────────────────────────
        // Neon glow separator — core bright line + soft falloff on each side
        private void UIGlowLine(CuiElementContainer c, string parent,
            string aMin, string aMax, float x1, float y1, float x2, float y2)
        {
            string core  = UIColor(config.ThemeColor, 0.92f);
            string inner = UIColor(config.ThemeColor, 0.24f);
            string outer = UIColor(config.ThemeColor, 0.07f);
            bool horiz   = Math.Abs(x2 - x1) >= Math.Abs(y2 - y1);
            if (horiz)
            {
                UIPanel(c, parent, "", outer, aMin, aMax, x1, y2 + 2f, x2, y2 + 4f);
                UIPanel(c, parent, "", inner, aMin, aMax, x1, y2,       x2, y2 + 2f);
                UIPanel(c, parent, "", core,  aMin, aMax, x1, y1,       x2, y2);
                UIPanel(c, parent, "", inner, aMin, aMax, x1, y1 - 2f,  x2, y1);
                UIPanel(c, parent, "", outer, aMin, aMax, x1, y1 - 4f,  x2, y1 - 2f);
            }
            else
            {
                UIPanel(c, parent, "", outer, aMin, aMax, x1 - 4f, y1, x1 - 2f, y2);
                UIPanel(c, parent, "", inner, aMin, aMax, x1 - 2f, y1, x1,      y2);
                UIPanel(c, parent, "", core,  aMin, aMax, x1,      y1, x2,      y2);
                UIPanel(c, parent, "", inner, aMin, aMax, x2,      y1, x2 + 2f, y2);
                UIPanel(c, parent, "", outer, aMin, aMax, x2 + 2f, y1, x2 + 4f, y2);
            }
        }

        private static void UIPanel(CuiElementContainer c, string parent, string name, string color,
            string aMin, string aMax, float oMinX, float oMinY, float oMaxX, float oMaxY,
            string material = null)
        {
            var el = new CuiElement
            {
                Parent = parent,
                Components = {
                    new CuiImageComponent { Color = color, Material = material },
                    new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = $"{oMinX} {oMinY}", OffsetMax = $"{oMaxX} {oMaxY}" }
                }
            };
            if (!string.IsNullOrEmpty(name)) el.Name = name;
            c.Add(el);
        }

        private const string MAT_BLUR = "assets/content/ui/uibackgroundblur-ingamemenu.mat";

        private static void UILabel(CuiElementContainer c, string parent, string color, string text, int size,
            string aMin, string aMax, float oMinX, float oMinY, float oMaxX, float oMaxY,
            TextAnchor align = TextAnchor.MiddleLeft, bool bold = false)
        {
            c.Add(new CuiElement
            {
                Parent = parent,
                Components = {
                    new CuiTextComponent { Text = text, FontSize = size, Color = color, Align = align,
                        Font = bold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf" },
                    new CuiRectTransformComponent { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = $"{oMinX} {oMinY}", OffsetMax = $"{oMaxX} {oMaxY}" }
                }
            });
        }

        private static void UIButton(CuiElementContainer c, string parent, string color, string textColor,
            string text, int size, string aMin, string aMax, float oMinX, float oMinY, float oMaxX, float oMaxY,
            string command, bool bold = false)
        {
            c.Add(new CuiButton
            {
                Button = { Color = color, Command = command },
                RectTransform = { AnchorMin = aMin, AnchorMax = aMax, OffsetMin = $"{oMinX} {oMinY}", OffsetMax = $"{oMaxX} {oMaxY}" },
                Text = { Text = text, FontSize = size, Color = textColor, Align = TextAnchor.MiddleCenter,
                    Font = bold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf" }
            }, parent);
        }

        private static string UIColor(string hex, float alpha)
        {
            if (string.IsNullOrEmpty(hex)) return $"1 1 1 {alpha:F2}";
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length < 6) return $"1 1 1 {alpha:F2}";
            int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            return $"{r / 255f:F3} {g / 255f:F3} {b / 255f:F3} {alpha:F2}";
        }
    }
}
