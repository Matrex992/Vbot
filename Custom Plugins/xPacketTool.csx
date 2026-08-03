// xPacketTool.csx - vBot Custom Plugin (C# Script)
// Purpose: Packet inject + live packet logger with filters.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

ushort ParseOpcode(string text)
{
    if (string.IsNullOrWhiteSpace(text)) throw new Exception("Opcode is empty");
    text = text.Trim();
    if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        text = text.Substring(2);

    if (!ushort.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var opcode))
        throw new Exception("Invalid opcode. Use hex like 7025 or 0x7025");

    return opcode;
}

byte[] ParseHexBytes(string text)
{
    if (string.IsNullOrWhiteSpace(text))
        return Array.Empty<byte>();

    var cleaned = new string(text.Where(Uri.IsHexDigit).ToArray());
    if (cleaned.Length % 2 != 0)
        throw new Exception("Hex bytes must have even length");

    var bytes = new byte[cleaned.Length / 2];
    for (int i = 0; i < bytes.Length; i++)
        bytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
    return bytes;
}

string HexDump(byte[] data)
{
    if (data == null || data.Length == 0) return "None";
    return string.Join(" ", data.Select(b => b.ToString("X2")));
}

// ===== UI =====
var panel = GUI.CreatePanel("xPacketTool", "Inject packets and inspect traffic");

// Global UI Vars
TextBox txtOpcode = null;
TextBox txtData = null;
CheckBox chkEncrypted = null;
CheckBox chkShowClient = null;
CheckBox chkShowServer = null;
RadioButton chkDontShow = null;
RadioButton chkOnlyShow = null;
TextBox txtFilterOpcode = null;
ListBox lstOpcodes = null;

GUI.Invoke(() => 
{
    panel.Container.BackColor = Color.WhiteSmoke;

    // 1. Packet Injector Group
    var gbInjector = new GroupBox();
    gbInjector.Text = "Packet Injector";
    gbInjector.Location = new Point(10, 10);
    gbInjector.Size = new Size(460, 150);
    gbInjector.Font = new Font("Segoe UI", 9f);
    panel.Container.Controls.Add(gbInjector);

    var lblOpcode = new Label();
    lblOpcode.Text = "Opcode (Hex):";
    lblOpcode.AutoSize = true;
    lblOpcode.Location = new Point(15, 25);
    gbInjector.Controls.Add(lblOpcode);

    txtOpcode = new TextBox();
    txtOpcode.Location = new Point(15, 45);
    txtOpcode.Size = new Size(80, 23);
    txtOpcode.MaxLength = 6;
    gbInjector.Controls.Add(txtOpcode);

    chkEncrypted = new CheckBox();
    chkEncrypted.Text = "Encrypted";
    chkEncrypted.Location = new Point(110, 47);
    chkEncrypted.AutoSize = true;
    gbInjector.Controls.Add(chkEncrypted);

    var lblData = new Label();
    lblData.Text = "Data (Hex Bytes):";
    lblData.AutoSize = true;
    lblData.Location = new Point(15, 80);
    gbInjector.Controls.Add(lblData);

    txtData = new TextBox();
    txtData.Location = new Point(15, 100);
    txtData.Size = new Size(430, 23);
    gbInjector.Controls.Add(txtData);

    var btnInjectClient = new Button();
    btnInjectClient.Text = "Inject to Client";
    btnInjectClient.Location = new Point(220, 35);
    btnInjectClient.Size = new Size(100, 30);
    btnInjectClient.UseVisualStyleBackColor = true;
    gbInjector.Controls.Add(btnInjectClient);

    var btnInjectServer = new Button();
    btnInjectServer.Text = "Inject to Server";
    btnInjectServer.Location = new Point(330, 35);
    btnInjectServer.Size = new Size(100, 30);
    btnInjectServer.UseVisualStyleBackColor = true;
    gbInjector.Controls.Add(btnInjectServer);


    // 2. Traffic Analysis Group (Left Bottom)
    var gbAnalysis = new GroupBox();
    gbAnalysis.Text = "Traffic Analysis";
    gbAnalysis.Location = new Point(10, 170);
    gbAnalysis.Size = new Size(460, 150);
    gbAnalysis.Font = new Font("Segoe UI", 9f);
    panel.Container.Controls.Add(gbAnalysis);

    chkShowClient = new CheckBox();
    chkShowClient.Text = "Show Client Packets [C->S]";
    chkShowClient.Location = new Point(15, 30);
    chkShowClient.AutoSize = true;
    gbAnalysis.Controls.Add(chkShowClient);

    chkShowServer = new CheckBox();
    chkShowServer.Text = "Show Server Packets [S->C]";
    chkShowServer.Location = new Point(15, 55);
    chkShowServer.AutoSize = true;
    gbAnalysis.Controls.Add(chkShowServer);

    var panelFilterMode = new Panel();
    panelFilterMode.Location = new Point(15, 90);
    panelFilterMode.Size = new Size(200, 50);
    gbAnalysis.Controls.Add(panelFilterMode);

    chkDontShow = new RadioButton();
    chkDontShow.Text = "Blacklist (Don't Show)";
    chkDontShow.Location = new Point(0, 0);
    chkDontShow.AutoSize = true;
    chkDontShow.Checked = true;
    panelFilterMode.Controls.Add(chkDontShow);

    chkOnlyShow = new RadioButton();
    chkOnlyShow.Text = "Whitelist (Only Show)";
    chkOnlyShow.Location = new Point(0, 25);
    chkOnlyShow.AutoSize = true;
    panelFilterMode.Controls.Add(chkOnlyShow);


    // 3. Opcode Filter List Group (Right Side)
    var gbFilters = new GroupBox();
    gbFilters.Text = "Opcode Filter List";
    gbFilters.Location = new Point(480, 10);
    gbFilters.Size = new Size(280, 310);
    gbFilters.Font = new Font("Segoe UI", 9f);
    panel.Container.Controls.Add(gbFilters);

    var lblFilter = new Label();
    lblFilter.Text = "Opcode (Hex):"; // Simplified label
    lblFilter.Location = new Point(15, 25);
    lblFilter.AutoSize = true;
    gbFilters.Controls.Add(lblFilter);

    txtFilterOpcode = new TextBox();
    txtFilterOpcode.Location = new Point(15, 45);
    txtFilterOpcode.Size = new Size(100, 23);
    txtFilterOpcode.MaxLength = 6;
    gbFilters.Controls.Add(txtFilterOpcode);

    var btnAddOpcode = new Button();
    btnAddOpcode.Text = "Add";
    btnAddOpcode.Location = new Point(125, 44);
    btnAddOpcode.Size = new Size(60, 25);
    btnAddOpcode.UseVisualStyleBackColor = true;
    gbFilters.Controls.Add(btnAddOpcode);

    lstOpcodes = new ListBox();
    lstOpcodes.Location = new Point(15, 80);
    lstOpcodes.Size = new Size(250, 180);
    gbFilters.Controls.Add(lstOpcodes);

    var btnRemOpcode = new Button();
    btnRemOpcode.Text = "Remove Selected";
    btnRemOpcode.Location = new Point(15, 270);
    btnRemOpcode.Size = new Size(250, 25);
    btnRemOpcode.UseVisualStyleBackColor = true;
    gbFilters.Controls.Add(btnRemOpcode);

    // Events
    btnAddOpcode.Click += (s, e) =>
    {
        try
        {
            var opcode = ParseOpcode(txtFilterOpcode.Text);
            AddFilterOpcode(opcode);
            GUI.BeginInvoke(() => txtFilterOpcode.Text = "");
        }
        catch (Exception ex)
        {
            Log.Error($"xPacketTool: {ex.Message}");
        }
    };

    btnRemOpcode.Click += (s, e) => RemoveSelectedFilterOpcode();

    btnInjectClient.Click += (s, e) => Inject(toClient: true);
    btnInjectServer.Click += (s, e) => Inject(toClient: false);

    // Make radio-like behavior (Native RadioButton handles this automatically within the panel container, no events needed)
    // But we need to update our internal logic toggles if we were using them?
    // Using RadioButton properties directly in DontShowMode() is better.
});


// ===== State =====
var filteredOpcodes = new HashSet<ushort>();
bool DontShowMode()
{
    // Need UI thread access?
    // Yes, Checked property access should be on UI thread or Invoke.
    bool val = true;
    GUI.Invoke(() => val = chkDontShow.Checked);
    return val;
}

void SetModeDontShow(bool dontShow)
{
    GUI.BeginInvoke(() =>
    {
        chkDontShow.Checked = dontShow;
        chkOnlyShow.Checked = !dontShow;
    });
}

// Helper to check filter (Radio buttons handle toggling automatically)
bool CanShow(ushort opcode)
{
    var inList = filteredOpcodes.Contains(opcode);
    if (DontShowMode())
        return !inList; // Blacklist: Show if NOT in list
    return inList;      // Whitelist: Show ONLY if in list
}

void AddFilterOpcode(ushort opcode)
{
    if (filteredOpcodes.Add(opcode))
    {
        GUI.BeginInvoke(() =>
        {
            if (lstOpcodes != null)
                lstOpcodes.Items.Add($"0x{opcode:X4}");
        });
        Log.Info($"xPacketTool: Added opcode [0x{opcode:X4}]");
    }
}

void RemoveSelectedFilterOpcode()
{
    GUI.BeginInvoke(() =>
    {
        if (lstOpcodes.SelectedItem == null) return;
        var text = lstOpcodes.SelectedItem.ToString();
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            var opcode = ParseOpcode(text);
            filteredOpcodes.Remove(opcode);
            lstOpcodes.Items.Remove(lstOpcodes.SelectedItem);
            Log.Info($"xPacketTool: Removed opcode [0x{opcode:X4}]");
        }
        catch { }
    });
}

void Inject(bool toClient)
{
    try
    {
        string opText = "";
        string dataText = "";
        bool encrypted = false;
        
        GUI.Invoke(() => {
            opText = txtOpcode.Text ?? "";
            dataText = txtData.Text ?? "";
            encrypted = chkEncrypted.Checked;
        });

        var opcode = ParseOpcode(opText);
        var bytes = ParseHexBytes(dataText);

        if (toClient)
        {
            Packets.SendToClient(opcode, bytes, encrypted);
            Log.Info($"xPacketTool: Inject ToClient {(encrypted ? "(Encrypted) " : "")}0x{opcode:X4} (Data) {HexDump(bytes)}");
        }
        else
        {
            Packets.SendToServer(opcode, bytes, encrypted);
            Log.Info($"xPacketTool: Inject ToServer {(encrypted ? "(Encrypted) " : "")}0x{opcode:X4} (Data) {HexDump(bytes)}");
        }
    }
    catch (Exception ex)
    {
        Log.Error($"xPacketTool: {ex.Message}");
    }
}


// ===== Packet listeners (wildcard) =====
string anyClientId = null;
string anyServerId = null;

anyClientId = Packets.OnAnyClientPacket((opcode, data) =>
{
    // Check Checked property safely
    bool show = false;
    GUI.Invoke(() => show = chkShowClient != null && chkShowClient.Checked);
    
    if (!show) return;
    if (!CanShow(opcode)) return;
    Log.Info($"Client: (Opcode) 0x{opcode:X4} (Data) {HexDump(data)}");
});

anyServerId = Packets.OnAnyServerPacket((opcode, data) =>
{
    bool show = false;
    GUI.Invoke(() => show = chkShowServer != null && chkShowServer.Checked);

    if (!show) return;
    if (!CanShow(opcode)) return;
    Log.Info($"Server: (Opcode) 0x{opcode:X4} (Data) {HexDump(data)}");
});

OnFinished += () =>
{
    try
    {
        if (!string.IsNullOrWhiteSpace(anyClientId)) Packets.RemoveHandler(anyClientId);
        if (!string.IsNullOrWhiteSpace(anyServerId)) Packets.RemoveHandler(anyServerId);
    }
    catch { }
};

Log.Info("xPacketTool loaded. Open Custom Plugins → xPacketTool");
