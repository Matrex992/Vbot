// =============================================================
// xControl - Party Control Plugin for vBot
// =============================================================
// Control your party members using in-game chat commands.
// Leaders write commands and party members will execute them.
// =============================================================
// Version: 1.0.0
// =============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

// ═══════════════════════════════════════════════════════════
// CONFIGURATION
// ═══════════════════════════════════════════════════════════

var leaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

// Follow settings
var followEnabled = false;
var followPlayer = "";
var followDistance = 10.0;

// Plugin info
var pluginName = "xControl";
var pluginVersion = "1.0.0";
var allowUnreadType0Commands = false;

var scheduledTimers = new List<System.Threading.Timer>();
var extraZoneAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["donwhang"] = new[] { "dunhuang" },
    ["dunhuang"] = new[] { "donwhang" },
    ["alexandria"] = new[] { "alexandria north", "alexandria south" },
    ["stormclouddesert"] = new[] { "stormandclouddesert" },
    ["stormandclouddesert"] = new[] { "stormclouddesert" },
    ["grassland"] = new[] { "grasslandroad" },
    ["grasslandroad"] = new[] { "grassland" }
};

Log.Info($"[{pluginName}] loaded successfully");

// ═══════════════════════════════════════════════════════════
// GUI SETUP
// ═══════════════════════════════════════════════════════════

// Direct reference to avoid "using" conflicts if not available
var panel = GUI.CreatePanel(pluginName, "Control your party using in-game chat");

// Global UI references for helpers
TextBox leaderInput = null;
ListBox leaderList = null;

GUI.Invoke(() => 
{
    panel.Container.BackColor = Color.WhiteSmoke;
    panel.Container.AutoSize = true;
    panel.Container.AutoSizeMode = AutoSizeMode.GrowAndShrink;

    // 1. Leader Configuration Group
    var gbLeaders = new GroupBox();
    gbLeaders.Text = "";
    gbLeaders.Location = new Point(10, 10);
    gbLeaders.Size = new Size(220, 280);
    gbLeaders.Font = new Font("Segoe UI", 9f);
    panel.Container.Controls.Add(gbLeaders);

    leaderInput = new TextBox();
    leaderInput.Location = new Point(10, 20);
    leaderInput.Size = new Size(120, 23);
    gbLeaders.Controls.Add(leaderInput);

    var btnAdd = new Button();
    btnAdd.Text = "Add";
    btnAdd.Location = new Point(135, 19);
    btnAdd.Size = new Size(75, 25);
    btnAdd.UseVisualStyleBackColor = true;
    gbLeaders.Controls.Add(btnAdd);

    leaderList = new ListBox();
    leaderList.Location = new Point(10, 50);
    leaderList.Size = new Size(200, 185);
    gbLeaders.Controls.Add(leaderList);

    var btnRemove = new Button();
    btnRemove.Text = "Remove Selected";
    btnRemove.Location = new Point(10, 245);
    btnRemove.Size = new Size(200, 25);
    btnRemove.UseVisualStyleBackColor = true;
    gbLeaders.Controls.Add(btnRemove);

    // 2. Command Reference Group
    var gbCommands = new GroupBox();
    gbCommands.Text = "";
    gbCommands.Location = new Point(240, 10);
    gbCommands.Size = new Size(530, 300);
    gbCommands.Font = new Font("Segoe UI", 9f);
    panel.Container.Controls.Add(gbCommands);

    var rightText =
        "- S / START : Start bot\r\n" +
        "- SS / STOP : Stop bot\r\n" +
        "- T / TRACE #Player? : Trace to leader or player\r\n" +
        "- N / NOTRACE : Stop trace\r\n" +
        "- F / FOLLOW #Player? #Dist? : Follow player\r\n" +
        "- NF / NOFOLLOW : Stop following\r\n" +
        "- RE / RETURN : Use Return Scroll\r\n" +
        "- RECALL #Town : Set recall on nearby gate\r\n" +
        "- Q1 / Q2 / Q3 : Teleport Nth dest of nearest NPC\r\n" +
        "- TP #Source,#Dest : Teleport via NPC name\r\n" +
        "- LQ : Kings Valley -> Pharaoh tomb\r\n" +
        "- REVERSE return|death|zone #Name?\r\n" +
        "- Z / ZERK : Berserk mode\r\n" +
        "- G / GETOUT : Leave party\r\n" +
        "- DIS / DC : Disconnect";

    var leftText =
        "- M / MOUNT #PetType? : Mount\r\n" +
        "- D / DS / DISMOUNT #PetType? : Dismount\r\n" +
        "- K / TERM : Terminate transport pet\r\n" +
        "- SIT : Sit or Stand\r\n" +
        "- JUMP : Knockback effect\r\n" +
        "- CAPE #Type? : PVP Cape (0-5)\r\n" +
        "- MOVEON #Radius? : Random move\r\n" +
        "- EQUIP #ItemName : Equip item\r\n" +
        "- UNEQUIP #ItemName : Unequip item\r\n" +
        "- USE #ItemName : Use item\r\n" +
        "- SETPOS : Set training position here\r\n" +
        "- SETPOS #X #Y : Set training position\r\n" +
        "- SETRADIUS #Radius : Set training radius\r\n" +
        "- SETSCRIPT #Path : Set script path\r\n" +
        "- SETAREA #Name : Set training area\r\n" +
        "- GETPOS : Get position\r\n" +
        "- STATUS / INV / HP / MP\r\n" +
        "- INJECT #Opcode #Enc? #Data?\r\n" +
        "- CHAT #Type #Message";

    var txtCommandsLeft = new TextBox();
    txtCommandsLeft.Multiline = true;
    txtCommandsLeft.ReadOnly = true;
    txtCommandsLeft.ScrollBars = ScrollBars.Vertical;
    txtCommandsLeft.Font = new Font("Segoe UI", 8.25f);
    txtCommandsLeft.Text = rightText;
    txtCommandsLeft.Location = new Point(10, 10);
    txtCommandsLeft.Size = new Size(250, 280);
    gbCommands.Controls.Add(txtCommandsLeft);

    var txtCommandsRight = new TextBox();
    txtCommandsRight.Multiline = true;
    txtCommandsRight.ReadOnly = true;
    txtCommandsRight.ScrollBars = ScrollBars.Vertical;
    txtCommandsRight.Font = new Font("Segoe UI", 8.25f);
    txtCommandsRight.Text = leftText;
    txtCommandsRight.Location = new Point(270, 10);
    txtCommandsRight.Size = new Size(250, 280);
    gbCommands.Controls.Add(txtCommandsRight);

    // Event Handlers
    btnAdd.Click += (s, e) =>
    {
        var name = (leaderInput.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        var normalized = NormalizePlayerName(name);
        if (AddLeaderName(name))
        {
            SaveLeaders();
            RefreshLeaderList();
            leaderInput.Text = "";
            Log.Info($"[{pluginName}] Added leader: {normalized}");
        }
    };

    btnRemove.Click += (s, e) =>
    {
        if (leaderList.SelectedItem == null) return;
        var selected = leaderList.SelectedItem.ToString();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            leaders.Remove(selected);
            SaveLeaders();
            RefreshLeaderList();
            Log.Info($"[{pluginName}] Removed leader: {selected}");
        }
    };
});

// Initialization — best-effort load; may use wrong config path if player not yet available
LoadLeaders();
RefreshLeaderList();

// Track whether we've successfully loaded leaders with a valid player context
var _leadersVerified = false;

// If game is already joined (script loaded after login), verify immediately
if (GameInfo.IsReady && !string.IsNullOrEmpty(Player.Name) && leaders.Count > 0)
    _leadersVerified = true;

// Logic Helpers
void RefreshLeaderList()
{
    GUI.BeginInvoke(() =>
    {
        if (leaderList == null) return;
        leaderList.Items.Clear();
        foreach (var name in leaders.OrderBy(x => x))
            leaderList.Items.Add(name);
    });
}

bool AddLeaderName(string playerName)
{
    var normalized = NormalizePlayerName(playerName);
    if (string.IsNullOrWhiteSpace(normalized))
        return false;

    return leaders.Add(normalized);
}

void LoadLeaders()
{
    try
    {
        leaders.Clear();
        Config.Load("xControl");
        var raw = Config.Get("xControl", "leaders", "");
        foreach (var item in (raw ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            AddLeaderName(item);
        }
    }
    catch { }
}

void SaveLeaders()
{
    try
    {
        var raw = string.Join(";", leaders.OrderBy(x => x));
        Config.Set("xControl", "leaders", raw);
        Config.Save("xControl");
    }
    catch { }
}

// Event Handlers

// ═══════════════════════════════════════════════════════════
// IPC COMMAND HANDLER (VManager → xControl sync)
// ═══════════════════════════════════════════════════════════

OnManagerCommand += (command) =>
{
    if (command == null || !command.StartsWith("xcontrol_sync:", StringComparison.OrdinalIgnoreCase))
        return;

    var payload = command.Substring("xcontrol_sync:".Length);
    leaders.Clear();

    foreach (var item in (payload ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
    {
        AddLeaderName(item);
    }

    SaveLeaders();
    RefreshLeaderList();
    Log.Info($"[{pluginName}] Leaders synced from VManager: {string.Join(", ", leaders)}");
};

// ═══════════════════════════════════════════════════════════
// HELPER FUNCTIONS
// ═══════════════════════════════════════════════════════════

bool IsLeader(string playerName)
{
    return ResolveLeaderSender(playerName) != null;
}

string ResolveLeaderSender(string playerName)
{
    if (string.IsNullOrWhiteSpace(playerName))
        return null;

    var normalized = NormalizePlayerName(playerName);
    if (string.IsNullOrWhiteSpace(normalized))
        return null;

    return leaders.Contains(normalized) ? normalized : null;
}

string NormalizePlayerName(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        return string.Empty;

    var value = name.Trim();

    while (value.Length > 2)
    {
        var first = value[0];
        char close;
        if (first == '[') close = ']';
        else if (first == '(') close = ')';
        else if (first == '<') close = '>';
        else break;

        var idx = value.IndexOf(close);
        if (idx <= 0)
            break;

        value = value.Substring(idx + 1).Trim();
    }

    var colon = value.IndexOf(':');
    if (colon > 0)
        value = value.Substring(0, colon).Trim();

    return value;
}

string NormalizeLookupKey(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

    var builder = new StringBuilder(value.Length);
    foreach (var ch in value.Trim())
    {
        if (char.IsLetterOrDigit(ch))
            builder.Append(char.ToLowerInvariant(ch));
    }

    return builder.ToString();
}

void AddLookupValue(HashSet<string> values, string value)
{
    var normalized = NormalizeLookupKey(value);
    if (!string.IsNullOrWhiteSpace(normalized))
        values.Add(normalized);
}

void AddLookupAliases(HashSet<string> values, string value)
{
    var normalized = NormalizeLookupKey(value);
    if (string.IsNullOrWhiteSpace(normalized))
        return;

    values.Add(normalized);
    if (extraZoneAliases.TryGetValue(normalized, out var aliases))
    {
        foreach (var alias in aliases)
            AddLookupValue(values, alias);
    }
}

HashSet<string> BuildLookupTerms(string value)
{
    var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    AddLookupAliases(values, value);
    return values;
}

HashSet<string> BuildDirectLookupTerms(string value)
{
    var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    AddLookupValue(values, value);
    return values;
}

string GetZoneDisplayName(string zoneKey)
{
    if (string.IsNullOrWhiteSpace(zoneKey))
        return string.Empty;

    try
    {
        var translated = vBot.Core.Game.ReferenceManager.GetTranslation(zoneKey);
        if (!string.IsNullOrWhiteSpace(translated) && !translated.Equals(zoneKey, StringComparison.OrdinalIgnoreCase))
            return translated;
    }
    catch { }

    return zoneKey;
}

System.Threading.Timer ScheduleOnce(int dueTimeMs, Action action)
{
    System.Threading.Timer timer = null;
    timer = new System.Threading.Timer(_ =>
    {
        try
        {
            action();
        }
        finally
        {
            lock (scheduledTimers)
            {
                scheduledTimers.Remove(timer);
            }

            timer?.Dispose();
        }
    }, null, dueTimeMs, System.Threading.Timeout.Infinite);

    lock (scheduledTimers)
    {
        scheduledTimers.Add(timer);
    }

    return timer;
}

bool LookupMatches(HashSet<string> values, HashSet<string> terms, bool exactOnly)
{
    foreach (var term in terms)
    {
        if (exactOnly)
        {
            if (values.Contains(term))
                return true;
        }
        else
        {
            if (values.Any(value => value.Contains(term, StringComparison.OrdinalIgnoreCase) || term.Contains(value, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
    }

    return false;
}

bool TryFindReverseZoneMatch(HashSet<string> terms, bool exactOnly, out int mapId, out string label)
{
    mapId = -1;
    label = null;

    if (terms.Count == 0)
        return false;

    var refMgr = vBot.Core.Game.ReferenceManager;
    if (refMgr == null)
        return false;

    var matches = new List<(int Id, string Label)>();
    foreach (var teleport in refMgr.OptionalTeleports.Values)
    {
        if (teleport == null)
            continue;

        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddLookupAliases(values, teleport.ZoneName128);
        AddLookupAliases(values, refMgr.GetTranslation(teleport.ZoneName128));
        AddLookupAliases(values, refMgr.GetOptionalTeleportLabel(teleport));

        if (LookupMatches(values, terms, exactOnly))
            matches.Add((teleport.ID, refMgr.GetOptionalTeleportLabel(teleport)));
    }

    if (matches.Count != 1)
        return false;

    mapId = matches[0].Id;
    label = matches[0].Label ?? string.Empty;
    return true;
}

bool TryResolveReverseZoneId(string search, out int mapId, out string label)
{
    mapId = -1;
    label = null;

    var refMgr = vBot.Core.Game.ReferenceManager;
    if (refMgr == null)
        return false;

    // Fast path: vBot's built-in exact label match
    if (refMgr.TryGetOptionalTeleportByLabel(search.Trim(), out var direct) && direct != null)
    {
        mapId = direct.ID;
        label = refMgr.GetOptionalTeleportLabel(direct);
        if (string.IsNullOrWhiteSpace(label))
            label = search.Trim();
        return true;
    }

    var exactTerms = BuildDirectLookupTerms(search);
    if (TryFindReverseZoneMatch(exactTerms, true, out mapId, out label))
    {
        if (string.IsNullOrWhiteSpace(label))
            label = search.Trim();
        return true;
    }

    var terms = BuildLookupTerms(search);
    if (terms.Count == 0)
        return false;

    foreach (var exactOnly in new[] { true, false })
    {
        if (TryFindReverseZoneMatch(terms, exactOnly, out mapId, out label))
        {
            if (string.IsNullOrWhiteSpace(label))
                label = search.Trim();
            return true;
        }
    }

    return false;
}

HashSet<string> GetRecallLookupNames(NpcInfo npc, vBot.Core.Client.ReferenceObjects.RefTeleport tp)
{
    var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    AddLookupAliases(values, npc?.Name);
    AddLookupAliases(values, npc?.CodeName);
    AddLookupAliases(values, tp?.CodeName);

    if (tp != null)
    {
        AddLookupAliases(values, tp.ZoneName128);
        AddLookupAliases(values, GetZoneDisplayName(tp.ZoneName128));
    }

    return values;
}

NpcInfo FindRecallNpc(string search, out string matchedLabel)
{
    matchedLabel = null;
    if (string.IsNullOrWhiteSpace(search))
        return null;

    var allNpcs = World.GetNPCs();

    // Primary: exact name match, same approach as phBot's GetNPCUniqueID
    foreach (var npc in allNpcs)
    {
        if (string.Equals(npc.Name?.Trim(), search.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            matchedLabel = npc.Name;
            return npc;
        }
    }

    // Secondary: name contains the search term (e.g. "Samarkand Gate Keeper" matches "Samarkand")
    foreach (var npc in allNpcs)
    {
        var name = npc.Name ?? "";
        if (name.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
            && FindTeleportForNpc(npc) != null)
        {
            matchedLabel = npc.Name;
            return npc;
        }
    }

    // Fallback: zone/codename lookup via TeleportData
    var terms = BuildLookupTerms(search);
    NpcInfo bestNpc = null;
    string bestLabel = null;
    var bestDistance = double.MaxValue;

    foreach (var npc in allNpcs)
    {
        var tp = FindTeleportForNpc(npc);
        if (tp == null)
            continue;

        var values = GetRecallLookupNames(npc, tp);
        if (!LookupMatches(values, terms, true) && !LookupMatches(values, terms, false))
            continue;

        if (bestNpc == null || npc.Distance < bestDistance)
        {
            bestNpc = npc;
            bestDistance = npc.Distance;
            bestLabel = GetZoneDisplayName(tp.ZoneName128);
        }
    }

    matchedLabel = string.IsNullOrWhiteSpace(bestLabel) ? search.Trim() : bestLabel;
    return bestNpc;
}

void SelectAndSetRecall(NpcInfo npc, string label)
{
    if (npc == null)
        return;

    var uid = npc.UniqueId;
    var matchedLabel = string.IsNullOrWhiteSpace(label) ? npc.Name : label;

    // phBot sends 0x7059 directly with the NPC unique ID — no select (0x7045) needed.
    // The server validates proximity server-side.
    try
    {
        Packets.Build(0x7059).WriteUInt(uid).SendToServer();
        Log.Info($"[{pluginName}] Recall set to {matchedLabel}");
    }
    catch (Exception ex)
    {
        Log.Warn($"[{pluginName}] Recall packet failed: {ex.Message}");
    }
}

double GetDistance(float ax, float ay, float bx, float by)
{
    return Math.Sqrt(Math.Pow(bx - ax, 2) + Math.Pow(by - ay, 2));
}

void RandomMovement(int maxRadius = 10)
{
    var rand = _rng;
    var offsetX = rand.NextDouble() * maxRadius * 2 - maxRadius;
    var offsetY = rand.NextDouble() * maxRadius * 2 - maxRadius;
    
    var newX = Player.X + (float)offsetX;
    var newY = Player.Y + (float)offsetY;
    
    Player.MoveTo(newX, newY);
    Log.Info($"[{pluginName}] Random move to ({newX:F1}, {newY:F1})");
}

void StartFollow(string player, double distance)
{
    followEnabled = true;
    followPlayer = player;
    followDistance = distance;
    Log.Info($"[{pluginName}] Following {player} at {distance}m distance");
}

void StopFollow()
{
    if (followEnabled)
    {
        followEnabled = false;
        followPlayer = "";
        Log.Info($"[{pluginName}] Stopped following");
    }
}

System.Reflection.FieldInfo _cachedItemField = null;
System.Reflection.MethodInfo _cachedUseToMethod = null;
object _cachedReverseScrollItem = null;
int _cachedReverseScrollTick = 0;
var _rng = new Random();
int _lastFollowMoveTick = 0;

object GetCoreInventoryItemFromScriptItem(object scriptItem)
{
    if (scriptItem == null) return null;

    try
    {
        if (_cachedItemField == null)
        {
            _cachedItemField = scriptItem.GetType().GetField("_item", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }
        
        if (_cachedItemField != null)
            return _cachedItemField.GetValue(scriptItem);
    }
    catch { }

    return null;
}

object FindReverseScrollCoreItem()
{
    try
    {
        // Use cached item if found recently (cache for 2 seconds to avoid repeated inventory scans)
        var now = Environment.TickCount;
        if (_cachedReverseScrollItem != null && (now - _cachedReverseScrollTick) < 2000)
            return _cachedReverseScrollItem;

        object found = null;

        // Only match items whose codename explicitly contains "REVERSE".
        // Do NOT use FindItemByType (wrong params matched return scroll) and do NOT
        // match "RETURN_SCROLL" — that is the regular one-way return scroll.
        foreach (var item in Inventory.GetAllItems())
        {
            var code = item?.CodeName ?? "";
            if (code.IndexOf("REVERSE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                found = GetCoreInventoryItemFromScriptItem(item);
                break;
            }
        }

        // Cache the result
        _cachedReverseScrollItem = found;
        _cachedReverseScrollTick = now;
        return found;
    }
    catch { }

    return null;
}

bool UseReverseScroll(byte destinationSlot, int mapId = -1)
{
    var coreItem = FindReverseScrollCoreItem();
    if (coreItem == null)
        return false;

    try
    {
        if (_cachedUseToMethod == null)
        {
            _cachedUseToMethod = coreItem.GetType().GetMethod("UseTo", new[] { typeof(byte), typeof(int) });
        }
        
        if (_cachedUseToMethod == null)
            return false;

        var result = _cachedUseToMethod.Invoke(coreItem, new object[] { destinationSlot, mapId });
        var used = result is bool ok && ok;
        if (used) _cachedReverseScrollItem = null; // invalidate so next call re-scans
        return used;
    }
    catch
    {
        return false;
    }
}

object FindInventoryItemByName(string itemName, bool equippedPart = false)
{
    if (string.IsNullOrWhiteSpace(itemName))
        return null;

    var search = itemName.Trim();
    var items = equippedPart ? Inventory.GetEquippedItems() : Inventory.GetAllItems();

    foreach (var item in items)
    {
        var name = item?.Name ?? "";
        var code = item?.CodeName ?? "";
        if (name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(code, search, StringComparison.OrdinalIgnoreCase) ||
            code.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return item;
        }
    }

    return null;
}

int GetEquipmentSlotFromCodeName(string codeName)
{
    if (string.IsNullOrWhiteSpace(codeName)) return -1;
    var c = codeName.ToUpperInvariant();

    if (c.Contains("HELMET") || c.Contains("_HELM_") || c.Contains("_HELM") ||
        c.Contains("_HAT_") || c.Contains("_HAT") || c.Contains("HEADGEAR") ||
        c.Contains("TIARA") || c.Contains("CROWN") || c.Contains("HOOD"))
        return 0;

    if (c.Contains("SHOULDER") || c.Contains("PAULDRON") || c.Contains("EPAULET"))
        return 1;

    if (c.Contains("GARMENT") || c.Contains("_ROBE") || c.Contains("_COAT") ||
        c.Contains("_ARMOR") || c.Contains("CORSELET") || c.Contains("HARNESS") ||
        c.Contains("TUNIC") || c.Contains("VEST") || c.Contains("_CHEST") || c.Contains("UPPER"))
        return 2;

    if (c.Contains("PANTS") || c.Contains("_PANT") || c.Contains("LEGGING") ||
        c.Contains("TROUSER") || c.Contains("SKIRT") || c.Contains("LOWER"))
        return 3;

    if (c.Contains("BOOTS") || c.Contains("_BOOT") || c.Contains("_SHOES") ||
        c.Contains("FOOTGUARD") || c.Contains("GREAVE") || c.Contains("SABATONS"))
        return 4;

    if (c.Contains("GLOVES") || c.Contains("_GLOVE") || c.Contains("GAUNTLET") ||
        c.Contains("HANDGUARD") || c.Contains("BRACER"))
        return 5;

    if (c.Contains("SHIELD") || c.Contains("PROTECTOR") || c.Contains("BUCKLER"))
        return 7;

    if (c.Contains("EARRING")) return 8;
    if (c.Contains("NECKLACE") || c.Contains("AMULET") || c.Contains("PENDANT")) return 9;
    if (c.Contains("RING")) return 10;

    return 6; // default: weapon slot
}

bool TryEquipByName(string itemName)
{
    var search = (itemName ?? "").Trim();
    if (string.IsNullOrWhiteSpace(search))
        return false;

    foreach (var item in Inventory.GetAllItems())
    {
        if (item == null) continue;
        var name = item.Name ?? "";
        var code = item.CodeName ?? "";
        if (name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
            !string.Equals(code, search, StringComparison.OrdinalIgnoreCase) &&
            code.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
            continue;

        try
        {
            if (_cachedSlotProp == null)
                _cachedSlotProp = item.GetType().GetProperty("Slot");

            if (_cachedSlotProp == null)
                return false;

            var fromSlot = Convert.ToInt32(_cachedSlotProp.GetValue(item));
            if (fromSlot < 13)
                return false; // already equipped

            var toSlot = GetEquipmentSlotFromCodeName(code);
            if (toSlot < 0)
                return false; // unrecognized item type

            // If ring slot 10 is occupied, try slot 11
            if (toSlot == 10 && Inventory.GetItemAt(10) != null)
                toSlot = 11;

            Inventory.MoveItem(fromSlot, toSlot);
            return true;
        }
        catch
        {
            return false;
        }
    }

    return false;
}

int FindFreeInventorySlot()
{
    for (var slot = 13; slot < Inventory.Capacity; slot++)
    {
        if (Inventory.GetItemAt(slot) == null)
            return slot;
    }

    return -1;
}

System.Reflection.PropertyInfo _cachedSlotProp = null;

bool TryUnequipByName(string itemName)
{
    var scriptItem = FindInventoryItemByName(itemName, true);
    if (scriptItem == null)
        return false;

    try
    {
        if (_cachedSlotProp == null)
        {
            _cachedSlotProp = scriptItem.GetType().GetProperty("Slot");
        }
        
        if (_cachedSlotProp == null)
            return false;

        var srcSlot = Convert.ToInt32(_cachedSlotProp.GetValue(scriptItem));
        var dstSlot = FindFreeInventorySlot();
        if (dstSlot < 0)
            return false;

        return Inventory.MoveItem(srcSlot, dstSlot);
    }
    catch
    {
        return false;
    }
}

bool TrySetTrainingAreaByName(string areaName)
{
    if (string.IsNullOrWhiteSpace(areaName))
        return false;

    var search = areaName.Trim();

    // Check default area first
    var defaultName = vBot.Core.PlayerConfig.Get("vBot.Training.DefaultName", "Default");
    if (!string.IsNullOrWhiteSpace(defaultName) && defaultName.Equals(search, StringComparison.OrdinalIgnoreCase))
    {
        vBot.Core.PlayerConfig.Set("vBot.Training.Index", 0);
        vBot.Core.PlayerConfig.Set("vBot.Training.AreaScript", vBot.Core.PlayerConfig.Get("vBot.Training.DefaultScript", string.Empty));
        return true;
    }

    // Search saved areas
    var rawAreas = vBot.Core.PlayerConfig.GetArray<string>("vBot.Training.Areas");
    for (var index = 0; index < rawAreas.Length; index++)
    {
        var split = rawAreas[index].Split("|", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (!vBot.Core.Objects.Area.TryParse(split, out var area))
            continue;

        if (!area.Name.Equals(search, StringComparison.OrdinalIgnoreCase))
            continue;

        // +1 because index 0 is reserved for default area in UI logic
        vBot.Core.PlayerConfig.Set("vBot.Training.Index", index + 1);
        vBot.Core.PlayerConfig.Set("vBot.Area.Region", area.Position.Region);
        vBot.Core.PlayerConfig.Set("vBot.Area.X", area.Position.XOffset);
        vBot.Core.PlayerConfig.Set("vBot.Area.Y", area.Position.YOffset);
        vBot.Core.PlayerConfig.Set("vBot.Area.Z", area.Position.ZOffset);
        vBot.Core.PlayerConfig.Set("vBot.Area.Radius", area.Radius);
        vBot.Core.PlayerConfig.Set("vBot.Training.AreaScript", area.ScriptFile ?? "");
        return true;
    }

    return false;
}

bool TryParseHexOpcode(string text, out ushort opcode)
{
    opcode = 0;
    if (string.IsNullOrWhiteSpace(text))
        return false;

    var t = text.Trim();
    if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        t = t.Substring(2);

    return ushort.TryParse(t, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out opcode);
}

byte[] ParseHexBytes(string text)
{
    if (string.IsNullOrWhiteSpace(text))
        return Array.Empty<byte>();

    var clean = text.Replace(" ", "").Replace("-", "").Replace(":", "").Trim();
    if (clean.Length % 2 != 0)
        return null;

    var data = new byte[clean.Length / 2];
    for (var i = 0; i < data.Length; i++)
    {
        if (!byte.TryParse(clean.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out data[i]))
            return null;
    }

    return data;
}

bool TryParseBoolToken(string text, out bool value)
{
    value = false;
    if (string.IsNullOrWhiteSpace(text))
        return false;

    var t = text.Trim().ToLowerInvariant();
    if (t == "1" || t == "true" || t == "yes" || t == "y") { value = true; return true; }
    if (t == "0" || t == "false" || t == "no" || t == "n") { value = false; return true; }
    return false;
}

bool TryHandleChatCommand(string chatArgs)
{
    if (string.IsNullOrWhiteSpace(chatArgs))
        return false;

    var parts = chatArgs.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 2)
        return false;

    var chatType = parts[0].ToLowerInvariant();
    var payload = parts[1].Trim();

    switch (chatType)
    {
        case "all": return Chat.All(payload);
        case "party": return Chat.Party(payload);
        case "guild": return Chat.Guild(payload);
        case "union": return Chat.Union(payload);
        case "stall": return Chat.Stall(payload);
        case "academy": return Chat.Academy(payload);
        case "global": return Chat.Global(payload);
        case "notice": Chat.Notice(payload); return true;
        case "private":
        {
            var pm = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (pm.Length < 2) return false;
            return Chat.Private(pm[0], pm[1]);
        }
        default:
            return false;
    }
}

bool TryHandleInjectCommand(string injectArgs)
{
    if (string.IsNullOrWhiteSpace(injectArgs))
        return false;

    var parts = injectArgs.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 1)
        return false;

    if (!TryParseHexOpcode(parts[0], out var opcode))
        return false;

    var encrypted = false;
    string dataText = null;

    if (parts.Length >= 2)
    {
        if (TryParseBoolToken(parts[1], out encrypted))
        {
            if (parts.Length >= 3)
                dataText = string.Join(" ", parts.Skip(2));
        }
        else
        {
            dataText = string.Join(" ", parts.Skip(1));
        }
    }

    var data = string.IsNullOrWhiteSpace(dataText) ? Array.Empty<byte>() : ParseHexBytes(dataText);
    if (data == null)
        return false;

    Packets.SendToServer(opcode, data, encrypted);
    return true;
}

bool TryParseTeleportRoute(string body, out string sourceName, out string destName)
{
    sourceName = null;
    destName = null;

    if (string.IsNullOrWhiteSpace(body))
        return false;

    if (body.Contains(','))
    {
        var parts = body.Split(new[] { ',' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        sourceName = parts[0].Trim();
        destName = parts[1].Trim();
        return !string.IsNullOrWhiteSpace(sourceName) && !string.IsNullOrWhiteSpace(destName);
    }

    var tokens = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (tokens.Length < 2)
        return false;

    string matchedSource = null;
    string matchedDest = null;

    for (var splitIndex = 1; splitIndex < tokens.Length; splitIndex++)
    {
        var src = string.Join(" ", tokens.Take(splitIndex));
        var dst = string.Join(" ", tokens.Skip(splitIndex));

        if (FindTeleportByName(src) == null || FindTeleportByName(dst) == null)
            continue;

        if (matchedSource != null)
            return false;

        matchedSource = src;
        matchedDest = dst;
    }

    if (matchedSource == null)
    {
        if (tokens.Length != 2)
            return false;

        matchedSource = tokens[0];
        matchedDest = tokens[1];
    }

    sourceName = matchedSource;
    destName = matchedDest;
    return true;
}

bool TryMount(string petType)
{
    var type = NormalizeLookupKey(string.IsNullOrWhiteSpace(petType) ? "transport" : petType);

    // 'pick' / ability pets cannot be ridden
    if (type == "pick" || type == "pickup" || type == "grab" || type == "ability")
        return false;

    // 'horse' path: if already summoned mount it directly, otherwise use inventory item
    if (type == "horse" || type == "mount" || type == "ride")
    {
        var horseVehicle = vBot.Core.Game.Player?.Vehicle;
        if (horseVehicle != null)
            return SendMountPacket(horseVehicle.UniqueId, 0x01);

        foreach (var item in Inventory.GetAllItems())
        {
            if (item == null) continue;
            if ((item.CodeName ?? "").StartsWith("ITEM_COS_C_", StringComparison.OrdinalIgnoreCase))
                return item.Use();
        }
        return false;
    }

    var player = vBot.Core.Game.Player;

    if (type == "fellow" || type == "attack" || type == "pet")
        return player?.Fellow != null && SendMountPacket(player.Fellow.UniqueId, 0x01);

    // transport / trade: if a pet is already summoned, mount it — don't use inventory again
    var jobPet = Pets.GetJobTransport();
    if (jobPet != null)
        return SendMountPacket(jobPet.UniqueId, 0x01);

    if (player?.Vehicle != null)
        return SendMountPacket(player.Vehicle.UniqueId, 0x01);

    // Nothing summoned yet — use inventory item to summon it
    foreach (var item in Inventory.GetAllItems())
    {
        if (item == null) continue;
        if ((item.CodeName ?? "").StartsWith("ITEM_COS_T_", StringComparison.OrdinalIgnoreCase))
            return item.Use();
    }

    return false;
}

bool TryDismount(string petType = "transport")
{
    var type = NormalizeLookupKey(string.IsNullOrWhiteSpace(petType) ? "transport" : petType);

    if (type == "pick" || type == "pickup" || type == "grab" || type == "ability")
        return false;

    var player = vBot.Core.Game.Player;

    if (type == "fellow" || type == "attack" || type == "pet")
        return player?.Fellow != null && SendMountPacket(player.Fellow.UniqueId, 0x00);

    // transport / horse / default — dismount current vehicle
    if (player?.Vehicle != null)
        return SendMountPacket(player.Vehicle.UniqueId, 0x00);

    if (player?.AbilityPet != null)
        return SendMountPacket(player.AbilityPet.UniqueId, 0x00);

    return false;
}

bool SendMountPacket(uint uniqueId, byte flag)
{
    Packets.Build(0x70CB)
        .WriteByte(flag)
        .WriteUInt(uniqueId)
        .SendToServer();
    return true;
}

byte ParseCapeType(string token)
{
    if (string.IsNullOrWhiteSpace(token))
        return 5;

    var t = token.Trim().ToLowerInvariant();
    if (byte.TryParse(t, out var val))
        return (byte)Math.Clamp(val, (byte)0, (byte)5);

    return t switch
    {
        "off" => 0,
        "none" => 0,
        "red" => 1,
        "gray" or "grey" => 2,
        "blue" => 3,
        "white" => 4,
        "gold" or "yellow" => 5,
        _ => 0
    };
}

// ═══════════════════════════════════════════════════════════
// TELEPORT HELPERS (Q1/Q2/Q3/LQ/TP)
// ═══════════════════════════════════════════════════════════

List<string> GetTeleportCodeCandidates(string npcCodeName)
{
    var candidates = new List<string>();
    if (string.IsNullOrWhiteSpace(npcCodeName))
        return candidates;

    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    void AddCandidate(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
            candidates.Add(value);
    }

    AddCandidate(npcCodeName);
    AddCandidate("GATE_" + npcCodeName);

    var normalized = npcCodeName;
    foreach (var prefix in new[] { "STORE_", "NPC_" })
    {
        if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(prefix.Length);
            break;
        }
    }

    AddCandidate(normalized);

    if (normalized.EndsWith("_GATE", StringComparison.OrdinalIgnoreCase))
        normalized = normalized.Substring(0, normalized.Length - "_GATE".Length);

    if (!normalized.StartsWith("GATE_", StringComparison.OrdinalIgnoreCase))
        AddCandidate("GATE_" + normalized);
    else
        AddCandidate(normalized);

    return candidates;
}

// Find the RefTeleport entry associated with a nearby spawned NPC.
// Matches by AssocRefObjId == NPC's model/ref ID, then by GATE_ + codename.
vBot.Core.Client.ReferenceObjects.RefTeleport FindTeleportForNpc(NpcInfo npc)
{
    if (npc == null)
        return null;

    var refMgr = vBot.Core.Game.ReferenceManager;

    // Primary: match by ref obj ID
    var tp = refMgr.TeleportData.FirstOrDefault(t => t.AssocRefObjId == npc.ModelId);
    if (tp != null)
        return tp;

    // Fallback: GATE_ + codename
    foreach (var gateCode in GetTeleportCodeCandidates(npc.CodeName))
    {
        tp = refMgr.TeleportData.FirstOrDefault(t =>
            string.Equals(t.CodeName, gateCode, StringComparison.OrdinalIgnoreCase));
        if (tp != null)
            return tp;
    }

    return null;
}

// Find the nearest teleporter NPC that has teleport data.
NpcInfo FindNearestTeleporter()
{
    var npcs = World.GetNPCs();
    NpcInfo closest = null;
    double closestDist = double.MaxValue;

    foreach (var npc in npcs)
    {
        if (npc.Distance < closestDist && FindTeleportForNpc(npc) != null)
        {
            closest = npc;
            closestDist = npc.Distance;
        }
    }

    return closest;
}

// Resolve a RefTeleport by in-game display name, CodeName, or zone name.
vBot.Core.Client.ReferenceObjects.RefTeleport FindTeleportByName(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        return null;

    var refMgr = vBot.Core.Game.ReferenceManager;
    var search = NormalizeLookupKey(name);
    vBot.Core.Client.ReferenceObjects.RefTeleport zoneMatch = null;

    foreach (var tp in refMgr.TeleportData)
    {
        if (tp == null)
            continue;

        // Match by teleport codename
        if (!string.IsNullOrEmpty(tp.CodeName)
            && NormalizeLookupKey(tp.CodeName) == search)
            return tp;

        // Match by associated NPC's translated display name
        if (tp.AssocRefObjId != 0
            && refMgr.CharacterData.TryGetValue(tp.AssocRefObjId, out var ch)
            && ch != null
            && !string.IsNullOrEmpty(ch.NameStrID))
        {
            var npcTr = refMgr.GetTranslation(ch.NameStrID);
            if (!string.IsNullOrEmpty(npcTr)
                && NormalizeLookupKey(npcTr) == search)
                return tp;
        }

        // Match by zone display name (lower priority)
        if (zoneMatch == null && !string.IsNullOrEmpty(tp.ZoneName128))
        {
            var zoneTr = GetZoneDisplayName(tp.ZoneName128);
            if (!string.IsNullOrEmpty(zoneTr)
                && NormalizeLookupKey(zoneTr) == search)
                zoneMatch = tp;
        }
    }

    return zoneMatch;
}

// Select any entity (NPC or portal) by UID, then after a delay send the teleport packet.
// Keep Players trace active across teleport. The trace manager will naturally
// wait while the game is loading and continue/re-resolve after the new area loads.
void SelectAndTeleport(uint uniqueId, uint destTeleportId, string logLabel, string entityName = "")
{
    StopFollow();

    var uid = uniqueId;
    var destId = destTeleportId;
    var label = logLabel;
    var via = string.IsNullOrWhiteSpace(entityName) ? uid.ToString() : entityName;

    Log.Info($"[{pluginName}] Teleporting to [{label}] via [{via}] while keeping trace active");

    // Wait 500ms for the character to fully stop server-side, then select the NPC
    ScheduleOnce(500, () =>
    {
        try
        {
            Packets.Build(0x7045).WriteUInt(uid).SendToServer();
        }
        catch (Exception ex)
        {
            Log.Warn($"[{pluginName}] NPC select packet failed: {ex.Message}");
            return;
        }

        // Wait 1000ms after selection for the server to register it, then teleport
        ScheduleOnce(1000, () =>
        {
            try
            {
                Packets.Build(0x705A)
                    .WriteUInt(uid)
                    .WriteByte((byte)0x02)
                    .WriteUInt(destId)
                    .SendToServer();
                Log.Info($"[{pluginName}] Teleporting to [{label}]");
            }
            catch (Exception ex)
            {
                Log.Warn($"[{pluginName}] Teleport packet failed: {ex.Message}");
            }
        });
    });
}

// Q1/Q2/Q3: teleport to the Nth destination of the nearest teleporter (NPC or portal, 1-based).
bool TryTeleportByIndex(int index)
{
    // Try NPC-based teleporter first
    var npc = FindNearestTeleporter();
    if (npc != null)
    {
        var tp = FindTeleportForNpc(npc);
        var links = tp?.GetLinks();
        if (links != null && index >= 1 && index <= links.Count)
        {
            var targetLink = links[index - 1];
            var destTp = targetLink.Target;
            var destLabel = destTp?.ZoneName ?? destTp?.CodeName ?? $"#{targetLink.TargetTeleport}";
            SelectAndTeleport(npc.UniqueId, (uint)targetLink.TargetTeleport, destLabel, npc.Name);
            return true;
        }
    }

    // Fallback: nearest portal with destinations (gates are PortalInfo, not NpcInfo)
    var nearestPortal = World.GetPortals()
        .Where(p => p.DestinationZones != null && p.DestinationZones.Count > 0)
        .OrderBy(p => p.Distance)
        .FirstOrDefault();

    if (nearestPortal == null)
        return false;

    if (index < 1 || index > nearestPortal.DestinationZones.Count)
    {
        Log.Warn($"[{pluginName}] Portal [{nearestPortal.DisplayLabel}] has {nearestPortal.DestinationZones.Count} destinations, Q{index} is out of range");
        return false;
    }

    var destZone = nearestPortal.DestinationZones[index - 1];
    var dstPortalTp = FindTeleportByName(destZone);
    if (dstPortalTp == null)
    {
        Log.Warn($"[{pluginName}] Q{index}: cannot resolve teleport ID for destination '{destZone}'");
        return false;
    }

    SelectAndTeleport(nearestPortal.UniqueId, dstPortalTp.ID, destZone, nearestPortal.DisplayLabel);
    return true;
}

// TP command: named source → named destination with delay.
// Teleport gates are PortalInfo entities (World.GetPortals()), not NPCs.
bool TryTeleportNamed(string sourceName, string destName)
{
    try
    {
        var refMgr = vBot.Core.Game.ReferenceManager;

        // ── 1. Portal-based path (correct for in-world teleport gates) ──────────
        var portals = World.GetPortals();

        // Source portal: exact match on ZoneName (home zone) or Name
        var srcPortal = portals
            .Where(p =>
                string.Equals(p.ZoneName, sourceName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, sourceName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Distance)
            .FirstOrDefault();

        if (srcPortal != null)
        {
            // Destination must be in the portal's DestinationZones (case-insensitive equality)
            var destZoneMatch = srcPortal.DestinationZones
                .FirstOrDefault(z => string.Equals(z, destName, StringComparison.OrdinalIgnoreCase));

            if (destZoneMatch == null)
            {
                Log.Warn($"[{pluginName}] Portal [{srcPortal.DisplayLabel}] cannot reach '{destName}'. Available: {string.Join(", ", srcPortal.DestinationZones)}");
                return false;
            }

            var dstPortalTp = FindTeleportByName(destZoneMatch);
            if (dstPortalTp == null)
            {
                Log.Warn($"[{pluginName}] TP: cannot resolve teleport ID for destination '{destZoneMatch}'");
                return false;
            }

            SelectAndTeleport(srcPortal.UniqueId, dstPortalTp.ID, destZoneMatch, srcPortal.DisplayLabel);
            return true;
        }

        // ── 2. NPC-based path (fallback for trader-NPCs etc.) ──────────────────
        var srcTp = FindTeleportByName(sourceName);
        if (srcTp == null)
            return false;

        var dstTp = FindTeleportByName(destName);
        if (dstTp == null)
            return false;

        var hasLink = refMgr.TeleportLinks.Any(l => l.OwnerTeleport == (int)srcTp.ID
                                                   && l.TargetTeleport == (int)dstTp.ID);
        if (!hasLink)
            return false;

        string srcNpcCode = null;
        if (srcTp.AssocRefObjId != 0
            && refMgr.CharacterData.TryGetValue(srcTp.AssocRefObjId, out var srcCh)
            && srcCh != null)
            srcNpcCode = srcCh.CodeName;

        var npcs = World.GetNPCs();
        NpcInfo targetNpc = null;
        if (!string.IsNullOrEmpty(srcNpcCode))
            targetNpc = npcs.FirstOrDefault(n =>
                string.Equals(n.CodeName, srcNpcCode, StringComparison.OrdinalIgnoreCase));
        if (targetNpc == null && !string.IsNullOrEmpty(srcTp.CodeName))
            targetNpc = npcs.FirstOrDefault(n =>
                string.Equals(n.CodeName, srcTp.CodeName, StringComparison.OrdinalIgnoreCase));
        if (targetNpc == null)
            targetNpc = npcs.FirstOrDefault(n =>
                string.Equals(n.Name, sourceName, StringComparison.OrdinalIgnoreCase));

        if (targetNpc == null)
            return false;

        SelectAndTeleport(targetNpc.UniqueId, dstTp.ID, destName, targetNpc.Name);
        return true;
    }
    catch (Exception ex)
    {
        Log.Warn($"[{pluginName}] TryTeleportNamed {sourceName} -> {destName} failed: {ex.Message}");
        return false;
    }
}

// ═══════════════════════════════════════════════════════════
// COMMAND HANDLER
// ═══════════════════════════════════════════════════════════

void HandleCommand(string sender, string msg)
{
    msg = msg.Trim();

    // Only process commands whose keyword is ALL CAPS — e.g. "CHAT PARTY Hello" is fine, "chat party hello" is ignored
    var keyword = msg.Contains(' ') ? msg.Substring(0, msg.IndexOf(' ')) : msg;
    if (keyword != keyword.ToUpper() || keyword.Length == 0)
        return;

    var upperMsg = msg.ToUpper();
    
    // BOT CONTROL
    if (upperMsg == "S" || upperMsg == "START")
    {
        Bot.Start();
        Log.Info($"[{pluginName}] Bot started by {sender}");
    }
    else if (upperMsg == "SS" || upperMsg == "STOP")
    {
        Bot.Stop();
        Log.Info($"[{pluginName}] Bot stopped by {sender}");
    }
    
    // TRACE
    else if (upperMsg == "T" || upperMsg == "TRACE")
    {
        Player.StartTrace(sender);
        Log.Info($"[{pluginName}] Starting Players trace to {sender}");
    }
    else if (upperMsg.StartsWith("T ") || upperMsg.StartsWith("TRACE "))
    {
        var target = upperMsg.StartsWith("TRACE ") ? msg.Substring(6).Trim() : msg.Substring(2).Trim();
        if (!string.IsNullOrWhiteSpace(target))
        {
            Player.StartTrace(target);
            Log.Info($"[{pluginName}] Starting Players trace to {target}");
        }
    }
    else if (upperMsg == "N" || upperMsg == "NOTRACE")
    {
        Player.StopTrace();
        StopFollow();
        Log.Info($"[{pluginName}] Players trace stopped");
    }
    
    // FOLLOW
    else if (upperMsg == "F" || upperMsg == "FOLLOW")
    {
        StartFollow(sender, 10);
    }
    else if (upperMsg.StartsWith("F ") || upperMsg.StartsWith("FOLLOW "))
    {
        var argsText = upperMsg.StartsWith("FOLLOW ") ? msg.Substring(7).Trim() : msg.Substring(2).Trim();
        var parts = argsText.Split(' ');
        var target = parts.Length > 0 ? parts[0] : sender;
        var distance = parts.Length > 1 && double.TryParse(parts[1], out var d) ? d : 10;
        StartFollow(target, distance);
    }
    else if (upperMsg == "NF" || upperMsg == "NOFOLLOW")
    {
        StopFollow();
    }
    
    // MOVEMENT
    else if (upperMsg == "MOVEON")
    {
        RandomMovement(10);
    }
    else if (upperMsg.StartsWith("MOVEON "))
    {
        if (int.TryParse(msg.Substring(7).Trim(), out var radius))
        {
            RandomMovement(Math.Abs(radius));
        }
    }
    else if (upperMsg == "SIT")
    {
        Packets.SendToServer(0x704F, new byte[] { 0x04 });
        Log.Info($"[{pluginName}] Sit/Stand");
    }
    
    // POSITION
    else if (upperMsg == "GETPOS")
    {
        var posMsg = $"Position: ({Player.X:F1}, {Player.Y:F1}) Region: {Player.RegionId}";
        Chat.Private(sender, posMsg);
        Log.Info($"[{pluginName}] Sent position to {sender}");
    }
    else if (upperMsg == "SETPOS")
    {
        // Use vBot training coordinate system (offsets + region)
        Bot.SetTrainingPositionHere();
        Log.Info($"[{pluginName}] Training position set to X:{Player.XOffset:F1}, Y:{Player.YOffset:F1}, Region:{Player.RegionId}");
    }
    else if (upperMsg.StartsWith("SETPOS "))
    {
        var parts = msg.Substring(7).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y))
        {
            Bot.SetTrainingPosition(x, y);
            Log.Info($"[{pluginName}] Training position set to X:{x:F1}, Y:{y:F1}");
            if (parts.Length >= 3)
            {
                Log.Info($"[{pluginName}] Region/Z parameters are not supported by API; using current region.");
            }
        }
    }
    
    // ITEMS
    else if (upperMsg == "RE" || upperMsg == "RETURN")
    {
        if (Player.IsDead)
        {
            Packets.SendToServer(0x3053, new byte[] { 0x01 });
            Log.Info($"[{pluginName}] Resurrecting at town...");
        }
        else
        {
            Log.Info($"[{pluginName}] Using return scroll...");
            Player.UseReturnScroll();
        }
    }
    // TELEPORT: Q1/Q2/Q3 = Nth destination of nearest teleporter NPC
    else if (upperMsg == "LQ" || upperMsg.StartsWith("LQ "))
    {
        if (!TryTeleportNamed("Kings Valley", "Pharaoh tomb (beginner)"))
            Log.Warn($"[{pluginName}] LQ: teleporter not nearby or route unavailable");
    }
    else if (upperMsg == "Q1" || upperMsg.StartsWith("Q1 "))
    {
        if (!TryTeleportByIndex(1))
            Log.Warn($"[{pluginName}] Q1: no teleporter nearby or no 1st destination");
    }
    else if (upperMsg == "Q2" || upperMsg.StartsWith("Q2 "))
    {
        if (!TryTeleportByIndex(2))
            Log.Warn($"[{pluginName}] Q2: no teleporter nearby or no 2nd destination");
    }
    else if (upperMsg == "Q3" || upperMsg.StartsWith("Q3 "))
    {
        if (!TryTeleportByIndex(3))
            Log.Warn($"[{pluginName}] Q3: no teleporter nearby or no 3rd destination");
    }
    else if (upperMsg.StartsWith("TP "))
    {
        var body = msg.Substring(3).Trim();
        if (TryParseTeleportRoute(body, out var src, out var dst))
        {
            if (!TryTeleportNamed(src, dst))
                Log.Warn($"[{pluginName}] TP {src} -> {dst} failed");
        }
        else
        {
            Log.Warn($"[{pluginName}] TP format: TP <source>,<destination>");
        }
    }
    else if (upperMsg.StartsWith("RECALL "))
    {
        var recallSearch = msg.Substring(7).Trim();

        // Recall gates are PortalInfo entities — NOT NPCs. World.GetNPCs() won't find them.
        var portals = World.GetPortals();

        // Only exact (full-name) equality — no substring/partial matching.
        // Checks ZoneName first, then Name, then CodeName.
        var gate = portals
            .Where(p =>
                string.Equals(p.ZoneName, recallSearch, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, recallSearch, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.CodeName, recallSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Distance)
            .FirstOrDefault();

        if (gate != null)
        {
            var label = string.IsNullOrWhiteSpace(gate.ZoneName) ? gate.Name : gate.ZoneName;
            Log.Info($"[{pluginName}] Setting recall to [{label}]...");
            // SetReturnPoint() blocks up to 5s — run on background thread
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (gate.SetReturnPoint())
                        Log.Info($"[{pluginName}] Recall set to [{label}]");
                    else
                        Log.Warn($"[{pluginName}] Recall failed for [{label}]");
                }
                catch (Exception ex)
                {
                    Log.Warn($"[{pluginName}] Recall error: {ex.Message}");
                }
            });
        }
        else
        {
            Log.Warn($"[{pluginName}] Recall: no portal found nearby matching '{recallSearch}'");
        }
    }
    else if (upperMsg == "Z" || upperMsg == "ZERK")
    {
        var player = vBot.Core.Game.Player;
        if (player?.CanEnterBerzerk == true)
        {
            player.EnterBerzerkMode();
            Log.Info($"[{pluginName}] Entering berserk mode");
        }
        else
        {
            Log.Warn($"[{pluginName}] Berserk not available");
        }
    }
    else if (upperMsg == "M" || upperMsg == "MOUNT" || upperMsg.StartsWith("M ") || upperMsg.StartsWith("MOUNT "))
    {
        var petType = "transport";
        if (upperMsg.StartsWith("M ") || upperMsg.StartsWith("MOUNT "))
            petType = upperMsg.StartsWith("MOUNT ") ? msg.Substring(6).Trim() : msg.Substring(2).Trim();

        if (TryMount(string.IsNullOrWhiteSpace(petType) ? "transport" : petType))
            Log.Info($"[{pluginName}] Mount command executed");
        else
            Log.Warn($"[{pluginName}] Mount failed");
    }
    else if (upperMsg == "D" || upperMsg == "DS" || upperMsg == "DISMOUNT"
          || upperMsg.StartsWith("D ") || upperMsg.StartsWith("DS ") || upperMsg.StartsWith("DISMOUNT "))
    {
        var petType = "transport";
        if (upperMsg.StartsWith("DISMOUNT "))
            petType = msg.Substring(9).Trim();
        else if (upperMsg.StartsWith("DS "))
            petType = msg.Substring(3).Trim();
        else if (upperMsg.StartsWith("D "))
            petType = msg.Substring(2).Trim();

        if (TryDismount(string.IsNullOrWhiteSpace(petType) ? "transport" : petType))
            Log.Info($"[{pluginName}] Dismount command executed");
        else
            Log.Warn($"[{pluginName}] Dismount failed");
    }
    else if (upperMsg == "K" || upperMsg == "KILL" || upperMsg == "TERM" || upperMsg == "TERMINATE")
    {
        var jobPet = Pets.GetJobTransport();
        var vehicle = Pets.GetVehicle();
        if (jobPet == null && vehicle == null)
        {
            Log.Warn($"[{pluginName}] No active transport pet to terminate");
        }
        else
        {
            Log.Info($"[{pluginName}] Terminating transport pet in 500ms...");
            ScheduleOnce(500, () =>
            {
                jobPet?.Terminate();
                vehicle?.Terminate();
            });
        }
    }
    else if (upperMsg == "SETRADIUS")
    {
        Log.Info($"[{pluginName}] Current training radius: {Bot.TrainingRadius}. Use: SETRADIUS <value>");
    }
    else if (upperMsg.StartsWith("SETRADIUS "))
    {
        if (int.TryParse(msg.Substring(10).Trim(), out var radius))
        {
            Bot.SetTrainingRadius(Math.Max(1, Math.Abs(radius)));
            Log.Info($"[{pluginName}] Training radius set to {Math.Max(1, Math.Abs(radius))}");
        }
        else
        {
            Log.Warn($"[{pluginName}] Invalid radius");
        }
    }
    else if (upperMsg.StartsWith("SETSCRIPT "))
    {
        var scriptPath = msg.Substring(10).Trim();
        vBot.Core.PlayerConfig.Set("vBot.Training.AreaScript", scriptPath);
        vBot.Core.PlayerConfig.Set("vBot.Lure.SelectedScriptPath", scriptPath);
        vBot.Core.PlayerConfig.Set("vBot.Lure.UseScript", true);
        Log.Info($"[{pluginName}] Script path set: {scriptPath}");
    }
    else if (upperMsg.StartsWith("SETAREA "))
    {
        var areaName = msg.Substring(8).Trim();
        if (TrySetTrainingAreaByName(areaName))
        {
            Log.Info($"[{pluginName}] Training area set: {areaName}");
            try { vBot.Core.Event.EventManager.FireEvent("OnSetTrainingArea"); } catch { }
        }
        else
        {
            Log.Warn($"[{pluginName}] Training area not found: {areaName}");
        }
    }
    else if (upperMsg == "PROFILE" || upperMsg.StartsWith("PROFILE "))
    {
        Log.Warn($"[{pluginName}] PROFILE is disabled because the vBot ProfileManager API is not available");
    }
    else if (upperMsg.StartsWith("INJECT "))
    {
        if (TryHandleInjectCommand(msg.Substring(7)))
            Log.Info($"[{pluginName}] Packet injected");
        else
            Log.Warn($"[{pluginName}] Inject format: INJECT <opcodeHex> <encrypted?> <dataHex>");
    }
    else if (upperMsg.StartsWith("CHAT "))
    {
        if (!TryHandleChatCommand(msg.Substring(5)))
            Log.Warn($"[{pluginName}] Chat format invalid");
    }
    else if (upperMsg == "JUMP")
    {
        Packets.SendToServer(0x3091, new byte[] { 0x0C });
        Log.Info($"[{pluginName}] Jump effect triggered");
    }
    else if (upperMsg.StartsWith("CAPE"))
    {
        var capeArg = upperMsg == "CAPE" ? "" : msg.Substring(4).Trim();
        var capeType = ParseCapeType(capeArg);
        Packets.Build(0x7516).WriteByte(capeType).SendToServer();
        Log.Info($"[{pluginName}] Cape request sent ({capeType})");
    }
    else if (upperMsg.StartsWith("EQUIP "))
    {
        var itemName = msg.Substring(6).Trim();
        if (TryEquipByName(itemName))
            Log.Info($"[{pluginName}] Equipped: {itemName}");
        else
            Log.Warn($"[{pluginName}] Unable to equip: {itemName}");
    }
    else if (upperMsg.StartsWith("UNEQUIP "))
    {
        var itemName = msg.Substring(8).Trim();
        if (TryUnequipByName(itemName))
            Log.Info($"[{pluginName}] Unequipped: {itemName}");
        else
            Log.Warn($"[{pluginName}] Unable to unequip: {itemName}");
    }
    else if (upperMsg.StartsWith("REVERSE"))
    {
        var arg = msg.Length > 7 ? msg.Substring(7).Trim() : "";
        if (string.IsNullOrWhiteSpace(arg))
        {
            Log.Warn($"[{pluginName}] Use: REVERSE return|death|zone <name>");
            return;
        }

        var args = arg.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var reverseType = args.Length > 0 ? args[0].ToLowerInvariant() : "";

        if (reverseType == "return")
        {
            ScheduleOnce(500, () =>
            {
                if (UseReverseScroll(2))
                    Log.Info($"[{pluginName}] Using reverse to last return-scroll location");
                else
                    Log.Warn($"[{pluginName}] Failed to use reverse scroll (return)");
            });
        }
        else if (reverseType == "death")
        {
            ScheduleOnce(500, () =>
            {
                if (UseReverseScroll(3))
                    Log.Info($"[{pluginName}] Using reverse to last death location");
                else
                    Log.Warn($"[{pluginName}] Failed to use reverse scroll (death)");
            });
        }
        else if (reverseType == "zone")
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                Log.Warn($"[{pluginName}] Usage: REVERSE zone <name>");
            }
            else
            {
                var search = args[1].Trim();
                if (!TryResolveReverseZoneId(search, out var mapId, out var matchedLabel))
                {
                    Log.Warn($"[{pluginName}] Reverse zone not found: {search}");
                }
                else
                {
                    var resolvedMapId = mapId;
                    var resolvedLabel = matchedLabel;
                    ScheduleOnce(500, () =>
                    {
                        if (UseReverseScroll(7, resolvedMapId))
                            Log.Info($"[{pluginName}] Using reverse to zone '{resolvedLabel}'");
                        else
                            Log.Warn($"[{pluginName}] Failed to use reverse scroll (zone)");
                    });
                }
            }
        }
        else if (reverseType == "player")
        {
            Log.Warn($"[{pluginName}] REVERSE player is not implemented in vBot script API yet");
        }
        else
        {
            Log.Warn($"[{pluginName}] Unknown reverse type: {reverseType}");
        }
    }
    
    // STATUS
    else if (upperMsg == "STATUS")
    {
        var status = $"HP: {Player.HPPercent}%, MP: {Player.MPPercent}%, Monsters: {World.AliveMonsterCount}";
        Chat.Private(sender, status);
        Log.Info($"[{pluginName}] Sent status to {sender}");
    }
    else if (upperMsg == "INV")
    {
        var items = Inventory.GetAllItems().Count;
        var free = Inventory.FreeSlots;
        Chat.Private(sender, $"Inventory: {items} items, {free} free slots");
    }
    else if (upperMsg.StartsWith("USE "))
    {
        var itemName = msg.Substring(4).Trim();
        if (Inventory.UseItemByName(itemName))
        {
            Log.Info($"[{pluginName}] Used item: {itemName}");
        }
        else
        {
            Log.Warn($"[{pluginName}] Item not found: {itemName}");
        }
    }
    
    // POTIONS
    else if (upperMsg == "HP")
    {
        Player.UseHealthPotion();
        Log.Info($"[{pluginName}] Used HP potion");
    }
    else if (upperMsg == "MP")
    {
        Player.UseManaPotion();
        Log.Info($"[{pluginName}] Used MP potion");
    }
    
    // PARTY
    else if (upperMsg == "G" || upperMsg == "GETOUT")
    {
        Log.Info($"[{pluginName}] Leaving party...");
        Party.Leave();
    }
    else if (upperMsg == "DIS" || upperMsg == "DC")
    {
        Log.Info($"[{pluginName}] Disconnect requested by {sender}");
        Bot.Stop();
        try
        {
            vBot.Core.Kernel.Proxy?.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Warn($"[{pluginName}] Disconnect failed: {ex.Message}");
        }
    }
}

// ═══════════════════════════════════════════════════════════
// EVENTS
// ═══════════════════════════════════════════════════════════

OnJoinedGame += () =>
{
    LoadLeaders();
    RefreshLeaderList();
    _leadersVerified = true;
};

OnChatMessage += (type, sender, message) =>
{
    var isAllowedType = (type == 1 || type == 2 || type == 4 || type == 5 || type == 11);
    var isUnreadFallback = allowUnreadType0Commands && type == 0 && string.Equals(sender, "UNREAD", StringComparison.OrdinalIgnoreCase);

    // Chat types: 1=All, 2=Private, 4=Party, 5=Guild, 11=Union
    if (isAllowedType || isUnreadFallback)
    {
        var leaderSender = ResolveLeaderSender(sender);

        if (leaderSender != null)
        {
            HandleCommand(leaderSender, message);
        }
    }
};

OnTick += () =>
{
    // One-time verify: if script loaded after game join, OnJoinedGame won't fire.
    // Reload leaders once when player context is available.
    if (!_leadersVerified && GameInfo.IsReady && !string.IsNullOrEmpty(Player.Name))
    {
        LoadLeaders();
        RefreshLeaderList();
        _leadersVerified = true;

    }

    if (followEnabled && !string.IsNullOrEmpty(followPlayer))
    {
        var now = Environment.TickCount;
        if ((now - _lastFollowMoveTick) >= 500)
        {
            var target = World.GetPlayer(followPlayer);
            if (target != null)
            {
                var distance = GetDistance(Player.X, Player.Y, target.X, target.Y);

                if (distance > followDistance)
                {
                    var dx = target.X - Player.X;
                    var dy = target.Y - Player.Y;
                    var len = (float)distance;

                    var moveDistance = distance - followDistance;
                    var newX = Player.X + (dx / len) * (float)moveDistance;
                    var newY = Player.Y + (dy / len) * (float)moveDistance;

                    Player.MoveTo(newX, newY);
                    _lastFollowMoveTick = now;
                }
            }
        }
    }
};

OnFinished += () =>
{
    lock (scheduledTimers)
    {
        foreach (var timer in scheduledTimers)
            timer.Dispose();
        scheduledTimers.Clear();
    }

    SaveLeaders();
};
