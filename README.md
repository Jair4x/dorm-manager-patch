Peeping Dorm Manager TL Patch
-----

### What's this?
A BepInEx (previously MelonLoader) mod made for the purpose of translating Peeping Dorm Manager, a unity IL2CPP game.
Documented, not only to preserve it, but to open this mod for anyone who wants to translate the game, to any language.
### What does it do?
1. Add a custom language to the options of the game (congigurable name and code in a .json file found in the "Config" file)
2. Load custom strings, made in a specific format found in [the tool](https://github.com/Jair4x/peeping-dorm-tool) I made for this mod, using English as a fallback language.
### How to install/use
Download the last release (or build it yourself) and put the DLL in the "BepInEx/Plugins" folder.

Then, for the translation, add a "Translation" (capitalization needed) folder on the same folder as the DLL.
Inside of said folder, put the `.json` files you created with the tool mentioned above.
### Configuration
When running the mod for the first time, a configuration file will be created in "BepInEx/Config" called `TranslationSettings.json`, inside, you'll see this:
1. "Language": This is the name of the custom language you'll see in the options in-game. Set it to whatever your language's name is. By default, it's "Español" (spanish).
2. "LanguageCode": The extension of the translation files. You can put whatever, but I recommend you just do it in [ISO 3166-1 Alpha-2](https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2) format. (i.e. the same format Discord uses for the flags)
3. "SelectedLanguage": This is the last language selected in the game, for persistence in language selection (AKA if you select the custom language, when you boot the mod, you should load the custom language). I don't recommend changing this unless you want the person to load the custom language directly when installing the mod. If you do want that, just change the value to the same as the "Language" field.
### Exporting the mod (and your translation)
Ensure the person has an **IL2CPP-Compatible BepInEx version**, like [6.0.0-pre.2](https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.2). And just give them the "Config" and "Plugins" folder with the mod and BepInEx + Mod config file.

"Why the BepInEx config file?" You might ask, well, we don't want the terminal open, so you should disable it.
```
[Logging.Console]

## Enables showing a console for log output.
# Setting type: Boolean
# Default value: true
Enabled = false
```
### Why the change from MelonLoader?
Exporting and installing the mod should be easy, MelonLoader is a pain to install a mod with, so I went with BepInEx.
### It doesn't work :(
Open an issue, I'll help you.
