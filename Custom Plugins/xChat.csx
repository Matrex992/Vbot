// =============================================================
// xChat - Chat Spammer + Chat Logger for vBot
// =============================================================
// Features:
//   1. Chat Spammer  - send a message to any channel on a timer
//   2. Chat Logger   - log selected chat types / game events to file
// =============================================================
// Version: 1.0.0
// =============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;

// ═══════════════════════════════════════════════════════════
// CONFIGURATION
// ═══════════════════════════════════════════════════════════

var pluginName    = "xChat";
var pluginVersion = "1.0.0";

// Spam state
var spamEnabled     = false;
var spamCounter     = 0;
var spamNextFireAt  = DateTime.MinValue;

// Config load guard (prevents CheckedChanged from saving mid-load)
var _loading = false;

// ═══════════════════════════════════════════════════════════
// GUI SETUP
// ═══════════════════════════════════════════════════════════

var panel = GUI.CreatePanel(pluginName, "Chat spammer + logger");

// ── Stop & dispose previous reload ──
if (panel.Container.Tag is Action _prevCleanup)
{
    try { _prevCleanup(); } catch { }
    panel.Container.Tag = null;
}

// UI control references (populated inside GUI.Invoke)
CheckBox  chkSpam        = null;
TextBox   txtSpamMsg     = null;
ComboBox  cmbChannel     = null;
NumericUpDown nudInterval = null;
Label     lblCounter     = null;

CheckBox chkLogAll       = null;
CheckBox chkLogPrivate   = null;
CheckBox chkLogParty     = null;
CheckBox chkLogGuild     = null;
CheckBox chkLogUnion     = null;
CheckBox chkLogAcademy   = null;
CheckBox chkLogStall     = null;
CheckBox chkLogGlobal    = null;
CheckBox chkLogNotice    = null;
CheckBox chkLogGM        = null;
CheckBox chkLogOther     = null;

CheckBox chkEvtUniqueSpawn   = null;
CheckBox chkEvtCharDied      = null;
CheckBox chkEvtCharAttacked  = null;
CheckBox chkEvtPetDied       = null;
CheckBox chkEvtRareDrop      = null;
CheckBox chkEvtItemDrop      = null;
CheckBox chkEvtHunterSpawn   = null;
CheckBox chkEvtThiefSpawn    = null;

CheckBox chkLogSingleFile    = null;
Label    lblLogPath          = null;

GUI.Invoke(() =>
{
    foreach (Control _c in panel.Container.Controls)
        _c.Dispose();
    panel.Container.Controls.Clear();
    panel.Container.BackColor = Color.WhiteSmoke;

    // ══════════════════════════════════════════════════════
    // 1. SPAM GROUP
    // ══════════════════════════════════════════════════════
    var gbSpam = new GroupBox();
    gbSpam.Text = "Chat Spammer";
    gbSpam.Location = new Point(10, 10);
    gbSpam.Size = new Size(760, 85);
    gbSpam.Font = new Font("Segoe UI", 9f);
    panel.Container.Controls.Add(gbSpam);

    // Enable checkbox
    chkSpam = new CheckBox();
    chkSpam.Text = "Enable";
    chkSpam.Location = new Point(10, 22);
    chkSpam.AutoSize = true;
    gbSpam.Controls.Add(chkSpam);

    // Channel label + combo
    var lblChan = new Label();
    lblChan.Text = "Channel:";
    lblChan.Location = new Point(80, 23);
    lblChan.AutoSize = true;
    gbSpam.Controls.Add(lblChan);

    cmbChannel = new ComboBox();
    cmbChannel.DropDownStyle = ComboBoxStyle.DropDownList;
    cmbChannel.Items.AddRange(new object[] { "All", "Party", "Guild", "Union", "Academy", "Stall", "Global" });
    cmbChannel.SelectedIndex = 0;
    cmbChannel.Location = new Point(140, 20);
    cmbChannel.Size = new Size(90, 22);
    gbSpam.Controls.Add(cmbChannel);

    // Interval label + spinner
    var lblInterval = new Label();
    lblInterval.Text = "Interval (s):";
    lblInterval.Location = new Point(242, 23);
    lblInterval.AutoSize = true;
    gbSpam.Controls.Add(lblInterval);

    nudInterval = new NumericUpDown();
    nudInterval.Minimum = 1;
    nudInterval.Maximum = 3600;
    nudInterval.Value = 10; // default 10 s
    nudInterval.Location = new Point(325, 20);
    nudInterval.Size = new Size(60, 22);
    gbSpam.Controls.Add(nudInterval);

    // Message input
    var lblMsg = new Label();
    lblMsg.Text = "Message:";
    lblMsg.Location = new Point(10, 54);
    lblMsg.AutoSize = true;
    gbSpam.Controls.Add(lblMsg);

    txtSpamMsg = new TextBox();
    txtSpamMsg.Location = new Point(70, 51);
    txtSpamMsg.Size = new Size(580, 22);
    txtSpamMsg.PlaceholderText = "Type message to spam...";
    gbSpam.Controls.Add(txtSpamMsg);

    // Spam counter label
    lblCounter = new Label();
    lblCounter.Text = "Sent: 0";
    lblCounter.Location = new Point(660, 54);
    lblCounter.AutoSize = true;
    lblCounter.ForeColor = Color.DimGray;
    gbSpam.Controls.Add(lblCounter);

    // ══════════════════════════════════════════════════════
    // 2. CHAT LOG GROUP
    // ══════════════════════════════════════════════════════
    var gbLog = new GroupBox();
    gbLog.Text = "Chat Logger — select types to log";
    gbLog.Location = new Point(10, 100);
    gbLog.Size = new Size(370, 136);
    gbLog.Font = new Font("Segoe UI", 9f);
    panel.Container.Controls.Add(gbLog);

    int ly = 22, lx1 = 10, lx2 = 125, lx3 = 250, lStep = 22;

    // Column 1
    chkLogAll     = AddCheck(gbLog, "All chat",   lx1, ly);
    chkLogPrivate = AddCheck(gbLog, "Private",    lx1, ly + lStep);
    chkLogParty   = AddCheck(gbLog, "Party",      lx1, ly + lStep * 2);
    chkLogGuild   = AddCheck(gbLog, "Guild",      lx1, ly + lStep * 3);
    chkLogUnion   = AddCheck(gbLog, "Union",      lx1, ly + lStep * 4);

    // Column 2
    chkLogStall   = AddCheck(gbLog, "Stall",      lx2, ly);
    chkLogGlobal  = AddCheck(gbLog, "Global",     lx2, ly + lStep);
    chkLogNotice  = AddCheck(gbLog, "Notice",     lx2, ly + lStep * 2);
    chkLogGM      = AddCheck(gbLog, "GM",         lx2, ly + lStep * 3);
    chkLogAcademy = AddCheck(gbLog, "Academy",    lx2, ly + lStep * 4);

    // Column 3
    chkLogOther   = AddCheck(gbLog, "All others", lx3, ly);

    // ══════════════════════════════════════════════════════
    // 3. EVENT LOG GROUP
    // ══════════════════════════════════════════════════════
    var gbEvt = new GroupBox();
    gbEvt.Text = "Event Logger — select events to log";
    gbEvt.Location = new Point(390, 100);
    gbEvt.Size = new Size(380, 118);
    gbEvt.Font = new Font("Segoe UI", 9f);
    panel.Container.Controls.Add(gbEvt);

    int ex1 = 10, ex2 = 195;

    // Column 1
    chkEvtUniqueSpawn  = AddCheck(gbEvt, "Unique monster spawn",  ex1, 22);
    chkEvtCharAttacked = AddCheck(gbEvt, "Character attacked",    ex1, 44);
    chkEvtCharDied     = AddCheck(gbEvt, "Character died",        ex1, 66);
    chkEvtPetDied      = AddCheck(gbEvt, "Pet / Transport died",  ex1, 88);

    // Column 2
    chkEvtRareDrop     = AddCheck(gbEvt, "Rare item pick",        ex2, 22);
    chkEvtItemDrop     = AddCheck(gbEvt, "Normal item pick",      ex2, 44);
    chkEvtHunterSpawn  = AddCheck(gbEvt, "Hunter / Trader spawn", ex2, 66);
    chkEvtThiefSpawn   = AddCheck(gbEvt, "Thief spawn",           ex2, 88);

    // ══════════════════════════════════════════════════════
    // 4. LOG FILE OPTIONS
    // ══════════════════════════════════════════════════════
    var gbFile = new GroupBox();
    gbFile.Text = "Log File Options";
    gbFile.Location = new Point(10, 246);
    gbFile.Size = new Size(760, 60);
    gbFile.Font = new Font("Segoe UI", 9f);
    panel.Container.Controls.Add(gbFile);

    chkLogSingleFile = new CheckBox();
    chkLogSingleFile.Text = "Save all logs to one file (xChat_log.txt)";
    chkLogSingleFile.Location = new Point(10, 22);
    chkLogSingleFile.AutoSize = true;
    gbFile.Controls.Add(chkLogSingleFile);

    lblLogPath = new Label();
    lblLogPath.Text = "Log path: " + GetLogDir();
    lblLogPath.Location = new Point(10, 40);
    lblLogPath.AutoSize = true;
    lblLogPath.ForeColor = Color.DimGray;
    gbFile.Controls.Add(lblLogPath);

    // ── Event: enable/disable spam + apply new interval
    chkSpam.CheckedChanged += (s, e) =>
    {
        spamEnabled = chkSpam.Checked;

        if (spamEnabled)
        {
            var _intervalMs = (int)((nudInterval?.Value ?? 10m) * 1000);
            spamNextFireAt = DateTime.Now.AddMilliseconds(_intervalMs);
            spamCounter = 0;
            lblCounter.Text = "Sent: 0";
            Log.Info($"[{pluginName}] Spam started — channel: {cmbChannel.SelectedItem}, interval: {nudInterval.Value}s");
        }
        else
        {
            Log.Info($"[{pluginName}] Spam stopped");
        }
    };

    // Auto-save checkbox state on change
    chkLogAll.CheckedChanged     += (s, e) => SaveConfig();
    chkLogPrivate.CheckedChanged += (s, e) => SaveConfig();
    chkLogParty.CheckedChanged   += (s, e) => SaveConfig();
    chkLogGuild.CheckedChanged   += (s, e) => SaveConfig();
    chkLogUnion.CheckedChanged   += (s, e) => SaveConfig();
    chkLogAcademy.CheckedChanged += (s, e) => SaveConfig();
    chkLogStall.CheckedChanged   += (s, e) => SaveConfig();
    chkLogGlobal.CheckedChanged  += (s, e) => SaveConfig();
    chkLogNotice.CheckedChanged  += (s, e) => SaveConfig();
    chkLogGM.CheckedChanged      += (s, e) => SaveConfig();
    chkLogOther.CheckedChanged   += (s, e) => SaveConfig();
    chkEvtUniqueSpawn.CheckedChanged  += (s, e) => SaveConfig();
    chkEvtCharAttacked.CheckedChanged += (s, e) => SaveConfig();
    chkEvtCharDied.CheckedChanged     += (s, e) => SaveConfig();
    chkEvtPetDied.CheckedChanged      += (s, e) => SaveConfig();
    chkEvtRareDrop.CheckedChanged     += (s, e) => SaveConfig();
    chkEvtItemDrop.CheckedChanged     += (s, e) => SaveConfig();
    chkEvtHunterSpawn.CheckedChanged  += (s, e) => SaveConfig();
    chkEvtThiefSpawn.CheckedChanged   += (s, e) => SaveConfig();
    chkLogSingleFile.CheckedChanged   += (s, e) => SaveConfig();
    cmbChannel.SelectedIndexChanged   += (s, e) => SaveConfig();
    nudInterval.ValueChanged          += (s, e) => SaveConfig();
    txtSpamMsg.TextChanged            += (s, e) => SaveConfig();

    // Store VisibleChanged handler for cleanup
    EventHandler _visibleHandler = null;
    _visibleHandler = (s, e) =>
    {
        if (panel.Container.Visible)
            LoadConfig();
    };
    panel.Container.VisibleChanged += _visibleHandler;

    panel.Container.Tag = (Action)(() =>
    {
        panel.Container.VisibleChanged -= _visibleHandler;
    });
});

// ═══════════════════════════════════════════════════════════
// INITIALIZATION
// ═══════════════════════════════════════════════════════════

LoadConfig();

// ═══════════════════════════════════════════════════════════
// SPAM TICK TIMER (500 ms tick, same as OnTick)
// ═══════════════════════════════════════════════════════════

// Pet state tracking (for tick-based death detection)
bool _hadGrowthPet    = false;
bool _hadFellowPet    = false;
bool _hadAbilityPet   = false;
bool _hadVehicle      = false;
bool _hadJobTransport = false;

// Inventory snapshot for pickup detection (item name → total amount)
var _inventorySnapshot = new Dictionary<string, int>(); // player inventory
var _groundItems       = new Dictionary<uint, string>(); // uniqueId → name (items on the ground)
var _pendingPickup     = new List<(string Name, DateTime When)>(); // left ground, awaiting inventory confirm

// Helper: safely snapshot player inventory
Dictionary<string, int> SnapPlayer() =>
    (Inventory.GetAllItems() ?? new List<ItemInfo>())
    .Where(i => !string.IsNullOrEmpty(i.Name))
    .GroupBy(i => i.Name)
    .ToDictionary(g => g.Key, g => g.Sum(i => i.Amount));

Action _onTickHandler                              = null;
Action<int, string, string> _onChatHandler          = null;
Action _onDeathHandler                             = null;
Action<string, uint> _onPlayerAttackHandler         = null;
Action<uint, string, int, int, bool> _onEntitySpawnHandler = null;
Action<string, int, bool> _onItemPickupHandler      = null;
Action _onInventoryUpdateHandler                   = null;
Action<string> _onPetDiedHandler                   = null;
Action<string, int> _onUniqueSpawnHandler           = null;
Action<uint> _onEntityDespawnHandler               = null;

_onTickHandler = () =>
{
    // Pet death detection (polling — more reliable than OnPetDied on private servers)
    if (chkEvtPetDied?.Checked == true)
    {
        bool hasGrowth    = Pets.HasGrowthPet;
        bool hasFellow    = Pets.HasFellowPet;
        bool hasAbility   = Pets.HasAbilityPet;
        bool hasVehicle   = Pets.HasVehicle;
        bool hasJobTrans  = Pets.GetJobTransport() != null;

        if (_hadGrowthPet    && !hasGrowth)   WriteEventLog("[Pet]: Growth pet died");
        if (_hadFellowPet    && !hasFellow)   WriteEventLog("[Pet]: Fellow pet died");
        if (_hadAbilityPet   && !hasAbility)  WriteEventLog("[Pet]: Ability pet died");
        if (_hadVehicle      && !hasVehicle)  WriteEventLog("[Pet]: Vehicle/Transport died");
        if (_hadJobTransport && !hasJobTrans) WriteEventLog("[Pet]: Job transport died");

        _hadGrowthPet    = hasGrowth;
        _hadFellowPet    = hasFellow;
        _hadAbilityPet   = hasAbility;
        _hadVehicle      = hasVehicle;
        _hadJobTransport = hasJobTrans;
    }

    // Pending pickup timeout — items that left the ground but never entered player inventory
    // after 600 ms means the pick pet kept them (no auto-transfer to player)
    if (chkEvtItemDrop?.Checked == true && _pendingPickup.Count > 0)
    {
        var now = DateTime.Now;
        var expired = _pendingPickup.Where(e => (now - e.When).TotalMilliseconds >= 600).ToList();
        foreach (var entry in expired)
        {
            _pendingPickup.Remove(entry);
            WriteEventLog($"[Pick]: {entry.Name}");
        }
    }

    // Spam
    if (!spamEnabled) return;
    if (DateTime.Now < spamNextFireAt) return;

    var intervalMs = (int)((nudInterval?.Value ?? 10m) * 1000);
    spamNextFireAt = DateTime.Now.AddMilliseconds(intervalMs);

    var msg = txtSpamMsg?.Text?.Trim() ?? "";
    if (string.IsNullOrWhiteSpace(msg)) return;

    var channel = cmbChannel?.SelectedItem?.ToString() ?? "All";
    var sent = channel switch
    {
        "All"     => Chat.All(msg),
        "Party"   => Chat.Party(msg),
        "Guild"   => Chat.Guild(msg),
        "Union"   => Chat.Union(msg),
        "Academy" => Chat.Academy(msg),
        "Stall"   => Chat.Stall(msg),
        "Global"  => Chat.Global(msg),
        _         => Chat.All(msg)
    };

    if (sent)
    {
        spamCounter++;
        GUI.BeginInvoke(() =>
        {
            if (lblCounter != null)
                lblCounter.Text = $"Sent: {spamCounter}";
        });
    }
};
OnTick += _onTickHandler;

// ═══════════════════════════════════════════════════════════
// CHAT MESSAGE HANDLER
// ═══════════════════════════════════════════════════════════

_onChatHandler = (type, sender, message) =>
{
    // type: 1=All, 2=Private, 3=GM, 4=Party, 5=Guild, 6=Global,
    //       7=Notice, 9=Stall, 11=Union, 16=Academy

    var prefix = type switch
    {
        1  => chkLogAll?.Checked     == true ? "[All]"     : null,
        2  => chkLogPrivate?.Checked == true ? "[Private]" : null,
        3  => chkLogGM?.Checked      == true ? "[GM]"      : null,
        4  => chkLogParty?.Checked   == true ? "[Party]"   : null,
        5  => chkLogGuild?.Checked   == true ? "[Guild]"   : null,
        6  => chkLogGlobal?.Checked  == true ? "[Global]"  : null,
        7  => chkLogNotice?.Checked  == true ? "[Notice]"  : null,
        9  => chkLogStall?.Checked   == true ? "[Stall]"   : null,
        11 => chkLogUnion?.Checked   == true ? "[Union]"   : null,
        16 => chkLogAcademy?.Checked == true ? "[Academy]" : null,
        _  => chkLogOther?.Checked   == true ? $"[Type{type}]" : null
    };

    if (prefix == null) return;

    var senderPart = string.IsNullOrWhiteSpace(sender) ? "" : $"{sender}: ";
    WriteLog($"{prefix}{senderPart}{message}");
};
OnChatMessage += _onChatHandler;

// ═══════════════════════════════════════════════════════════
// GAME EVENT HANDLERS
// ═══════════════════════════════════════════════════════════

_onDeathHandler = () =>
{
    if (chkEvtCharDied?.Checked == true)
        WriteEventLog($"Character died: {Player.Name ?? "unknown"}");
};
OnDeath += _onDeathHandler;

_onPlayerAttackHandler = (name, id) =>
{
    if (chkEvtCharAttacked?.Checked == true)
        WriteEventLog($"Attacked by player: {name}");
};
OnPlayerAttack += _onPlayerAttackHandler;

_onEntitySpawnHandler = (uniqueId, name, entityType, jobType, isJobActive) =>
{
    // Player spawn — only log when actively wearing a job suit
    // jobType: 0=None, 1=Hunter, 2=Trader, 3=Thief
    if (entityType == 0 && isJobActive)
    {
        if ((jobType == 1 || jobType == 2) && chkEvtHunterSpawn?.Checked == true)
            WriteEventLog($"[JOB][{(jobType == 1 ? "Hunter" : "Trader")}]: {name}");
        else if (jobType == 3 && chkEvtThiefSpawn?.Checked == true)
            WriteEventLog($"[JOB][Thief]: {name}");
    }

    // Track item drops so we can detect when they are picked up
    if (entityType == 3 && !string.IsNullOrEmpty(name))
        _groundItems[uniqueId] = name;
};
OnEntitySpawn += _onEntitySpawnHandler;

// Item left the ground — add to pending queue; confirmed by OnInventoryUpdate or OnTick fallback
_onEntityDespawnHandler = (uniqueId) =>
{
    if (!_groundItems.TryGetValue(uniqueId, out var itemName)) return;
    _groundItems.Remove(uniqueId);
    if (itemName == "Gold") return; // skip gold — always auto-picked, not worth logging
    if (chkEvtItemDrop?.Checked == true || chkEvtRareDrop?.Checked == true)
        _pendingPickup.Add((itemName, DateTime.Now));
};
OnEntityDespawn += _onEntityDespawnHandler;

_onUniqueSpawnHandler = (name, modelId) =>
{
    if (chkEvtUniqueSpawn?.Checked == true)
        WriteEventLog($"[Spawn][Unique]: {name}");
};
OnUniqueSpawn += _onUniqueSpawnHandler;

// Initialize player inventory snapshot at load time
_inventorySnapshot = SnapPlayer();

// OnInventoryUpdate — player gained item; log it only if it came from the ground (pending queue)
// Manual pet→player moves are NOT in the pending queue — they are silently ignored
_onInventoryUpdateHandler = () =>
{
    if (chkEvtItemDrop?.Checked != true && chkEvtRareDrop?.Checked != true) return;

    var playerCurrent = SnapPlayer();

    foreach (var kvp in playerCurrent)
    {
        int prev   = _inventorySnapshot.TryGetValue(kvp.Key, out var p) ? p : 0;
        int gained = kvp.Value - prev;
        if (gained <= 0) continue;

        // Confirm this item came from the ground (in pending queue)
        int idx = _pendingPickup.FindIndex(e => e.Name == kvp.Key);
        if (idx < 0) continue; // not from ground — skip (e.g. manual pet→player move)

        _pendingPickup.RemoveAt(idx); // mark as handled so OnTick doesn't double-log
        if (chkEvtItemDrop?.Checked == true)
            WriteEventLog($"[Pick]: {kvp.Key} x{gained}");
    }

    _inventorySnapshot = playerCurrent;
};
OnInventoryUpdate += _onInventoryUpdateHandler;

// OnItemPickup — rare-flag override (may not fire on all private servers)
_onItemPickupHandler = (name, amount, isRare) =>
{
    if (isRare && chkEvtRareDrop?.Checked == true)
        WriteEventLog($"[Pick][Rare]: {name} x{amount}");
    // Normal items are already handled by OnInventoryUpdate above
};
OnItemPickup += _onItemPickupHandler;

// Pet death is detected via OnTick polling above (OnPetDied unreliable on private servers)

// ── Build full cleanup action ──
var _visCleanup = panel.Container.Tag as Action;
panel.Container.Tag = (Action)(() =>
{
    try { OnTick             -= _onTickHandler;         } catch { }
    try { OnChatMessage      -= _onChatHandler;         } catch { }
    try { OnDeath            -= _onDeathHandler;        } catch { }
    try { OnPlayerAttack     -= _onPlayerAttackHandler; } catch { }
    try { OnEntitySpawn        -= _onEntitySpawnHandler;       } catch { }
    try { OnEntityDespawn      -= _onEntityDespawnHandler;     } catch { }
    try { OnUniqueSpawn        -= _onUniqueSpawnHandler;       } catch { }
    try { OnItemPickup         -= _onItemPickupHandler;        } catch { }
    try { OnInventoryUpdate    -= _onInventoryUpdateHandler;   } catch { }
    try { OnPetDied          -= _onPetDiedHandler;      } catch { }
    try { _visCleanup?.Invoke();                        } catch { }
    spamEnabled = false;
    Log.Info($"[{pluginName}] Cleaned up");
});

OnFinished += () =>
{
    if (panel.Container.Tag is Action _cleanup)
    {
        try { _cleanup(); } catch { }
        panel.Container.Tag = null;
    }
    Log.Info($"[{pluginName}] Unloaded");
};

Log.Info($"[{pluginName}] v{pluginVersion} loaded!");

// ═══════════════════════════════════════════════════════════
// HELPERS
// ═══════════════════════════════════════════════════════════

CheckBox AddCheck(GroupBox parent, string text, int x, int y)
{
    var cb = new CheckBox();
    cb.Text = text;
    cb.Location = new Point(x, y);
    cb.AutoSize = true;
    parent.Controls.Add(cb);
    return cb;
}

string GetLogDir()
{
    var dir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "User", "xChat");
    if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);
    return dir;
}

string GetLogFilePath()
{
    if (chkLogSingleFile?.Checked == true)
        return Path.Combine(GetLogDir(), "xChat_log.txt");

    var charName = Player.Name ?? "unknown";
    var invalid  = Path.GetInvalidFileNameChars();
    var safe     = new string(charName.Where(c => !invalid.Contains(c)).ToArray());
    return Path.Combine(GetLogDir(), $"xChat_{safe}.txt");
}

void WriteLog(string line)
{
    try
    {
        var ts       = DateTime.Now.ToString("dd/MM HH:mm:ss");
        var filePath = GetLogFilePath();
        File.AppendAllText(filePath, $"{ts} > {line}{Environment.NewLine}");
    }
    catch { }
}

void WriteEventLog(string line)
{
    WriteLog("[EVENT] " + line);
}

void SaveConfig()
{
    if (_loading) return;
    try
    {
        Config.Load(pluginName);
        Config.Set(pluginName, "LogAll",         (chkLogAll?.Checked     == true).ToString());
        Config.Set(pluginName, "LogPrivate",      (chkLogPrivate?.Checked == true).ToString());
        Config.Set(pluginName, "LogParty",        (chkLogParty?.Checked   == true).ToString());
        Config.Set(pluginName, "LogGuild",        (chkLogGuild?.Checked   == true).ToString());
        Config.Set(pluginName, "LogUnion",        (chkLogUnion?.Checked   == true).ToString());
        Config.Set(pluginName, "LogAcademy",      (chkLogAcademy?.Checked == true).ToString());
        Config.Set(pluginName, "LogStall",        (chkLogStall?.Checked   == true).ToString());
        Config.Set(pluginName, "LogGlobal",       (chkLogGlobal?.Checked  == true).ToString());
        Config.Set(pluginName, "LogNotice",       (chkLogNotice?.Checked  == true).ToString());
        Config.Set(pluginName, "LogGM",           (chkLogGM?.Checked      == true).ToString());
        Config.Set(pluginName, "LogOther",        (chkLogOther?.Checked   == true).ToString());
        Config.Set(pluginName, "EvtUniqueSpawn",  (chkEvtUniqueSpawn?.Checked  == true).ToString());
        Config.Set(pluginName, "EvtCharAttacked", (chkEvtCharAttacked?.Checked == true).ToString());
        Config.Set(pluginName, "EvtCharDied",     (chkEvtCharDied?.Checked     == true).ToString());
        Config.Set(pluginName, "EvtPetDied",      (chkEvtPetDied?.Checked      == true).ToString());
        Config.Set(pluginName, "EvtRareDrop",     (chkEvtRareDrop?.Checked     == true).ToString());
        Config.Set(pluginName, "EvtItemDrop",     (chkEvtItemDrop?.Checked     == true).ToString());
        Config.Set(pluginName, "EvtHunterSpawn",  (chkEvtHunterSpawn?.Checked  == true).ToString());
        Config.Set(pluginName, "EvtThiefSpawn",   (chkEvtThiefSpawn?.Checked   == true).ToString());
        Config.Set(pluginName, "LogSingleFile",   (chkLogSingleFile?.Checked   == true).ToString());
        Config.Set(pluginName, "SpamChannel",     cmbChannel?.SelectedItem?.ToString() ?? "All");
        Config.Set(pluginName, "SpamInterval",    (nudInterval?.Value ?? 10m).ToString());
        Config.Set(pluginName, "SpamMessage",     txtSpamMsg?.Text ?? "");
        Config.Save(pluginName);
    }
    catch { }
}

void LoadConfig()
{
    try
    {
        Config.Load(pluginName);

        bool GetBool(string key) =>
            bool.TryParse(Config.Get(pluginName, key, "false"), out var v) && v;

        _loading = true;
        GUI.BeginInvoke(() =>
        {
            try
            {
                if (chkLogAll     != null) chkLogAll.Checked     = GetBool("LogAll");
                if (chkLogPrivate != null) chkLogPrivate.Checked = GetBool("LogPrivate");
                if (chkLogParty   != null) chkLogParty.Checked   = GetBool("LogParty");
                if (chkLogGuild   != null) chkLogGuild.Checked   = GetBool("LogGuild");
                if (chkLogUnion   != null) chkLogUnion.Checked   = GetBool("LogUnion");
                if (chkLogAcademy != null) chkLogAcademy.Checked = GetBool("LogAcademy");
                if (chkLogStall   != null) chkLogStall.Checked   = GetBool("LogStall");
                if (chkLogGlobal  != null) chkLogGlobal.Checked  = GetBool("LogGlobal");
                if (chkLogNotice  != null) chkLogNotice.Checked  = GetBool("LogNotice");
                if (chkLogGM      != null) chkLogGM.Checked      = GetBool("LogGM");
                if (chkLogOther   != null) chkLogOther.Checked   = GetBool("LogOther");
                if (chkEvtUniqueSpawn  != null) chkEvtUniqueSpawn.Checked  = GetBool("EvtUniqueSpawn");
                if (chkEvtCharAttacked != null) chkEvtCharAttacked.Checked = GetBool("EvtCharAttacked");
                if (chkEvtCharDied     != null) chkEvtCharDied.Checked     = GetBool("EvtCharDied");
                if (chkEvtPetDied      != null) chkEvtPetDied.Checked      = GetBool("EvtPetDied");
                if (chkEvtRareDrop     != null) chkEvtRareDrop.Checked     = GetBool("EvtRareDrop");
                if (chkEvtItemDrop     != null) chkEvtItemDrop.Checked     = GetBool("EvtItemDrop");
                if (chkEvtHunterSpawn  != null) chkEvtHunterSpawn.Checked  = GetBool("EvtHunterSpawn");
                if (chkEvtThiefSpawn   != null) chkEvtThiefSpawn.Checked   = GetBool("EvtThiefSpawn");
                if (chkLogSingleFile   != null) chkLogSingleFile.Checked   = GetBool("LogSingleFile");

                // Spam settings (Enable always starts unchecked)
                var savedChannel = Config.Get(pluginName, "SpamChannel", "All");
                if (cmbChannel != null)
                {
                    var idx = cmbChannel.Items.IndexOf(savedChannel);
                    cmbChannel.SelectedIndex = idx >= 0 ? idx : 0;
                }
                if (nudInterval != null)
                {
                    if (decimal.TryParse(Config.Get(pluginName, "SpamInterval", "10"), out var iv))
                        nudInterval.Value = Math.Max(nudInterval.Minimum, Math.Min(nudInterval.Maximum, iv));
                }
                if (txtSpamMsg != null)
                    txtSpamMsg.Text = Config.Get(pluginName, "SpamMessage", "");
            }
            finally
            {
                _loading = false;
            }
        });
    }
    catch { }
}
