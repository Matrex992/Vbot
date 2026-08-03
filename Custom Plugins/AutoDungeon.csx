using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vBot.Scripting.API;
using vBot.Core.Components;
using vBot.Core.Components.Scripting;
using vBot.Core.Objects.Spawn;

// AutoDungeon.csx - A C# port of the xAutoDungeon phBot plugin
// Adds death recovery, filtering, and loot waiting to the 'attackarea' command.
// Adds a 'godimensional' script command for Forgotten World.

public class AutoDungeonPlugin
{
    private static readonly string PluginName = "AutoDungeon";

    // GUI Controls
    private ScriptPanel _panel;
    private CheckBox _cbxEnableDeathRecovery;
    private TextBox _tbxCharName;
    private ListBox _lstCharWhitelist;
    
    // Ignore Name list
    private TextBox _tbxMobs;
    private ListBox _lstMobs;
    
    // Ignore Types
    private CheckBox _cbxIgnoreGeneral, _cbxOnlyCountGeneral;
    private CheckBox _cbxIgnoreChampion, _cbxOnlyCountChampion;
    private CheckBox _cbxIgnoreGiant, _cbxOnlyCountGiant;
    private CheckBox _cbxIgnoreTitan, _cbxOnlyCountTitan;
    private CheckBox _cbxIgnoreStrong, _cbxOnlyCountStrong;
    private CheckBox _cbxIgnoreElite, _cbxOnlyCountElite;
    private CheckBox _cbxIgnoreUnique, _cbxOnlyCountUnique;
    private CheckBox _cbxIgnoreUnique2, _cbxOnlyCountUnique2;
    private CheckBox _cbxIgnoreParty, _cbxOnlyCountParty;
    private CheckBox _cbxIgnoreChampionParty, _cbxOnlyCountChampionParty;
    private CheckBox _cbxIgnoreGiantParty, _cbxOnlyCountGiantParty;

    // Others
    private CheckBox _cbxAcceptForgottenWorld;

    private List<string> _charWhitelist = new();
    private List<string> _mobIgnoreNames = new();
    
    // Using simple int lists for type ignoring like Python
    private List<int> _ignoreTypes = new();
    private List<int> _onlyCountTypes = new();

    private bool _loading = false;
    private string _attackHookId = null;

    private static readonly Dictionary<int, string> _typeIdToRarityName = new()
    {
        { 0, "General" }, { 1, "Champion" }, { 3, "Unique" }, { 4, "Giant" }, { 5, "Titan" },
        { 6, "Elite" }, { 7, "EliteStrong" }, { 8, "Unique2" },
        { 16, "GeneralParty" }, { 17, "ChampionParty" }, { 20, "GiantParty" }
    };

    public AutoDungeonPlugin()
    {
    }

    public void Initialize()
    {
        try
        {
            CreateGUI();
            LoadConfig();
            
            // Register Forgotten World Hook
            Packets.OnServerPacket(0x751A, HandleFWInvite);
            
            // Sync ignore/prefer lists to the Monsters API so the targeting core
            // respects our filters natively (no manual OnFilterTarget needed).
            SyncMonstersApi();
            
            // Register Custom Script Commands into vBot memory
            RegisterScriptCommand(new AutoDungeonAttackAreaCommand(this));
            RegisterScriptCommand(new AutoDungeonGoDimensionalCommand(this));
        }
        catch (Exception ex)
        {
            Log.Error($"[{PluginName}] INIT FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void RegisterScriptCommand(IScriptCommand cmd)
    {
        // CommandHandlers is List<IScriptCommand>, find by name
        var existing = ScriptManager.CommandHandlers.FirstOrDefault(h => h.Name.Equals(cmd.Name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            // Override existing (like the default attackarea)
            var idx = ScriptManager.CommandHandlers.IndexOf(existing);
            ScriptManager.CommandHandlers[idx] = cmd;
        }
        else
        {
            ScriptManager.CommandHandlers.Add(cmd);
        }
    }

    public void Shutdown()
    {
        try
        {
            // Cleanup GUI
            _panel?.Dispose();

            // Clear script-level monster preferences so they don't leak after unload
            Monsters.ClearAll();

            // Remove Commands
            var atkCmd = ScriptManager.CommandHandlers.FirstOrDefault(h => h.Name.Equals("attackarea", StringComparison.OrdinalIgnoreCase));
            if (atkCmd != null) ScriptManager.CommandHandlers.Remove(atkCmd);
            var dimCmd = ScriptManager.CommandHandlers.FirstOrDefault(h => h.Name.Equals("godimensional", StringComparison.OrdinalIgnoreCase));
            if (dimCmd != null) ScriptManager.CommandHandlers.Remove(dimCmd);
        }
        catch (Exception ex)
        {
            Log.Error($"[{PluginName}] SHUTDOWN FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    #region GUI Creation & Config
    
    private void CreateGUI()
    {
        _panel = GUI.CreatePanel(PluginName, "Death Recovery for Area Hunting");

        // Death Recovery
        _panel.CreateLabel("--- Death Recovery Settings ---", 10, 10);
        _cbxEnableDeathRecovery = _panel.CreateCheckBox("Enable Death Recovery", 10, 30, false, _ => SaveConfig());
        _panel.CreateLabel("Characters to monitor (Empty = All):", 10, 55);
        _tbxCharName = _panel.CreateTextBox(10, 75, 120);
        _panel.CreateButton("Add", 135, 74, 50, 22, () => {
            string name = ScriptPanel.GetText(_tbxCharName);
            if (!string.IsNullOrWhiteSpace(name) && !_charWhitelist.Contains(name)) {
                _charWhitelist.Add(name);
                ScriptPanel.AddItem(_lstCharWhitelist, name);
                ScriptPanel.SetText(_tbxCharName, "");
                SaveConfig();
            }
        });
        _lstCharWhitelist = _panel.CreateListBox(10, 100, 175, 80);
        _panel.CreateButton("Remove Selected", 10, 185, 175, 25, () => {
             string sel = ScriptPanel.GetSelectedItem(_lstCharWhitelist);
             if (!string.IsNullOrEmpty(sel)) {
                 _charWhitelist.Remove(sel);
                 ScriptPanel.RemoveItem(_lstCharWhitelist, sel);
                 SaveConfig();
             }
        });

        // Forgotten World
        _panel.CreateLabel("--- Forgotten World ---", 10, 220);
        _cbxAcceptForgottenWorld = _panel.CreateCheckBox("Auto Accept FW Invitations", 10, 240, false, _ => SaveConfig());

        // Ignore Mobs (Name)
        _panel.CreateLabel("--- Add monster names to ignore ---", 220, 10);
        _tbxMobs = _panel.CreateTextBox(220, 30, 120);
        _panel.CreateButton("Add", 345, 29, 50, 22, () => {
            string name = NormalizeMobName(ScriptPanel.GetText(_tbxMobs));
            if (!string.IsNullOrWhiteSpace(name) && !ContainsIgnoreMobName(name)) {
                _mobIgnoreNames.Add(name);
                ScriptPanel.AddItem(_lstMobs, name);
                ScriptPanel.SetText(_tbxMobs, "");
                SaveConfig();
                SyncMonstersApi();
            }
        });
        _lstMobs = _panel.CreateListBox(220, 55, 175, 125);
        _panel.CreateButton("Remove Selected", 220, 185, 175, 25, () => {
             string sel = NormalizeMobName(ScriptPanel.GetSelectedItem(_lstMobs));
             if (!string.IsNullOrEmpty(sel)) {
                 _mobIgnoreNames.RemoveAll(m => NormalizeMobName(m).Equals(sel, StringComparison.OrdinalIgnoreCase));
                 ScriptPanel.RemoveItem(_lstMobs, sel);
                 SaveConfig();
                 SyncMonstersApi();
             }
        });

        // Filters by Type
        _panel.CreateLabel("--- Monster Counter Preferences ---", 430, 10);
        _panel.CreateLabel("Ignore", 540, 25);
        _panel.CreateLabel("Only Count", 600, 25);
        
        int y = 50;
        _panel.CreateLabel("General (0)", 430, y);
        _cbxIgnoreGeneral = CreateTypeFilterCB(_panel, 540, y, 0, _ignoreTypes);
        _cbxOnlyCountGeneral = CreateTypeFilterCB(_panel, 600, y, 0, _onlyCountTypes);
        
        y += 20;
        _panel.CreateLabel("Champion (1)", 430, y);
        _cbxIgnoreChampion = CreateTypeFilterCB(_panel, 540, y, 1, _ignoreTypes);
        _cbxOnlyCountChampion = CreateTypeFilterCB(_panel, 600, y, 1, _onlyCountTypes);
        
        y += 20;
        _panel.CreateLabel("Unique (3)", 430, y);
        _cbxIgnoreUnique = CreateTypeFilterCB(_panel, 540, y, 3, _ignoreTypes);
        _cbxOnlyCountUnique = CreateTypeFilterCB(_panel, 600, y, 3, _onlyCountTypes);

        y += 20;
        _panel.CreateLabel("Giant (4)", 430, y);
        _cbxIgnoreGiant = CreateTypeFilterCB(_panel, 540, y, 4, _ignoreTypes);
        _cbxOnlyCountGiant = CreateTypeFilterCB(_panel, 600, y, 4, _onlyCountTypes);

        y += 20;
        _panel.CreateLabel("Titan (5)", 430, y);
        _cbxIgnoreTitan = CreateTypeFilterCB(_panel, 540, y, 5, _ignoreTypes);
        _cbxOnlyCountTitan = CreateTypeFilterCB(_panel, 600, y, 5, _onlyCountTypes);

        y += 20;
        _panel.CreateLabel("Elite (6)", 430, y);
        _cbxIgnoreStrong = CreateTypeFilterCB(_panel, 540, y, 6, _ignoreTypes);
        _cbxOnlyCountStrong = CreateTypeFilterCB(_panel, 600, y, 6, _onlyCountTypes);

        y += 20;
        _panel.CreateLabel("EliteStrong (7)", 430, y);
        _cbxIgnoreElite = CreateTypeFilterCB(_panel, 540, y, 7, _ignoreTypes);
        _cbxOnlyCountElite = CreateTypeFilterCB(_panel, 600, y, 7, _onlyCountTypes);

        y += 20;
        _panel.CreateLabel("Unique2 (8)", 430, y);
        _cbxIgnoreUnique2 = CreateTypeFilterCB(_panel, 540, y, 8, _ignoreTypes);
        _cbxOnlyCountUnique2 = CreateTypeFilterCB(_panel, 600, y, 8, _onlyCountTypes);

        y += 20;
        _panel.CreateLabel("Party (16)", 430, y);
        _cbxIgnoreParty = CreateTypeFilterCB(_panel, 540, y, 16, _ignoreTypes);
        _cbxOnlyCountParty = CreateTypeFilterCB(_panel, 600, y, 16, _onlyCountTypes);

        y += 20;
        _panel.CreateLabel("ChampionParty (17)", 430, y);
        _cbxIgnoreChampionParty = CreateTypeFilterCB(_panel, 540, y, 17, _ignoreTypes);
        _cbxOnlyCountChampionParty = CreateTypeFilterCB(_panel, 600, y, 17, _onlyCountTypes);

        y += 20;
        _panel.CreateLabel("GiantParty (20)", 430, y);
        _cbxIgnoreGiantParty = CreateTypeFilterCB(_panel, 540, y, 20, _ignoreTypes);
        _cbxOnlyCountGiantParty = CreateTypeFilterCB(_panel, 600, y, 20, _onlyCountTypes);
    }

    private CheckBox CreateTypeFilterCB(ScriptPanel panel, int x, int y, int typeId, List<int> backingList)
    {
        return panel.CreateCheckBox("", x, y, false, isChecked => {
            if (isChecked && !backingList.Contains(typeId))
                backingList.Add(typeId);
            else if (!isChecked && backingList.Contains(typeId))
                backingList.Remove(typeId);
            SaveConfig();
            SyncMonstersApi();
        });
    }

    private void LoadConfig()
    {
        _loading = true;
        try
        {
            Config.Load(PluginName);

            // Idempotent reload: clear local lists and listbox UI before re-populating
            // so this method is safe to call again (e.g. when Config auto-reloads after
            // the character first becomes available, or on a mid-session char switch).
            _charWhitelist.Clear();
            ScriptPanel.ClearList(_lstCharWhitelist);
            _mobIgnoreNames.Clear();
            ScriptPanel.ClearList(_lstMobs);

            ScriptPanel.SetChecked(_cbxEnableDeathRecovery, Config.Get(PluginName, "EnableDeathRecovery", false));
            ScriptPanel.SetChecked(_cbxAcceptForgottenWorld, Config.Get(PluginName, "AcceptFW", false));

            var cw = Config.Get(PluginName, "CharWhitelist", new string[0]);
            foreach (var c in cw) {
                _charWhitelist.Add(c);
                ScriptPanel.AddItem(_lstCharWhitelist, c);
            }

            var mn = Config.Get(PluginName, "IgnoreNames", new string[0]);
            foreach (var m in mn) {
                var normalized = NormalizeMobName(m);
                if (!string.IsNullOrWhiteSpace(normalized) && !ContainsIgnoreMobName(normalized))
                {
                    _mobIgnoreNames.Add(normalized);
                    ScriptPanel.AddItem(_lstMobs, normalized);
                }
            }

            // Mutate in-place: checkbox lambdas captured these list references at creation time,
            // so reassigning the fields to new objects would leave the lambdas writing to stale lists.
            _ignoreTypes.Clear();
            _ignoreTypes.AddRange(Config.Get<int[]>(PluginName, "IgnoreTypes", new int[0]));
            _onlyCountTypes.Clear();
            _onlyCountTypes.AddRange(Config.Get<int[]>(PluginName, "OnlyCountTypes", new int[0]));

            // Refresh checkboxes safely
            UpdateCheckboxesFromList(_ignoreTypes, true);
            UpdateCheckboxesFromList(_onlyCountTypes, false);

            // Push the freshly-loaded ignore lists into the core targeting API.
            SyncMonstersApi();
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    ///     Public entry point used by the OnScriptConfigReloaded subscription
    ///     in the top-level script body. Re-reads the per-character config and
    ///     refreshes the panel UI when vBot finally resolves a real character
    ///     name (or when the user switches characters mid-session).
    /// </summary>
    public void ReloadFromConfig()
    {
        try
        {
            LoadConfig();
        }
        catch (Exception ex)
        {
            Log.Error($"[{PluginName}] ReloadFromConfig failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void UpdateCheckboxesFromList(List<int> list, bool isIgnore)
    {
        void Set(int typeId, CheckBox ignoreCb, CheckBox onlyCountCb)
        {
            var cb = isIgnore ? ignoreCb : onlyCountCb;
            ScriptPanel.SetChecked(cb, list.Contains(typeId));
        }

        Set(0, _cbxIgnoreGeneral, _cbxOnlyCountGeneral);
        Set(1, _cbxIgnoreChampion, _cbxOnlyCountChampion);
        Set(3, _cbxIgnoreUnique, _cbxOnlyCountUnique);
        Set(4, _cbxIgnoreGiant, _cbxOnlyCountGiant);
        Set(5, _cbxIgnoreTitan, _cbxOnlyCountTitan);
        Set(6, _cbxIgnoreStrong, _cbxOnlyCountStrong);
        Set(7, _cbxIgnoreElite, _cbxOnlyCountElite);
        Set(8, _cbxIgnoreUnique2, _cbxOnlyCountUnique2);
        Set(16, _cbxIgnoreParty, _cbxOnlyCountParty);
        Set(17, _cbxIgnoreChampionParty, _cbxOnlyCountChampionParty);
        Set(20, _cbxIgnoreGiantParty, _cbxOnlyCountGiantParty);
    }

    private void SaveConfig()
    {
        if (_loading) return;
        Config.Set(PluginName, "EnableDeathRecovery", ScriptPanel.IsChecked(_cbxEnableDeathRecovery));
        Config.Set(PluginName, "AcceptFW", ScriptPanel.IsChecked(_cbxAcceptForgottenWorld));
        Config.Set(PluginName, "CharWhitelist", _charWhitelist.ToArray());
        Config.Set(PluginName, "IgnoreNames", _mobIgnoreNames.ToArray());
        Config.Set(PluginName, "IgnoreTypes", _ignoreTypes.ToArray());
        Config.Set(PluginName, "OnlyCountTypes", _onlyCountTypes.ToArray());
        Config.Save(PluginName);
    }

    /// <summary>
    /// Pushes the current ignore/prefer/only-attack lists into the Monsters API
    /// so the core targeting loop respects them natively.
    /// </summary>
    private void SyncMonstersApi()
    {
        // Clear previous state pushed by this plugin
        Monsters.ClearAll();

        // Ignored names
        foreach (var name in _mobIgnoreNames)
            Monsters.IgnoreName(name);

        // Ignored rarities
        foreach (var typeId in _ignoreTypes)
            Monsters.IgnoreRarity(typeId);

        // NOTE: _onlyCountTypes is intentionally NOT mapped to Monsters.OnlyAttackRarity().
        // "Only Count" controls which mobs count toward area-completion detection,
        // NOT which mobs the bot is allowed to target. The bot should still attack
        // any non-ignored monster; only the attackarea "area cleared" check uses _onlyCountTypes.
    }

    #endregion

    #region Packet Hooks

    private void HandleFWInvite(ushort opcode, byte[] data)
    {
        if (ScriptPanel.IsChecked(_cbxAcceptForgottenWorld))
        {
            Log.Info($"[{PluginName}] Auto-accepting Forgotten World invitation...");
            var builder = Packets.Build(0x751C);
            // Replicate python: packet = data[:4] + 00 00 00 00 + 01
            for (int i=0; i<4; i++) builder.WriteByte(data[i]);
            builder.WriteUInt(0);
            builder.WriteByte(1); // Accept
            builder.SendToServer();
        }
    }

    #endregion

    #region Death Recovery Logic

    public bool IsDeathRecoveryEnabledAndAllowed()
    {
        if (!ScriptPanel.IsChecked(_cbxEnableDeathRecovery)) return false;
        
        // If whitelist is empty, allow all. Otherwise, check if name is in list.
        if (_charWhitelist.Count == 0) return true;
        return _charWhitelist.Contains(Player.Name);
    }

    // Death Recovery state
    private bool _isDead = false;
    private long _lastWaitDropsTimer = 0;
    
    // We expose these to let the AttackArea override hook into it
    public AutoDungeonAttackAreaCommand CurrentAttackCmd { get; set; } = null;

    // Make handlers public so top-level code can wire them to ScriptGlobals events
    public void OnDeathHandler()
    {
        try
        {
            if (!_isDead)
            {
                _isDead = true;
                // Log.Info($"[{PluginName}] DEATH DETECTED.");
                if (CurrentAttackCmd != null && IsDeathRecoveryEnabledAndAllowed())
                {
                    Log.Info($"[{PluginName}] Script execution halted, waiting for resurrection...");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[{PluginName}] OnDeath error: {ex.Message}");
        }
    }

    public void OnRespawnHandler()
    {
        try
        {
            if (_isDead)
            {
                _isDead = false;
                // Log.Info($"[{PluginName}] RESURRECTION DETECTED.");
                
                if (CurrentAttackCmd != null && IsDeathRecoveryEnabledAndAllowed())
                {
                    Log.Info($"[{PluginName}] Restarting bot to continue AttackArea...");
                    
                    Task.Delay(3000).ContinueWith(_ => {
                        if (GameInfo.IsReady)
                            Bot.Start();
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[{PluginName}] OnRespawn error: {ex.Message}");
        }
    }

    public void OnTickHandler()
    {
        try
        {
            if (Player.HP == 0 && !_isDead)
                OnDeathHandler();
            else if (Player.HP > 0 && _isDead)
                OnRespawnHandler();
        }
        catch (Exception ex)
        {
            Log.Error($"[{PluginName}] OnTick error: {ex.Message}");
        }
    }

    #endregion

    #region Filtering

    private static string NormalizeMobName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        return string.Join(" ", name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private bool ContainsIgnoreMobName(string name)
    {
        var normalized = NormalizeMobName(name);
        return _mobIgnoreNames.Any(m => NormalizeMobName(m).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsIgnoredByName(string mobName)
    {
        var normalizedMobName = NormalizeMobName(mobName);
        if (string.IsNullOrEmpty(normalizedMobName))
            return false;

        foreach (var ignoreName in _mobIgnoreNames)
        {
            var normalizedIgnore = NormalizeMobName(ignoreName);
            if (string.IsNullOrEmpty(normalizedIgnore))
                continue;

            if (normalizedMobName.Equals(normalizedIgnore, StringComparison.OrdinalIgnoreCase) ||
                normalizedMobName.Contains(normalizedIgnore, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public bool ShouldAttackMob(vBot.Core.Objects.Spawn.SpawnedMonster mob)
    {
        if (mob == null || mob.State.LifeState != vBot.Core.Objects.LifeState.Alive) return false;

        // Ignore by Name
        var mName = mob.Record.GetRealName() ?? "";
        if (IsIgnoredByName(mName))
            return false;

        // Rarity matching
        int typeId = (int)mob.Rarity;

        // Ignore types
        if (_ignoreTypes.Contains(typeId))
            return false;

        // If OnlyCount is not empty, and this isn't in it, ignore
        if (_onlyCountTypes.Count > 0 && !_onlyCountTypes.Contains(typeId))
            return false;

        return true;
    }

    public bool ShouldAttackMob(MonsterInfo mob)
    {
        if (mob == null || !mob.IsAlive) return false;

        // Ignore by Name
        if (IsIgnoredByName(mob.Name))
            return false;

        // Rarity matching — uses MonsterInfo.RarityId (matches Silkroad TypeID values)
        int typeId = mob.RarityId;

        // Ignore types
        if (_ignoreTypes.Contains(typeId))
            return false;

        // If OnlyCount is not empty, and this isn't in it, ignore
        if (_onlyCountTypes.Count > 0 && !_onlyCountTypes.Contains(typeId))
            return false;

        return true;
    }
    
    #endregion
}

// --------------------------------------------------------------------------
// Custom Script Commands
// --------------------------------------------------------------------------

public class AutoDungeonAttackAreaCommand : IScriptCommand
{
    public string Name => "attackarea";
    public bool IsBusy { get; private set; }
    
    public Dictionary<string, string> Arguments => new()
    {
        { "X", "World X Offset" },
        { "Y", "World Y Offset" },
        { "Radius", "Attack Radius" }
    };

    private volatile bool _stopped = false;
    private readonly AutoDungeonPlugin _plugin;

    public AutoDungeonAttackAreaCommand(AutoDungeonPlugin plugin)
    {
        _plugin = plugin;
    }

    public bool Execute(string[] args)
    {
        float worldX = 0, worldY = 0;
        float? explicitZ = null;
        int explicitRegionId = 0;
        int radius = 50;

        if (args != null && args.Length >= 5)
        {
            if (!int.TryParse(args[0], out explicitRegionId) ||
                !float.TryParse(args[1], out worldX) ||
                !float.TryParse(args[2], out worldY) ||
                !float.TryParse(args[3], out var parsedZ) ||
                !int.TryParse(args[4], out radius))
            {
                Log.Warn($"[{Name}] Invalid parameters.");
                return false;
            }

            explicitZ = parsedZ;
        }
        else if (args != null && args.Length >= 3)
        {
            if (!float.TryParse(args[0], out worldX) ||
                !float.TryParse(args[1], out worldY) ||
                !int.TryParse(args[2], out radius))
            {
                Log.Warn($"[{Name}] Invalid parameters.");
                return false;
            }
        }
        else
        {
            var player = vBot.Core.Game.Player;
            if (player != null)
            {
                var pSource = player.Movement.Source;
                worldX = pSource.X;
                worldY = pSource.Y;
                radius = Bot.TrainingRadius > 0 ? Bot.TrainingRadius : 50;
            }
            else
            {
                Log.Warn($"[{Name}] Cannot determine current position.");
                return false;
            }
        }

        try
        {
            IsBusy = true;
            _stopped = false;
            _plugin.CurrentAttackCmd = this;

            // Convert world coordinates to region + offsets and set training area manually
            // (Bypassing Bot.SetTrainingPosition since it currently loses dungeon floor/Z context)
            var currentPlayer = vBot.Core.Game.Player;
            var playerPosition = currentPlayer?.Movement.Source ?? default;
            var contextRegion = explicitRegionId != 0
                ? (vBot.Core.Objects.Region)explicitRegionId
                : playerPosition.Region;
            var resolvedRegion = contextRegion.IsDungeon ? contextRegion : default;
            var position = new vBot.Core.Objects.Position(worldX, worldY, resolvedRegion);
            if (contextRegion.IsDungeon)
                position.ZOffset = explicitZ ?? playerPosition.ZOffset;
            else if (explicitZ.HasValue)
                position.ZOffset = explicitZ.Value;

            vBot.Core.PlayerConfig.Set("vBot.Area.Region", position.Region.Id);
            vBot.Core.PlayerConfig.Set("vBot.Area.X", position.XOffset);
            vBot.Core.PlayerConfig.Set("vBot.Area.Y", position.YOffset);
            vBot.Core.PlayerConfig.Set("vBot.Area.Z", position.ZOffset);
            vBot.Core.PlayerConfig.Set("vBot.Area.Radius", radius);
            vBot.Core.Event.EventManager.FireEvent("OnSetTrainingArea");
            var resolvedAttackArea = new vBot.Core.Objects.Area("AttackArea", position, radius);

            Log.Info($"[{Name}] Started Area: X:{worldX:0.0} Y:{worldY:0.0} R:{radius}");
            
            // Wait for movement
            while (!_stopped && Bot.IsRunning && ScriptManager.Running && !Player.IsDead)
            {
                 var player = vBot.Core.Game.Player;
                 if (player != null)
                 {
                     var pSource = player.Movement.Source;
                     if (IsInsideResolvedArea(resolvedAttackArea, pSource)) break;
                 }
                 Thread.Sleep(500);
            }

            if (_stopped)
                return false;

            // Signal MovementBundle that attacking is now allowed in this area
            vBot.Core.PlayerConfig.Set("vBot.Script.AttackAreaActive", true);

            int noMobGraceWaitCount = 0;
            const int graceCountMax = 10; // 5 seconds (500ms intervals)

            while (!_stopped && ScriptManager.Running)
            {
                // If bot stopped or dead and recovery allowed, wait.
                if (!Bot.IsRunning || Player.IsDead)
                {
                    if (_plugin.IsDeathRecoveryEnabledAndAllowed())
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    else
                    {
                        Log.Warn($"[{Name}] Player died / bot stopped. Aborting area (Death recovery disabled).");
                        break;
                    }
                }

                // Count specific mobs
                var allMobs = World.GetAliveMonsters();
                int validMobCount = 0;
                
                var player = vBot.Core.Game.Player;
                if (player != null)
                {
                    foreach(var m in allMobs)
                    {
                        var mSpawn = SpawnManager.GetEntity<SpawnedMonster>(m.UniqueId);
                        if (mSpawn != null && IsInsideResolvedArea(resolvedAttackArea, mSpawn.Movement.Source))
                        {
                            if (_plugin.ShouldAttackMob(m))
                            {
                                validMobCount++;
                            }
                        }
                    }
                }

                if (validMobCount > 0)
                {
                    noMobGraceWaitCount = 0;
                }
                else
                {
                    noMobGraceWaitCount++;
                    if (noMobGraceWaitCount >= graceCountMax)
                    {
                        // All mobs are dead! Wait for drops before completing.
                        Log.Info($"[{Name}] Valid mobs cleared. Waiting for drops...");
                        
                        WaitForLoot();
                        
                        Log.Info($"[{Name}] Area complete.");
                        break;
                    }
                }

                Thread.Sleep(500);
            }

            return !_stopped;
        }
        finally
        {
            IsBusy = false;
            _plugin.CurrentAttackCmd = null;
            vBot.Core.PlayerConfig.Set("vBot.Script.AttackAreaActive", false);
        }
    }

    private static bool IsInsideResolvedArea(vBot.Core.Objects.Area area, vBot.Core.Objects.Position position)
    {
        if (area.Position.Region.IsDungeon && position.Region.Id != area.Position.Region.Id)
            return false;

        return area.IsPositionInArea(position);
    }

    private void WaitForLoot()
    {
        // Max 10 seconds waiting
        int maxWait = 20; 
        
        while (maxWait > 0 && !_stopped && ScriptManager.Running)
        {
            var drops = World.GetDrops();
            bool hasPickableDrop = false;
            
            // Replicate condition logic from PickupManager (approximate)
            foreach (var drop in drops)
            {
                if (PickupManager.PickupEverything) { hasPickableDrop = true; break; }
                if (PickupManager.PickupGold && drop.IsGold) { hasPickableDrop = true; break; }
                
                // Compare with user Pick Filter
                if (PickupManager.PickupFilter.Any(p => p.CodeName == drop.Name)) // Simplified check (using Name vs CodeName depends on drop obj, but suffices usually or query item ref)
                {
                    hasPickableDrop = true; break;
                }
            }

            if (hasPickableDrop)
            {
                Thread.Sleep(500);
                maxWait--;
            }
            else
            {
                break;
            }
        }
    }

    public void Stop()
    {
        _stopped = true;
        vBot.Core.PlayerConfig.Set("vBot.Script.AttackAreaActive", false);
        IsBusy = false;
    }
}

public class AutoDungeonGoDimensionalCommand : IScriptCommand
{
    public string Name => "godimensional";
    public bool IsBusy { get; private set; }
    
    public Dictionary<string, string> Arguments => new()
    {
        { "Name", "Optional specific name of the dimensional hole item" }
    };

    private volatile bool _stopped = false;
    private readonly AutoDungeonPlugin _plugin;

    public AutoDungeonGoDimensionalCommand(AutoDungeonPlugin plugin)
    {
        _plugin = plugin;
    }

    public bool Execute(string[] args)
    {
        string specificName = args != null && args.Length > 0 ? string.Join(" ", args) : null;
        
        try
        {
            IsBusy = true;
            _stopped = false;
            
            Bot.Stop();
            
            // Search inventory
            ItemInfo dimensionalItem = null;
            if (string.IsNullOrWhiteSpace(specificName))
            {
                 // Find by Type ID (Dimensional Holes usually TID1=3, TID2=12, TID3=7 according to python script)
                 dimensionalItem = Inventory.FindItemByType(3, 12, 7);
            }
            else
            {
                 dimensionalItem = Inventory.FindItem(specificName);
            }

            if (dimensionalItem == null)
            {
                 Log.Warn($"[{Name}] Dimensional Hole not found in inventory!");
                 return false;
            }

            Log.Info($"[{Name}] Using {dimensionalItem.Name}...");
            
            // Using packets instead of generic use allows specifying locale fix
            var pBuilder = Packets.Build((ushort)0x704C);
            pBuilder.WriteByte((byte)dimensionalItem.Slot);
            
            var locale = GameInfo.LocaleId;
            if (locale == 56 || locale == 18 || locale == 61) // Same locale hack from Python
            {
                pBuilder.WriteBytes(new byte[] { 0x30, 0x0C, 0x0C, 0x07 });
            }
            else
            {
                pBuilder.WriteBytes(new byte[] { 0x6C, 0x3E });
            }
            
            pBuilder.SendToServer();
            
            // Because spawning the pillar takes time...
            Log.Info($"[{Name}] Waiting for pillar to spawn...");
            
            int attempts = 30; // 15 seconds max wait
            uint pillarUid = 0;
            
            while (attempts > 0 && !_stopped && ScriptManager.Running)
            {
                Thread.Sleep(500);
                
                // Find nearest Dimensional Pillar NPC
                var npcs = World.GetNPCs();
                var pillar = npcs.FirstOrDefault(n => n.CodeName.Contains("DIMENSIONAL")); // Approximation, adjust based on exact CodeName
                
                if (pillar != null)
                {
                    pillarUid = pillar.UniqueId;
                    break;
                }
                attempts--;
            }

            if (pillarUid == 0)
            {
                Log.Warn($"[{Name}] Pillar never spawned!");
                return false;
            }
            
            Log.Info($"[{Name}] Entering Dimensional Pillar...");
            
            // 0x7045 Select
            var selPkt = Packets.Build(0x7045);
            selPkt.WriteUInt(pillarUid);
            selPkt.SendToServer();
            
            Thread.Sleep(1000);
            
            // 0x704B Interact
            var intPkt = Packets.Build(0x704B);
            intPkt.WriteUInt(pillarUid);
            intPkt.SendToServer();
            
            // 0x705A Execute (Option 3 usually means enter)
            var execPkt = Packets.Build(0x705A);
            execPkt.WriteUInt(pillarUid);
            execPkt.WriteByte(3);
            execPkt.SendToServer();
            
            Thread.Sleep(5000);
            Bot.Start();
            
            return !_stopped;
        }
        finally
        {
             IsBusy = false;
        }
    }

    public void Stop()
    {
        _stopped = true;
        IsBusy = false;
    }
}

// Global script entry point required by Roslyn
try
{
    var plugin = new AutoDungeonPlugin();
    plugin.Initialize();

    // Register ScriptGlobals events at top level (only accessible here, not inside classes)
    OnDeath += plugin.OnDeathHandler;
    OnRespawn += plugin.OnRespawnHandler;
    OnTick += plugin.OnTickHandler;

    // Subscribe to the Config auto-reload signal. The script runs once at vBot startup,
    // before any character logs in, so the initial Config.Load reads the "default" file
    // (the per-character file is silently ignored). When the character finally loads,
    // the Config API re-reads the correct file and fires this event so we can refresh
    // local lists and panel checkboxes from the now-correct data.
    Action<string> onConfigReloaded = scriptName =>
    {
        if (string.Equals(scriptName, "AutoDungeon", StringComparison.Ordinal))
            plugin.ReloadFromConfig();
    };
    vBot.Core.Event.EventManager.SubscribeEvent("OnScriptConfigReloaded", onConfigReloaded);

    OnFinished += () => {
        try { vBot.Core.Event.EventManager.UnsubscribeEvent("OnScriptConfigReloaded", onConfigReloaded); } catch { }
        plugin.Shutdown();
    };
}
catch (Exception ex)
{
    Log.Error($"[AutoDungeon] CSX LOAD FAILED: {ex.GetType().Name}: {ex.Message}");
}
