// =============================================================
// xAutoConfig - Default profile copier for vBot Custom Plugins
// =============================================================
// Copies Default*.json and script_*_Default.json files to the
// current character when the character enters the game.
// =============================================================
// Version: 1.0.0
// =============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

var pluginName = "xAutoConfig";
var pluginVersion = "1.0.0";

var panel = GUI.CreatePanel(pluginName, "Default profile copier for the current character");
Label lblStatus = null;
Label lblCharacter = null;
Label lblProfile = null;

Log.Info($"[{pluginName}] v{pluginVersion} loaded");

void UpdateStatusLabel(string message)
{
    GUI.BeginInvoke(() =>
    {
        if (lblStatus != null && !lblStatus.IsDisposed)
            lblStatus.Text = "Status: " + message;
    });
}

void RefreshPanelContext()
{
    var character = GetCharacterName();
    var profile = GetProfileName();

    GUI.BeginInvoke(() =>
    {
        if (lblCharacter != null && !lblCharacter.IsDisposed)
            lblCharacter.Text = "Character: " + (string.IsNullOrWhiteSpace(character) ? "Waiting for game join" : character);

        if (lblProfile != null && !lblProfile.IsDisposed)
            lblProfile.Text = "Profile: " + profile;
    });
}

string SafeFilePart(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

    foreach (var invalid in Path.GetInvalidFileNameChars())
        value = value.Replace(invalid, '_');

    return value.Trim();
}

string GetCharacterName()
{
    return SafeFilePart(Player.Name);
}

string GetProfileName()
{
    var profile = SafeFilePart(Bot.CurrentProfile);
    return string.IsNullOrWhiteSpace(profile) ? "Default" : profile;
}

string GetProfileCharacterPrefix()
{
    var character = GetCharacterName();
    if (string.IsNullOrWhiteSpace(character))
        return string.Empty;

    return GetProfileName() + "_" + character;
}

void CopyDefaultFile(string source, string target, string message)
{
    try
    {
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.Copy(source, target, true);
        Log.Info($"[{pluginName}] {message}: {Path.GetFileName(target)}");
        UpdateStatusLabel(message);
    }
    catch (Exception ex)
    {
        Log.Error($"[{pluginName}] Failed to copy {Path.GetFileName(source)}: {ex.Message}");
        UpdateStatusLabel("Copy failed: " + Path.GetFileName(source));
    }
}

bool IsDefaultProfileJsonFile(string fileName)
{
    if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        return false;

    if (!fileName.StartsWith("Default", StringComparison.OrdinalIgnoreCase))
        return false;

    var suffix = fileName.Substring("Default".Length);
    if (suffix.Equals(".json", StringComparison.OrdinalIgnoreCase))
        return true;

    return suffix.Length > ".json".Length
        && suffix.StartsWith(".", StringComparison.Ordinal)
        && suffix.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
}

bool TryGetScriptDefaultPrefix(string fileName, out string prefix)
{
    prefix = string.Empty;
    if (string.IsNullOrWhiteSpace(fileName))
        return false;

    const string requiredPrefix = "script_";
    const string defaultSuffix = "_default.json";

    if (!fileName.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase)
        || !fileName.EndsWith(defaultSuffix, StringComparison.OrdinalIgnoreCase))
        return false;

    var prefixLength = fileName.Length - defaultSuffix.Length;
    if (prefixLength <= requiredPrefix.Length)
        return false;

    prefix = fileName.Substring(0, prefixLength);
    return true;
}

void CopyProfileJsonDefaults(string configDir, string profileCharacterPrefix)
{
    var defaults = Directory.EnumerateFiles(configDir, "*.json")
        .Where(path => IsDefaultProfileJsonFile(Path.GetFileName(path)))
        .ToList();

    foreach (var source in defaults)
    {
        var fileName = Path.GetFileName(source);
        var suffix = fileName.Substring("Default".Length);
        var target = Path.Combine(configDir, profileCharacterPrefix + suffix);

        if (!File.Exists(target))
            CopyDefaultFile(source, target, "Default profile loaded");
    }
}

void CopyScriptJsonDefaults(string configDir)
{
    var character = GetCharacterName();
    if (string.IsNullOrWhiteSpace(character))
        return;

    var defaults = Directory.EnumerateFiles(configDir, "script_*_Default.json")
        .Concat(Directory.EnumerateFiles(configDir, "script_*_default.json"))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    foreach (var source in defaults)
    {
        var fileName = Path.GetFileName(source);
        if (!TryGetScriptDefaultPrefix(fileName, out var prefix))
            continue;

        var target = Path.Combine(configDir, prefix + "_" + character + ".json");
        if (!File.Exists(target))
            CopyDefaultFile(source, target, "Default script config loaded");
    }
}

void ApplyAutoConfig()
{
    var configDir = Config.ConfigDirectory;
    Directory.CreateDirectory(configDir);
    RefreshPanelContext();

    var profileCharacterPrefix = GetProfileCharacterPrefix();
    if (string.IsNullOrWhiteSpace(profileCharacterPrefix))
    {
        Log.Warn($"[{pluginName}] Character name is not available yet; skipping default config copy.");
        UpdateStatusLabel("Waiting for character name");
        return;
    }

    UpdateStatusLabel("Applying defaults...");
    CopyProfileJsonDefaults(configDir, profileCharacterPrefix);
    CopyScriptJsonDefaults(configDir);
    UpdateStatusLabel("Defaults checked");
}

lblStatus = panel.CreateLabel("Status: Loaded", 10, 10, 640);
lblCharacter = panel.CreateLabel("Character: Waiting for game join", 10, 35, 640);
lblProfile = panel.CreateLabel("Profile: Default", 10, 60, 640);
panel.CreateLabel("Copies Default*.json and script_*_default.json on first game join.", 10, 90, 640);
panel.CreateButton("Apply Now", 10, 120, 100, 28, () =>
{
    UpdateStatusLabel("Manual apply started");
    Task.Run(() => ApplyAutoConfig());
});
RefreshPanelContext();

OnJoinedGame += ApplyAutoConfig;

OnFinished += () =>
{
    Log.Info($"[{pluginName}] unloaded");
};
