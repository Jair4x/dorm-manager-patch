using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Injection;
using SimpleJSON;
using System.Collections;
using System.IO;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace TranslationHook
{
    [BepInPlugin("com.jair4x.translationhook", "TranslationHook", "2.0.0")]
    [BepInProcess("Peeping Dorm Manager.exe")]
    public class Core : BasePlugin
    {
        private new ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("TranslationHook");
        public static CoroutineHost Host;

        public override void Load()
        {
            Log.LogInfo("Hook initialized.");

            ClassInjector.RegisterTypeInIl2Cpp<CoroutineHost>();

            CreateHostAndStartChecker();
        }

        private void CreateHostAndStartChecker()
        {
            if (Host == null)
            {
                var go = new GameObject("TranslationHookCoroutineHost");
                UnityEngine.Object.DontDestroyOnLoad(go);
                Host = go.AddComponent<CoroutineHost>();

                Log.LogInfo("Starting readiness checker...");
                Host.StartCoroutine(CheckGameReadiness());
            }
        }

        private IEnumerator CheckGameReadiness()
        {
            int attempts = 0;
            while (attempts < 60) // Try for 60 seconds max
            {
                attempts++;
                Log.LogInfo($"Checking game readiness... attempt {attempts}");

                // Check if LocalizationSettings is actually available
                try
                {
                    if (LocalizationSettings.StringDatabase != null)
                    {
                        Log.LogInfo("Game systems ready! Starting main coroutines...");
                        Host.StartCoroutine(SelectEnglishLocale());
                        Host.StartCoroutine(InjectorCoroutine());
                        Host.StartCoroutine(MonitorLocaleChanges());
                        yield break;
                    }
                }
                catch (System.Exception ex)
                {
                    Log.LogInfo($"Game not ready yet: {ex.Message}");
                }

                yield return new WaitForSeconds(1f);
            }

            Log.LogWarning("Game readiness timeout - starting anyway...");
            Host.StartCoroutine(SelectEnglishLocale());
            Host.StartCoroutine(InjectorCoroutine());
            Host.StartCoroutine(MonitorLocaleChanges());
        }

        private IEnumerator MonitorLocaleChanges()
        {
            Log.LogInfo("[MonitorLocaleChanges] Starting...");

            Log.LogInfo("[MonitorLocaleChanges] Starting monitoring loop...");

            UnityEngine.Localization.Locale locale = LocalizationSettings.SelectedLocale;

            while (true)
            {
                yield return new WaitForSeconds(0.5f);

                UnityEngine.Localization.Locale currentLocale = LocalizationSettings.SelectedLocale;

                if (currentLocale != locale)
                {
                    Log.LogInfo($"[MonitorLocaleChanges] Changed to {currentLocale?.Identifier.Code ?? "null"}");

                    if (currentLocale != null && currentLocale.Identifier.Code == "en")
                    {
                        Log.LogInfo($"[MonitorLocaleChanges] Injecting...");
                        yield return Host.StartCoroutine(InjectorCoroutine());

                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

                        yield return new WaitForSeconds(0.5f);
                        Host.StartCoroutine(ChangeLocaleName("Español"));
                    }

                    locale = currentLocale;
                }
            }
        }

        private IEnumerator InjectorCoroutine()
        {
            Log.LogInfo("[InjectorCoroutine] Starting...");

            Log.LogInfo("[InjectorCoroutine] Getting String Tables...");

            var stringDatabase = LocalizationSettings.StringDatabase;
            var handle = stringDatabase.GetAllTables();

            if (!handle.IsDone || handle.Result == null)
            {
                Log.LogError("[InjectorCoroutine] Unable to get StringTables.");
                yield break;
            }

            Log.LogInfo("[InjectorCoroutine] String Tables found, injecting language...");
            Host.StartCoroutine(ChangeLocaleName("Español"));

            string translationsDir = Path.Combine(Paths.ConfigPath, "Translations");
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
                        Log.LogWarning($"[InjectorCoroutine] File {jsonPath} does not exist.");
                        index++;
                        continue;
                    }

                    string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                    var parsed = JSON.Parse(jsonContent);
                    if (parsed == null || !parsed.IsObject)
                    {
                        Log.LogError($"[InjectorCoroutine] File {jsonPath} is corrupt or invalid.");
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
                    Log.LogInfo("[Injector] Injected all translations.");
                    break;
                }
            }
        }

        private IEnumerator ChangeLocaleName(string LangName)
        {
            Log.LogInfo("[ChangeLocaleName] Starting...");

            Log.LogInfo("[ChangeLocaleName] Waiting for main menu...");

            while (SceneManager.GetActiveScene().name != "MainMenu")
                yield return new WaitForSeconds(0.5f);

            Log.LogInfo("[ChangeLocaleName] Main menu found, trying to replace locale name...");

            var dropdown = GameObject.FindObjectOfType<TMP_Dropdown>();
            if (dropdown == null)
            {
                Log.LogWarning("[ChangeLocaleName] Couldn't find TMP_Dropdown.");
                yield break;
            }

            for (int i = 0; i < dropdown.options.Count; i++)
            {
                var option = dropdown.options[i];
                if (option.text == "English")
                {
                    option.text = LangName;
                    Log.LogInfo("[ChangeLocaleName] Changed locale name.");
                    dropdown.RefreshShownValue();
                    break;
                }
            }
        }

        private IEnumerator SelectEnglishLocale()
        {
            Log.LogInfo("[AutoLocale] Starting...");

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

    public class CoroutineHost : UnityEngine.MonoBehaviour
    {
        // This shi don't need anything
    }
}
