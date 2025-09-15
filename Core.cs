using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Injection;
using SimpleJSON;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace TranslationHook
{
    // "Why version 2.4.0?" You might ask, as for that:
    // - 1.0.0 -> 1.7.25: MelonLoader (initial version, available in repo with a bunch of swears. Also, yeah, X.X.25 is a LOT of patches, and they were.)
    // - 2.0.0: Port to BepInEx (this file, available in the commit history, although it didn't work and it was 5 AM at the time, so I didn't even see what the bug said)
    // - 2.1.0: Fixing the logic to make it work. (Not a hotfix/patch, but literally changing a bunch of things)
    // - 2.2.0: Configuration (BepInEx/Config/TranslationSettings.json). Not only for the next update, but to allow more people who maybe want the game in their language to translate without issues.
    // - 2.3.0: Extra custom language (from config) in language dropdown + fallback in English. (So if you select, let's say, Korean, and then select the custom language, the default images and stuff are in English instead of Korean)
    // - 2.4.0: Restoring english language and backups, and stuff.
    [BepInPlugin("com.jair4x.translationhook", "TranslationHook", "2.4.0")]
    [BepInProcess("Peeping Dorm Manager.exe")]
    public class Core : BasePlugin
    {
        private new ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("TranslationHook");
        public static CoroutineHost? Host;

        public override void Load()
        {
            Log.LogInfo("[Pre-Hook] Initializing hook...");

            // Add a MonoBehaviour because we can't run Coroutines here otherwise
            ClassInjector.RegisterTypeInIl2Cpp<CoroutineHost>();
            try
            {
                Host = IL2CPPChainloader.AddUnityComponent<CoroutineHost>();
            }
            catch
            {
                var go = new GameObject("TranslationHookCoroutineHost");
                UnityEngine.Object.DontDestroyOnLoad(go);
                Host = go.AddComponent<CoroutineHost>();
            }

            Host.Init(Log, this);
            Host.StartReadinessChecker();
        }
    }

    public class CoroutineHost : MonoBehaviour
    {
        private ManualLogSource Log;
        private Core? Core; // Why this? Idk, just leave it there, it ain't gonna do anything bad.
        private TranslationSettings TLSettings;
        private Dictionary<string, Dictionary<string, string>> englishBackup = new Dictionary<string, Dictionary<string, string>>();

        public void Init(ManualLogSource Log, Core core)
        {
            this.Log = Log;
            this.Core = core;
            this.TLSettings = TranslationSettings.Load();
        }

        public void StartReadinessChecker()
        {
            this.StartCoroutine(GameReadinessChecker());
        }

        private IEnumerator GameReadinessChecker()
        {
            int attempts = 0;
            while (attempts < 60) // Try for 60 seconds max
            {
                attempts++;
                Log.LogInfo($"[Pre-Hook] Checking game readiness... attempt {attempts}");

                // Check if LocalizationSettings is actually available
                try
                {
                    if (LocalizationSettings.StringDatabase != null)
                    {
                        Log.LogInfo("[Pre-Hook] Game systems ready! Starting main coroutines...");
                        this.StartCoroutine(MainMenuWatcher());
                        //this.StartCoroutine(LocaleChangeMonitor());
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Log.LogInfo($"[Pre-Hook] Game not ready yet: {ex.Message}");
                }

                yield return new WaitForSeconds(1f);
            }

            Log.LogWarning("[Pre-Hook] Game readiness timeout - starting anyway...");
            this.StartCoroutine(MainMenuWatcher());
            //this.StartCoroutine(LocaleChangeMonitor());
        }

        public IEnumerator InjectorCoroutine()
        {
            Log.LogInfo("[InjectorCoroutine] Starting...");

            while (LocalizationSettings.StringDatabase == null)
            {
                Log.LogInfo("[InjectorCoroutine] StringDatabase is null...");
                yield return null; // null = just wait
            }

            Log.LogInfo("[InjectorCoroutine] Getting String Tables...");
            var handle = LocalizationSettings.StringDatabase.GetAllTables();

            while (!handle.IsDone)
                yield return null; // null = just wait

            if (!handle.IsDone || handle.Result == null)
            {
                Log.LogError("[InjectorCoroutine] Failed to load StringTables.");
                if (!handle.IsDone)
                {
                    Log.LogError("[InjectorCoroutine] handle wasn't done."); // It should be impossible to get here, but eh, who knows
                }
                else
                {
                    Log.LogError("[InjectorCoroutine] handle.Result was null.");
                }
                yield break;
            }

            Log.LogInfo("[InjectorCoroutine] String Tables found.");

            Log.LogInfo("[InjectorCoroutine] Checking if we have a backup of the English tables...");
            if (englishBackup == null || englishBackup.Count == 0)
            {
                Log.LogInfo("[InjectorCoroutine] No backup found, creating backup now...");
                yield return this.StartCoroutine(BackupEnglishTables());
            }
            else
            {
                Log.LogInfo("[InjectorCoroutine] Backup already present, skipping backup.");
            }

            this.StartCoroutine(AddCustomLocale(TLSettings.Language));

            Log.LogInfo("[InjectorCoroutine] Injecting strings...");
            string translationsDir = Path.Combine(Paths.PluginPath, "Translations");
            var tables = handle.Result;
            var index = 0;
            while (true)
            {
                try
                {
                    var table = tables[index];
                    var tableName = table.TableCollectionName;
                    var jsonPath = Path.Combine(translationsDir, $"{tableName}_{TLSettings.LanguageCode}.json");
                    var fileName = Path.GetFileName(jsonPath);

                    if (!File.Exists(jsonPath))
                    {
                        Log.LogWarning($"[InjectorCoroutine] {fileName} does not exist.");
                        index++;
                        continue;
                    }

                    string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                    var parsed = JSON.Parse(jsonContent);
                    if (parsed == null || !parsed.IsObject)
                    {
                        Log.LogError($"[InjectorCoroutine] {fileName} is corrupt or invalid.");
                        index++;
                        continue;
                    }

                    var tableEntries = table.m_TableEntries;
                    int count = 0;

                    foreach (var entry in tableEntries)
                    {
                        var keyIdStr = entry.Key.ToString();

                        if (parsed.HasKey(keyIdStr))
                        {
                            var newText = parsed[keyIdStr];
                            entry.Value.Value = newText;
                            count++;
                        }
                    }

                    index++;
                    continue;
                }
                catch
                {
                    Log.LogInfo("[Injector] Injected all available translations.");
                    break;
                }
            }
        }

        private IEnumerator MainMenuWatcher()
        {
            string lastScene = "";
            while (true)
            {
                yield return new WaitForSeconds(0.5f);

                string currentScene = SceneManager.GetActiveScene().name;
                if (currentScene == "MainMenu" && currentScene != lastScene)
                {
                    Log.LogInfo("[MainMenuWatcher] Main Menu loaded, ensuring custom locale exists...");
                    yield return this.StartCoroutine(EnsureCustomLocale());
                }

                lastScene = currentScene;
            }
        }

        private IEnumerator EnsureCustomLocale()
        {
            Log.LogInfo("[CustomLocale] Waiting for Main Menu...");
            while (SceneManager.GetActiveScene().name != "MainMenu")
                yield return new WaitForSeconds(0.5f);

            var dropdown = FindObjectOfType<TMP_Dropdown>();
            if (dropdown == null) yield break;

            var callbacks = dropdown.onValueChanged;
            dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent(); // Disable dropdown events

            // Add custom option if it doesn't exist
            bool exists = false;
            for (int i = 0; i < dropdown.options.Count; i++)
                if (dropdown.options[i].text == TLSettings.Language)
                    exists = true;

            if (!exists)
            {
                dropdown.options.Add(new TMP_Dropdown.OptionData(TLSettings.Language));
                dropdown.RefreshShownValue();
                Log.LogInfo("[CustomLocale] Added custom language option.");
            }

            // Select last saved language
            int idx = 0;
            for (int i = 0; i < dropdown.options.Count; i++)
                if (dropdown.options[i].text == TLSettings.SelectedLanguage)
                    idx = i;

            dropdown.value = idx;
            dropdown.RefreshShownValue();
            dropdown.onValueChanged = callbacks; // Restore dropdown events

            // If the last saved language is our custom one, inject translations
            if (TLSettings.SelectedLanguage == TLSettings.Language)
            {
                Log.LogInfo("[CustomLocale] Applying custom language...");
                yield return this.StartCoroutine(SelectEnglishLocale());
                yield return this.StartCoroutine(InjectorCoroutine());

                // Reload strings
                yield return this.StartCoroutine(ReloadAllLocalizedStrings());
            }

            // Listen in on the language dropdown for language changes
            this.StartCoroutine(DropdownMonitor(dropdown));
        }

        private IEnumerator DropdownMonitor(TMP_Dropdown dropdown)
        {
            string? lastValue = null;

            while (true)
            {
                yield return new WaitForSeconds(0.5f);

                if (dropdown == null || dropdown.options.Count == 0) continue;

                string currentValue = dropdown.options[dropdown.value].text;

                if (currentValue != lastValue)
                {
                    lastValue = currentValue;
                    TLSettings.SelectedLanguage = currentValue;
                    TLSettings.Save();

                    if (currentValue == TLSettings.Language)
                    {
                        Log.LogInfo("[CustomLocale] Custom language selected. Applying translations...");

                        // Force English as Callback
                        yield return this.StartCoroutine(SelectEnglishLocale());

                        // Hide the dropdown 
                        dropdown.Hide();

                        // Inject translation
                        yield return this.StartCoroutine(InjectorCoroutine());

                        // Reload strings
                        yield return this.StartCoroutine(ReloadAllLocalizedStrings());
                    }
                    else if (currentValue == "English")
                    {
                        if (englishBackup == null || englishBackup.Count == 0)
                        {
                            // Create a backup if we don't have it.
                            this.StartCoroutine(BackupEnglishTables());
                        }
                        else
                        {
                            // Restore the original english lines (since, you know, we use it as fallback and inject our custom language on top)
                            this.StartCoroutine(RestoreEnglishTables());
                        }

                        // Reload strings
                        yield return this.StartCoroutine(ReloadAllLocalizedStrings());
                    }
                }
            }
        }

        public IEnumerator ReloadAllLocalizedStrings()
        {
            Log.LogInfo("[ReloadAllLocalizedStrings] Starting reload...");

            // 1. All LocalizeStringEvent components first (these are bound to UI)
            var allEvents = FindObjectsOfType<LocalizeStringEvent>(true);
            foreach (var evt in allEvents)
            {
                evt.RefreshString();
            }

            // 2. Standalone LocalizedString objects not bound to UI
            var allMonoBehaviours = FindObjectsOfType<MonoBehaviour>(true);
            foreach (var mb in allMonoBehaviours)
            {
                var fields = mb.GetType().GetFields(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance
                );

                foreach (var field in fields)
                {
                    if (typeof(LocalizedString).IsAssignableFrom(field.FieldType))
                    {
                        var localizedString = field.GetValue(mb) as LocalizedString;
                        if (localizedString != null)
                        {
                            localizedString.RefreshString();
                        }
                    }
                }
            }

            yield return null;
            Log.LogInfo("[ReloadAllLocalizedStrings] Reload complete.");
        }

        private IEnumerator BackupEnglishTables()
        {
            Log.LogInfo("[BackupEnglishTables] Starting backup...");

            while (LocalizationSettings.StringDatabase == null)
            {
                Log.LogInfo("[BackupEnglishTables] StringDatabase is null...");
                yield return null;
            }

            var handle = LocalizationSettings.StringDatabase.GetAllTables();
            while (!handle.IsDone)
                yield return null;

            if (!handle.IsDone || handle.Result == null)
            {
                Log.LogError("[BackupEnglishTables] Failed to load StringTables.");
                yield break;
            }

            var tables = handle.Result;
            englishBackup = new Dictionary<string, Dictionary<string, string>>();

            int index = 0;
            while (true)
            {
                try
                {
                    var table = tables[index];
                    var tableName = table.TableCollectionName;

                    var tableEntries = table.m_TableEntries;
                    var tableDict = new Dictionary<string, string>();

                    foreach (var entry in tableEntries)
                    {
                        var keyIdStr = entry.Key.ToString();

                        object rawVal = entry.Value;
                        string text;

                        // If it's a string (unlikely, but whatever)
                        if (rawVal is string s)
                        {
                            text = s;
                        }
                        else
                        {
                            // StringTableEntry or whatever has a .Value property
                            var valProp = rawVal?.GetType().GetProperty("Value");
                            if (valProp != null)
                                text = valProp.GetValue(rawVal)?.ToString() ?? "";
                            else
                                text = rawVal?.ToString() ?? "";
                        }

                        tableDict[keyIdStr] = text;
                    }

                    englishBackup[tableName] = tableDict;

                    Log.LogInfo($"[BackupEnglishTables] Backed up table '{tableName}' ({tableDict.Count} entries).");

                    index++;
                    continue;
                }
                catch
                {
                    Log.LogInfo("[BackupEnglishTables] Backed up all available tables.");
                    break;
                }
            }
        }

        private IEnumerator RestoreEnglishTables()
        {
            Log.LogInfo("[RestoreEnglishTables] Restoring from backup...");

            if (englishBackup == null || englishBackup.Count == 0)
            {
                Log.LogWarning("[RestoreEnglishTables] No backup found, aborting...");
                yield break;
            }

            while (LocalizationSettings.StringDatabase == null)
            {
                Log.LogInfo("[RestoreEnglishTables] StringDatabase is null...");
                yield return null;
            }

            var handle = LocalizationSettings.StringDatabase.GetAllTables();
            while (!handle.IsDone)
                yield return null;

            if (!handle.IsDone || handle.Result == null)
            {
                Log.LogError("[RestoreEnglishTables] Failed to load StringTables.");
                yield break;
            }

            var tables = handle.Result;
            int index = 0;

            while (true)
            {
                try
                {
                    var table = tables[index];
                    var tableName = table.TableCollectionName;

                    if (!englishBackup.TryGetValue(tableName, out var tableDict))
                    {
                        index++;
                        continue;
                    }

                    var tableEntries = table.m_TableEntries;
                    int restoredCount = 0;

                    foreach (var kvp in tableEntries)
                    {
                        var keyIdStr = kvp.Key.ToString();

                        if (!tableDict.TryGetValue(keyIdStr, out var originalText))
                            continue;

                        var entryObj = kvp.Value; // entryObj = StringTableEntry
                        if (entryObj == null)
                            continue;

                        var valProp = entryObj.GetType().GetProperty("Value");
                        if (valProp != null)
                        {
                            valProp.SetValue(entryObj, originalText);
                            restoredCount++;
                        }
                    }

                    Log.LogInfo($"[RestoreEnglishTables] Restored {restoredCount} entries in table '{tableName}'.");
                    index++;
                    continue;
                }
                catch
                {
                    Log.LogInfo("[RestoreEnglishTables] Restored all available tables.");
                    break;
                }
            }

            // Refresh localized strings after restore
            yield return this.StartCoroutine(ReloadAllLocalizedStrings());

            Log.LogInfo("[RestoreEnglishTables] Restore complete.");
        }

        private IEnumerator LocaleChangeMonitor()
        {
            Log.LogInfo("[LocaleChangeMonitor] Starting...");

            string? lastDropdownValue = null;

            while (true)
            {
                yield return new WaitForSeconds(0.5f);

                var dropdown = FindObjectOfType<TMP_Dropdown>();
                if (dropdown == null || dropdown.options.Count == 0 || dropdown.value >= dropdown.options.Count)
                    continue; // Wait for the next frame

                string currentValue = dropdown.options[dropdown.value].text;

                if (currentValue == TLSettings.Language)
                {
                    // Change into the english locale for fallback
                    dropdown.Hide();
                    this.StartCoroutine(SelectEnglishLocale());

                    // Inject translation
                    Log.LogInfo("[LocaleChangeMonitor] Injecting translations for custom language...");
                    yield return this.StartCoroutine(InjectorCoroutine());
                    break;
                }

                if (currentValue != lastDropdownValue)
                {
                    Log.LogInfo($"[LocaleChangeMonitor] Dropdown changed to {currentValue}");

                    if (currentValue == TLSettings.Language)
                    {
                        // Change into the english locale for fallback
                        dropdown.Hide();
                        this.StartCoroutine(SelectEnglishLocale());

                        // Inject translation
                        Log.LogInfo("[LocaleChangeMonitor] Injecting translations for custom language...");
                        yield return this.StartCoroutine(InjectorCoroutine());
                    }

                    lastDropdownValue = currentValue;
                    TLSettings.SelectedLanguage = currentValue;
                    TLSettings.Save();
                }

            }
        }

        public IEnumerator AddCustomLocale(string LangName)
        {
            Log.LogInfo("[AddCustomLocale] Waiting for main menu...");

            while (SceneManager.GetActiveScene().name != "MainMenu")
                yield return new WaitForSeconds(0.5f);

            var dropdown = FindObjectOfType<TMP_Dropdown>();
            if (dropdown == null)
            {
                Log.LogWarning("[AddCustomLocale] Couldn't find Language Dropdown.");
                yield break;
            }

            // Check if the option exists
            bool exists = false;
            for (int i = 0; i < dropdown.options.Count; i++)
            {
                if (dropdown.options[i].text == LangName)
                {
                    exists = true;
                    break;
                }
            }

            // If it doesn't, create it.
            if (!exists)
            {
                dropdown.options.Add(new TMP_Dropdown.OptionData(LangName));
                dropdown.RefreshShownValue();
            }

            Log.LogInfo($"[AddCustomLocale] Ensured dropdown has {LangName}");
        }

        public IEnumerator SelectEnglishLocale()
        {
            Log.LogInfo("[AutoLocale] Selecting english as locale...");

            var locales = LocalizationSettings.AvailableLocales.Locales;

            foreach (var loc in locales)
            {
                if (loc.Identifier.Code == "en")
                {
                    LocalizationSettings.SelectedLocale = loc;
                    Log.LogInfo("[AutoLocale] Selected English locale.");
                    break;
                }
            }
            yield break;
        }
    }
    
    // Load config stuff.
    // To know what this is, go and check BepInEx/Config/TranslationSettings.json
    public class TranslationSettings
    {
        // Get values, defaults are for Spanish translation
        public string Language { get; set; } = "Español";
        public string LanguageCode { get; set; } = "es";
        public string SelectedLanguage { get; set; } = "en";

        private static string ConfigPath => Path.Combine(Paths.ConfigPath, "TranslationSettings.json");

        // Load config from JSON file (duh)
        public static TranslationSettings Load()
        {
            if (!File.Exists(ConfigPath))
            {
                var defaults = new TranslationSettings();
                defaults.Save();
                return defaults;
            }
                
            try
            {
                string json = File.ReadAllText(ConfigPath, System.Text.Encoding.UTF8);
                var parsed = JSON.Parse(json);
                if (parsed == null) return new TranslationSettings();

                return new TranslationSettings
                {
                    Language = parsed["language"] ?? "Español",
                    LanguageCode = parsed["languageCode"] ?? "es",
                    SelectedLanguage = parsed["selectedLanguage"] ?? "en"
                };
            }
            catch
            {
                return new TranslationSettings();
            }
        }

        // Save config (again, duh)
        public void Save()
        {
            var jsonObj = new JSONObject
            {
                ["language"] = Language,
                ["languageCode"] = LanguageCode,
                ["selectedLanguage"] = SelectedLanguage
            };

            File.WriteAllText(ConfigPath, jsonObj.ToString(), System.Text.Encoding.UTF8);
        }
    }

}
