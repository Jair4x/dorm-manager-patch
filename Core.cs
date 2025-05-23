using MelonLoader;
using UnityEngine;
using HarmonyLib;
using System.Collections;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using MelonLoader.Utils;
using SimpleJSON;
using System.Reflection;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Il2CppTMPro;

[assembly: MelonInfo(typeof(TranslationHook.Core), "TranslationHook", "1.7.25", "Jair4x", null)]
[assembly: MelonGame("Horny Doge", "Peeping Dorm Manager")]

/* 
 * [!] THIS CODE CONTAINS WHAT YOU MIGHT CONSIDER AS "BAD WORDS". But after suffering this much with this, I don't care.   [!]
 * [!] AND IT (probably) SUCKS AND YOU MIGHT NOT EVEN UNDERSTAND IT.                                                       [!]
 * [!] Also thanks for reading this, I guess.                                                                              [!]
 * 
 * [?] "What am I doing here? What is this?" [?]
 * 
 * This code needs some technical explanation, since the comments don't give full context of this.
 * 
 * First, this is a translation mod I made for Peeping Dorm Manager, one of my many translation projects.
 * This code is for MelonLoader 0.6.6, because UnityExplorer won't work with any other version, somehow.
 * I used UnityExplorer to see the scene names, the game assets and scene structure. You might want to do it too if you plan to work on a game like this.
 * 
 * Since I never worked with Unity like this before, I thought I'd give it a try.
 * Saying that this took years of my life is an understatement, but I think it paid off. Who knows, maybe this'll be useful for someone else.
 * 
 * The game's translation system is Unity.Localization, which is a bit of a pain to work with.
 * More so, it uses IL2CPP, which hardens reverse engineering, so we can't just use normal C# reflection.
 *
 * Meaning: I worked my ASS off to make this work, whatever means possible.
 * Even if I quite literally patch EVERY SINGLE LOCALIZATION METHOD. I'll make it work.
 * 
 * So, in the end, did I really patch every localization method Unity has? Yes, but... 
 * I didn't end up using it since I got stuck in a situation where the game wouldn't recognise the variables and therefore couldn't format them,-
 * - so I went with what I already had: Replace the strings of the English locale with the translated ones. A classic in Fan Translation.
 * 
 * Might re-take that approach to see if I can find a fix to that problem in the future, but in the meantime, enjoy having your strings replaced.
 * 
 * Took me about a week or two[*] to work on this, without even taking into account extracting assets, working on Unity 2021.3.38f1 to see if I could just-
 * - add assets into the game bundles (turns out, I need the whole game for that, and I do NOT have enough time to remake the ENTIRE game structure JUST to-
 * - add a language).
 * 
 * Oh, by the way, some "for" and "foreach" have been changed with absolute "while" war crimes because YEAH, THANKS IL2CPP FOR NOT LETTING ME ENUMERATE SHIT.
 * 
 * [*] Week as in "168 hours of pure pain", not as in "haha, 2 hours per day for a week". IT'S BEEN MONTHS. MONTHS OF ME.
 * AND THAT'S WITHOUT COUNTING THE TRANSLATION ITSELF AND IMAGE STUFF.
 * 
 * Seriously, FUCK IL2CPP, I hope I don't have to suffer through this anymore.
 */
namespace TranslationHook
{
    public class Core : MelonMod
    {
        // Not used anymore, used for LoadTranslations().
        // Dictionary that represents a StringTable
        public static Dictionary<string, Dictionary<string, string>> translatedTables = new();

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Hook initialized.");

            // As the name implies, selecting the english locale when booting up the game.
            MelonCoroutines.Start(SelectEnglishLocale());

            // To inject translations when we re-select the english locale.
            // When changing to the English locale again, the main menu is reloaded, hence why the options menu is closed.
            MelonCoroutines.Start(MonitorLocaleChanges());

            // Change the strings for the translated ones on boot.
            // Cheap, because I replace a language, but eh, you're gonna play it anyway, probably.
            MelonCoroutines.Start(InjectorCoroutine());

            /* 
             * [!] Unused functions/coroutines, I just gave up on creating a new language to work on it.                [!]
             * [!] Maybe I'll work on it some day, just not now. I just want to be able to work on the translation-     [!]
             * [!] - and export all this stuff...                                                                       [!]
             */

            // Add 'es' locale to the game.
            // If you want to change a locale, just go to this Coroutine's code and change it.
            //MelonCoroutines.Start(AddCustomLocale());

            // Load the plain .json files for the translations and load them into translatedTables (the dictionary above).
            // filenames (*_es.json) are hardcoded, but you can change that if you're working with another language.
            //MelonCoroutines.Start(LoadTranslations());

            // Fuck IL2CPP, we're patching EVERYTHING.
            // Warcrimes were commited in the name of translating are in this function.
            //ApplyAggressiveAhhPatches();
        }

        private IEnumerator MonitorLocaleChanges()
        {
            yield return LocalizationSettings.InitializationOperation;

            Locale locale = LocalizationSettings.SelectedLocale;

            // Periodically check for locale changes
            while (true)
            {
                yield return new WaitForSeconds(0.5f); // Check every half second

                Locale currentLocale = LocalizationSettings.SelectedLocale;

                if (currentLocale != locale)
                {
                    MelonLogger.Msg($"[Locale] Changed to {currentLocale?.Identifier.Code ?? "null"}");

                    if (currentLocale != null && currentLocale.Identifier.Code == "en")
                    {
                        MelonLogger.Msg($"[Locale] Injecting...");
                        yield return MelonCoroutines.Start(InjectorCoroutine());

                        // Reload the Main menu (only way I could think to reload all strings)
                        // This closes the options menu, nothing I can do about it.
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

                        // Wait for the scene to reload and change the Locale name again
                        yield return new WaitForSeconds(0.5f); // Kinda dirty, but what can I do?
                        MelonCoroutines.Start(ChangeLocaleName("Español"));
                    }

                    locale = currentLocale;
                }
            }
        }

        private static IEnumerator InjectorCoroutine()
        {
            yield return LocalizationSettings.InitializationOperation;

            var stringDatabase = LocalizationSettings.StringDatabase;
            var handle = stringDatabase.GetAllTables();
            yield return handle;

            if (!handle.IsDone || handle.Result == null)
            {
                MelonLogger.Error("[Injector] Unable to get StringTables.");
                yield break;
            }

            // Change the Locale name from "English" to whatever. Spanish in this case.
            ChangeLocaleName("Español");

            string translationsDir = Path.Combine(MelonEnvironment.UserDataDirectory, "Translations");
            var tables = handle.Result;
            var index = 0;
            while (true)
            {
                try
                {
                    var table = tables[index];
                    var tableName = table.TableCollectionName;
                    var jsonPath = Path.Combine(translationsDir, $"{tableName}_es.json");

                    if (!File.Exists(jsonPath))
                    {
                        MelonLogger.Error($"[Injector] File {jsonPath} does not exist.");
                        index++;
                        continue;
                    }

                    string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                    var parsed = JSON.Parse(jsonContent);
                    if (parsed == null || !parsed.IsObject)
                    {
                        MelonLogger.Error($"[Injector] File {jsonPath} is corrupt or invalid.");
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

                    //MelonLogger.Msg($"[Injector] Injected {count} lines into {tableName}.");
                    index++;
                    continue;
                }
                catch 
                {
                    MelonLogger.Msg("[Injector] Injected all translations.");
                    break;
                }
            }
        }

        private static IEnumerator ChangeLocaleName(string LangName)
        {
            yield return LocalizationSettings.InitializationOperation;

            // Wait until we load the Main Menu
            while (SceneManager.GetActiveScene().name != "MainMenu")
                yield return new WaitForSeconds(0.5f);

            var dropdown = GameObject.FindObjectOfType<TMP_Dropdown>();
            if (dropdown == null)
            {
                MelonLogger.Warning("[Injector] Couldn't find any of the TMP_Dropdown instances.");
                yield break;
            }

            for (int i = 0; i < dropdown.options.Count; i++)
            {
                var option = dropdown.options[i];
                if (option.text == "English")
                {
                    option.text = LangName;
                    MelonLogger.Msg("[Injector] Changed locale name.");
                    dropdown.RefreshShownValue();
                    break;
                }
            }
        }

        private static IEnumerator SelectEnglishLocale()
        {
            yield return LocalizationSettings.InitializationOperation;

            var locales = LocalizationSettings.AvailableLocales.Locales;

            foreach (var loc in locales)
            {
                if (loc.Identifier.Code == "en")
                {
                    LocalizationSettings.SelectedLocale = loc;
                    MelonLogger.Msg("[Injector] Selected English locale.");
                    break;
                }
            }
        }

        private static IEnumerator LoadTranslations()
        {
            yield return LocalizationSettings.InitializationOperation;

            // Load every json file in UserData that has a translation.
            string translationsDir = Path.Combine(MelonEnvironment.UserDataDirectory, "Translations");
            string[] jsonFiles = Directory.GetFiles(translationsDir, "*_es.json");

            // We clear the dictionary just in case
            translatedTables.Clear();

            foreach (var file in jsonFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string tableName = fileName.Replace("_es", "");

                //MelonLogger.Msg($"Reading {tableName}.");

                string jsonText = File.ReadAllText(file, System.Text.Encoding.UTF8);

                if (string.IsNullOrWhiteSpace(jsonText))
                {
                    MelonLogger.Error($"{fileName} is empty or unreadable.");
                    continue;
                }

                try
                {
                    var json = JSON.Parse(jsonText);
                    if (json == null || !json.IsObject)
                    {
                        MelonLogger.Error($"{fileName} is corrupt or invalid.");
                        continue;
                    }

                    // This is in order to make the nested part of the translatedTables Dictionary
                    Dictionary<string, string> tableTranslations = new Dictionary<string, string>();

                    int entriesLoaded = 0;
                    // kvp = KeyValueProp
                    foreach (var kvp in json.AsObject)
                    {
                        string idStr = kvp.Key;
                        string value = kvp.Value;

                        if (!string.IsNullOrWhiteSpace(idStr) && !string.IsNullOrWhiteSpace(value))
                        {
                            tableTranslations[idStr] = value;
                            entriesLoaded++;
                        }
                    }

                    // Add the extracted dialogs to their correspondent table inside the Dictionary.
                    translatedTables[tableName] = tableTranslations;

                    //MelonLogger.Msg($"Loaded {tableName} with {entriesLoaded} entries.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error reading {fileName}: {ex.Message}");
                    continue;
                }
            }

            /*
            int totalEntries = 0;
            foreach (var entry in translatedTables)
                totalEntries += entry.Value.Count;
            MelonLogger.Msg($"Total translations loaded: {totalEntries}");
            */

            MelonLogger.Msg("Loaded translations.");

            yield break;
        }

        private IEnumerator AddCustomLocale()
        {
            yield return LocalizationSettings.InitializationOperation;

            var locales = LocalizationSettings.AvailableLocales.Locales;
            Locale spanishLocale = null;

            foreach (var loc in locales)
            {
                if (loc.Identifier.Code == "es")
                {
                    spanishLocale = loc;
                }
            }

            if (spanishLocale == null)
            {
                // Create a new Spanish locale
                spanishLocale = ScriptableObject.CreateInstance<Locale>();
                spanishLocale.name = "Español";
                spanishLocale.LocaleName = "Español";
                spanishLocale.Identifier = new LocaleIdentifier("es");
                locales.Add(spanishLocale);
                MelonLogger.Msg("[Locale] Spanish locale set.");
            }
            else
            {
                MelonLogger.Warning("[Locale] 'es' locale already in the list.");
            }

            if (spanishLocale != null)
            {
                // Set the locale to "es" automatically
                LocalizationSettings.SelectedLocale = spanishLocale;
                PlayerPrefs.SetString("UnityLocale", "es");
                PlayerPrefs.Save();

                MelonLogger.Msg("[Locale] Spanish selected as active locale.");
            }
        }

        // Toxic waste of a function
        private void ApplyAggressiveAhhPatches()
        {
            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("com.jair4x.translationhook");

            // List of types to search for methods to patch
            Type[] typesToPatch = new Type[]
            {
                typeof(LocalizedStringDatabase),
                typeof(LocalizedString),
                typeof(StringTable)
            };

            // List of method names that might be involved in localization
            string[] methodNamePatterns = new string[]
            {
                "GetLocalizedString",
                "GetString",
                "GetEntry",
                "GetText",
                "GetValue",
                "Localize"
            };

            int patchedCount = 0;

            foreach (var type in typesToPatch)
            {
                MelonLogger.Msg($"Searching for methods to patch in {type.Name}...");

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                foreach (var method in methods)
                {
                    // Skip methods that don't match our patterns
                    bool shouldPatch = false;
                    foreach (var pattern in methodNamePatterns)
                    {
                        if (method.Name.Contains(pattern))
                        {
                            shouldPatch = true;
                            break;
                        }
                    }

                    if (!shouldPatch)
                        continue;

                    // Skip methods that don't return string or AsyncOperationHandle<string>
                    if (method.ReturnType != typeof(string) &&
                        method.ReturnType != typeof(AsyncOperationHandle<string>) &&
                        !method.Name.Contains("GetLocalizedString"))
                        continue;

                    try
                    {
                        harmony.Patch(
                            method,
                            prefix: new HarmonyMethod(typeof(GeneralPatch), "Prefix")
                        );

                        patchedCount++;
                        MelonLogger.Msg($"Patched {type.Name}.{method.Name}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Failed to patch {type.Name}.{method.Name}: {ex.Message}");
                    }
                }
            }

            MelonLogger.Msg($"Successfully patched {patchedCount} methods");

            // Also patch any method that takes a TableEntryReference as a parameter
            int entryRefPatchCount = 0;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        // Skip system types and Unity types to avoid unnecessary patching
                        if (type.Namespace != null && (
                            type.Namespace.StartsWith("System") ||
                            type.Namespace.StartsWith("Unity") ||
                            type.Namespace.StartsWith("Il2Cpp")))
                            continue;

                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                        {
                            // Skip methods that don't return string
                            if (method.ReturnType != typeof(string) && method.ReturnType != typeof(AsyncOperationHandle<string>))
                                continue;

                            // Check if any parameter is TableEntryReference
                            bool hasTableEntryRef = false;
                            foreach (var param in method.GetParameters())
                            {
                                if (param.ParameterType == typeof(TableEntryReference))
                                {
                                    hasTableEntryRef = true;
                                    break;
                                }
                            }

                            if (!hasTableEntryRef)
                                continue;

                            try
                            {
                                harmony.Patch(
                                    method,
                                    prefix: new HarmonyMethod(typeof(GeneralPatch), "Prefix")
                                );

                                entryRefPatchCount++;
                                MelonLogger.Msg($"Patched {type.Name}.{method.Name} (has TableEntryReference)");
                            }
                            catch (Exception)
                            {
                                // Ignore errors because fuck it.
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignore errors for assembly scanning
                }
            }

            MelonLogger.Msg($"Additionally patched {entryRefPatchCount} methods with TableEntryReference parameters");
        }
    }

    // You DON'T WANT TO SEE THIS.
    class GeneralPatch
    {
        public static bool Prefix(object __instance, ref object __result, MethodBase __originalMethod, object[] __args)
        {
            try
            {
                // If we aren't in the spanish locale, move on.
                if (LocalizationSettings.SelectedLocale?.Identifier.Code != "es")
                    return true;

                Type returnType = null;
                if (__originalMethod is MethodInfo methodInfo)
                {
                    returnType = methodInfo.ReturnType;
                }

                string tableName = null;
                string keyId = null;

                // Try to find the tableName and keyID from a LocalizedString.
                foreach (var arg in __args)
                {
                    if (arg is LocalizedString locStr)
                    {
                        try
                        {
                            tableName = locStr.TableReference.TableCollectionName;
                            keyId = locStr.TableEntryReference.KeyId.ToString();
                        }
                        catch (Il2CppInterop.Runtime.ObjectCollectedException e)
                        {
                            MelonLogger.Warning($"[Hook] LocalizedString got invalidated by IL2CPP: {e.Message}");
                            return true;
                        }
                        break;
                    }
                }

                // Try to find the tableName and keyID from TableReferences
                if (tableName == null || keyId == null)
                {
                    TableReference tableRef = null;
                    TableEntryReference entryRef = null;

                    foreach (var arg in __args)
                    {
                        if (arg is TableReference tr) tableRef = tr;
                        if (arg is TableEntryReference ter) entryRef = ter;
                    }

                    if (tableRef != null)
                        tableName = tableRef.TableCollectionName;
                    if (entryRef != null)
                    {
                        try
                        {
                            keyId = entryRef?.m_KeyId.ToString();
                        }
                        catch (Il2CppInterop.Runtime.ObjectCollectedException e)
                        {
                            MelonLogger.Warning($"[Hook] EntryRef got invalidated by IL2CPP: {e.Message}");
                            return true;
                        }
                    }
                }

                // Show translation if we got them both
                if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(keyId))
                {
                    if (Core.translatedTables.TryGetValue(tableName, out var tableDict))
                    {
                        if (tableDict.TryGetValue(keyId, out var translation))
                        {
                            if (returnType == typeof(string))
                            {
                                __result = translation;
                                return false;
                            }
                            else if (returnType == typeof(AsyncOperationHandle<string>))
                            {
                                __result = Addressables.ResourceManager.CreateCompletedOperation(translation, null);
                                return false;
                            }
                        }
                    }
                }

                // Fallback: In case we didn't find anything at all.
                if (!string.IsNullOrEmpty(keyId))
                {
                    // kv = KeyValue
                    foreach (var kv in Core.translatedTables)
                    {
                        if (kv.Value.TryGetValue(keyId, out var fallbackTranslation))
                        {
                            if (returnType == typeof(string))
                            {
                                __result = fallbackTranslation;
                                return false;
                            }
                            else if (returnType == typeof(AsyncOperationHandle<string>))
                            {
                                __result = fallbackTranslation;
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in general patch: {ex.Message}");
            }

            // Let the original method run
            return true;
        }
    }
}