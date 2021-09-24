using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CHEF.Extensions;

namespace CHEF.Components.Watcher
{
    public static class CommonIssues
    {
        public static void CheckCommonLogError(string text, List<string> listOfSins, ref bool canBeSolvedWithHfpatch)
        {
            bool Contains(string testStr)
            {
                return text.Contains(testStr, StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(text)) return;

            if (text.Contains("GfxDevice: creating device client; threaded=1\nCrash!!!\nSymInit"))
            {
                listOfSins.Add("The game is crashing immediately after initializing graphics." +
                               "\n   A - If you are using any Citrix software, read this for a solution: <https://pastebin.com/KGYJKbrC>" +
                               "\n   B - Update your GPU drivers. You might need to use Display Driver Uninstaller to clean up old drivers.");
                return;
            }

            if (text.ContainsAll(new[]{"Koikatsu Party VR",
                "Failed to patch System.Void ChaControl::Initialize(System.Byte _sex, System.Boolean _hiPoly, UnityEngine.GameObject _objRoot, System.Int32 _id, System.Int32 _no, ChaFileControl _chaFile)"}, StringComparison.Ordinal))
            {
                listOfSins.Add("Looks like VR is crashing because you have the latest update from Steam. To fix this, either install HF Patch v3.9 or newer, or roll back to a previous update (check pinned messages for more information).");
            }

            if (Regex.IsMatch(text, @"Tried to load non-existing asset bundle: .*_Data/\.\./abdata/adv/.*unity3d"))
            {
                listOfSins.Add("Looks like some of your game files might be missing or corrupted. You might have to completely reinstall your game to fix this. Make sure to backup your UserData folder.");
            }

            if (!Contains("] BepInEx 5."))
            {
                if (Contains("Chainloader"))
                    listOfSins.Add("It looks like you have a very old version of BepInEx (older than v5.0). If you try to use any recent plugins your game will be likely crash or become unstable. Please update your game and your mods to the latest versions.");
                else
                    listOfSins.Add($"It looks like BepInEx is not starting. Because of this no mods or plugins will load." +
                                   $"\n   A - If you didn't install any mods yet, you will have to install BepInEx first." +
                                   $"\n   B - If you tried to update BepInEx, make sure that you've downloaded the x64 version of BepInEx." +
                                   $"\n   C - If the mods worked before but suddently stopped, try to restart your PC and run the game as administrator. If running as administrator helps then try to fix file permissions of your game folder. If you are using a third-party antivirus, try to exclude your Koikatsu game directory in the andtivirus settings and check if no files were quaranteened.");
                canBeSolvedWithHfpatch = true;
            }

            var pathMatch = Regex.Match(text, "Platform assembly: (.+) \\(this message is harmless\\)");
            string gamePath = string.Empty;
            if (pathMatch.Success)
            {
                gamePath = Path.GetDirectoryName(pathMatch.Groups[1].TrimmedValue()).Replace('/', '\\');
                if (gamePath.EndsWith("\\Managed"))
                    gamePath = Path.GetDirectoryName(Path.GetDirectoryName(gamePath));

                if (!gamePath.Contains(@"\steamapps\", StringComparison.OrdinalIgnoreCase))
                {
                    if (gamePath.Contains(@"\Program Files (x86)\", StringComparison.Ordinal))
                        listOfSins.Add("It looks like your game is installed to `Program Files (x86)`. This can cause serious issues with game data and prevent the game from working properly. Move the game folder out of Program Files. (for example `D:\\Games\\Koikatsu`).");
                    else if (gamePath.Contains(@"\Program Files\", StringComparison.Ordinal))
                        listOfSins.Add("It looks like your game is installed to `Program Files`. This can cause issues in some cases. It's recommended to move the game folder out of Program Files. (for example `D:\\Games\\Koikatsu`).");
                }

                if (gamePath.Length > 110)
                    listOfSins.Add("Your game directory path is too long. This can cause serious issues. Move the game folder closer to the root of your hard drive (for example `D:\\Games\\Koikatsu`).");

                if (gamePath.Any(c => c >= 128))
                    listOfSins.Add("Your game directory path contains non-ascii characters. This can cause serious issues. Move the game to a path that only uses English characters (for example `D:\\Games\\Koikatsu`).");

                if (gamePath.Any(char.IsWhiteSpace) && text.Contains("Loading [VideoExport 1.1.0]", StringComparison.Ordinal))
                    listOfSins.Add("Your game directory has spaces in its path. This can cause issues with the VideoExport plugin (failing to record). Move the game to a path with no spaces (for example `D:\\Games\\Koikatsu`) to avoid this issue.");

                if (gamePath.StartsWith("c:\\windows", StringComparison.OrdinalIgnoreCase) || gamePath.StartsWith("C:\\ProgramData", StringComparison.OrdinalIgnoreCase) ||
                   (gamePath.StartsWith("C:\\users", StringComparison.OrdinalIgnoreCase) && gamePath.Contains("AppData")))
                    listOfSins.Add($"Your game seems to be installed to a dangerous or error-provoking directory ({gamePath}). This can cause serious issues. Move the game to a simple path like `D:\\Games\\Koikatsu` to avoid issues.");
            }

            var bundleLoadErrors = Regex.Matches(text, @"\[Warning:XUnity\.ResourceRedirector\] Tried to load non-existing asset bundle: (.+)");
            if (bundleLoadErrors.Count > 0)
            {
                var filteredBundleErrors = bundleLoadErrors
                    .Select(x => x.Groups[1].TrimmedValue().Replace('/', '\\'))
                    .Attempt(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.Replace(gamePath, string.Empty, StringComparison.OrdinalIgnoreCase));
                listOfSins.Add($"It looks like some of your game files are missing or corrupted. You might have to reinstall the game. Here are (some of) the missing files: `{string.Join("`, `", filteredBundleErrors)}`");
            }

            if (text.Contains("FlashBangZ", StringComparison.OrdinalIgnoreCase))
                listOfSins.Add("It looks like you are using a FlashBangZ repack. It's strongly recommend that you remove it and download a fresh version of the game, or at least install the latest version of HF Patch. Until you get rid of this repack you will be very unlikely to receive any help here, and any cards you post might be removed on sight. His repacks have been caught to contain malware in the past, and they are known to be badly put together and have many issues. You can read more about it here <https://discordapp.com/channels/447114928785063977/447120583189331968/506923193454428182>.");

            if (Regex.Matches(text, "Multiple versions detected, only").Count > 30)
            {
                listOfSins.Add("Your `mods` folder looks to be very messy and there are many duplicate mods. This can cause issues, please consider cleaning it up.");
                canBeSolvedWithHfpatch = true;
            }

            var loadFails = Regex.Matches(text, @"Could not load \[(.*)\] because it has missing dependencies: (.+) ?\(?(.*)\)?");
            if (loadFails.Any())
            {
                var loadFailsStrings = loadFails
                    .Select(x =>
                    {
                        var versionStr = x.Groups[3].TrimmedValue();
                        return string.IsNullOrWhiteSpace(versionStr) ?
                            $"You need to install `{x.Groups[2].TrimmedValue()}` because it is needed by `{x.Groups[1].TrimmedValue()}`" :
                            $"You need to install/update `{x.Groups[2].TrimmedValue()}` to {versionStr} because it is needed by `{x.Groups[1].TrimmedValue()}`";
                    })
                    .Distinct()
                    .OrderBy(x => x);
                listOfSins.AddRange(loadFailsStrings);
            }

            var incompatibilities = Regex.Matches(text, @"Could not load \[(.*)\] because it is incompatible with: (.*)");
            if (incompatibilities.Any())
            {
                var filtered = incompatibilities.Select(x => new
                { conflictor = x.Groups[2].TrimmedValue(), victim = x.Groups[1].TrimmedValue() }).ToList();

                if (filtered.Any(x => x.victim.StartsWith("Text Dump")))
                    listOfSins.Add("You have the `Text Dump` plugin installed. It's meant to only be used by translators. If you don't need it you should remove it.");

                var loadFailsStrings = filtered
                    .Select(x => $"You need to remove `{x.conflictor}` because it is incompatible with `{x.victim}`")
                    .Distinct()
                    .OrderBy(x => x);
                listOfSins.AddRange(loadFailsStrings);
            }

            var plugsForWrongGames = Regex.Matches(text, @"] because of process filters \((.*)\)");
            var otherGameNames = plugsForWrongGames.Select(x => x.Groups[1].TrimmedValue())
                .Where(x => !_acceptableProcessNames.Any(x.Contains))
                .SelectMany(x => x.Split(',')).Select(y => y.Trim())
                .Distinct()
                .ToList();
            if (otherGameNames.Any())
            {
                listOfSins.Add($"It looks like you have plugins for a different game installed ({string.Join(", ", otherGameNames.OrderBy(x => x))}). This can cause plugins or even the game itself to crash or misbehave. Remove all plugins that are not meant to be used in Koikatsu and try again.");
                canBeSolvedWithHfpatch = true;
            }

            var wrongBepinVersions = Regex.Matches(text, @"Plugin \[(.*)\] targets a wrong version of BepInEx \((.*)\)");
            if (wrongBepinVersions.Any())
            {
                var minAcceptableVersion = new Version(5, 0, 0, 0);
                var wrongVersions = wrongBepinVersions
                    .Attempt(x => new { plug = x.Groups[1].TrimmedValue(), ver = new Version(x.Groups[2].TrimmedValue()) });

                foreach (var wrongVersion in wrongVersions)
                {
                    listOfSins.Add(wrongVersion.ver >= minAcceptableVersion
                        ? $"Plugin [{wrongVersion.plug}] requires a newer version of BepInEx ({wrongVersion.ver}). It might misbehave until you update BepInEx."
                        : $"Plugin [{wrongVersion.plug}] is for an old version of BepInEx ({wrongVersion.ver}). It will not work properly and is likely to break random things until it is removed. If you want to use it, place it directly inside your `BepInEx` folder and make sure you have the latest version of BepInEx.BepIn4Patcher.");
                }
            }

            if (Contains("MissingMethodException: Method not found: 'HarmonyLib.Harmony."))
                listOfSins.Add("It looks like your BepInEx might be outdated, and because of this some plugins are crashing. Please update BepInEx to the latest version and try again.");

            var typeloads = Regex.Matches(text, @"Exception: Could not load type '.+' from assembly '(.+), Version=");
            if (typeloads.Any())
            {
                var typeloadStrings = typeloads
                    .Select(x => x.Groups[1].TrimmedValue() + ".dll")
                    .Distinct();
                listOfSins.Add($"The following files failed to load: {string.Join(", ", typeloadStrings)}\nSome of these files are missing, corrupted or duplicated, which can cause other files to fail to load as well. This can cause plugins or even the game itself to crash or misbehave.");
                canBeSolvedWithHfpatch = true;
            }

            var dupedPlugins = Regex.Matches(text, @"Skipping because a newer version exists \[(.+)\]");
            if (dupedPlugins.Count > 0)
            {
                listOfSins.Add($"It looks like you have duplicated plugin dlls in your `BepInEx\\plugins` directory. " +
                               $"This might cause some issues, please consider removing the duplicates. " +
                               $"Affected plugins: `{string.Join("`, `", dupedPlugins.Select(x => x.Groups[1].TrimmedValue()).Distinct())}`");
            }

            var memAmount = Regex.Match(text, "Processor:.+RAM: (\\d+)MB");
            if (memAmount.Success)
            {
                var memCount = int.Parse(memAmount.Groups[1].TrimmedValue());
                if (memCount < 6000)
                {
                    listOfSins.Add($"You have only {Math.Round(memCount / 1024m)}GB of RAM. At least 6 GB of RAM is recommended. The game might randomly crash, hang or load very slowly. You can prevent these issues by closing other applications, reducing number of characters, turning off high-poly mode and lowering screenshot quality.");
                }
            }

            if (Contains("Sideloader Modpack - Exclusive HS2") || Contains("Sideloader Modpack - Exclusive AIS"))
            {
                listOfSins.Add("It looks like you have mods for AI-Shoujo and/or HoneySelect2 installed. This **will** cause issues. To fix this:\n   1 - Remove `mods\\Sideloader Modpack - Bleeding Edge`, `mods\\Sideloader Modpack - Exclusive AIS`, and `mods\\Sideloader Modpack - HS2`\n   2 - Update KKManager to v0.12.0 or newer (<https://github.com/IllusionMods/KKManager/releases/latest>)\n   3 - Start KKManager and look for updates (top right), then install all Sideloader Modpack updates.");
            }

            if (Contains("KoikPlugins.dll"))
            {
                listOfSins.Add("Looks like you are using kPlug. This plugin frequently breaks some of the mods often used by the rest of the community. Katarsys, the creator of kPlug, refuses to work with other modders to make kPlug and other mods compatible with each other. If you want to get help you will either need to uninstall kPlug and ask again, read the included kPlug readme, or ask Katarsys for help.");
            }

            if (Contains(@"BepInEx\Stiletto.dll"))
            {
                listOfSins.Add("Looks like you are using an old version of Stiletto for BepInEx 4. This will cause issues. Please update to the latest version of Stiletto. Make sure to remove the old version!");
            }

            if (Contains("MPCharCtrl+PoseInfo.UpdateInfo("))
            {
                listOfSins.Add("Looks like one of your poses in `UserData\\Studio\\pose` might be corrupted. The pose list might be broken. Try removing some of the recently added poses.");
            }

            if (Contains("D3D11: Failed to create RenderTexture"))
            {
                listOfSins.Add("You might be running out of VRAM / RAM, or your GPU or GPU drivers might be having issues.");
            }

            if (Contains("**** Crash! ****"))
            {
                listOfSins.Add("The game hard-crashed. This is usually caused by running out of RAM, outdated/bad drivers, other software, or corrupted game files (mods/plugins are usually NOT the cause). You can try the following:" +
                               "\n    A - Make sure that you have at least 4GB of free RAM (the more the better). Close all other applications to free up memory and avoid conflicts." +
                               "\n    B - If the game crashed in story mode, reduce the amount of students in school (slider at top right of the class roster) and/or turn off the High poly mode in plugin settings." +
                               "\n    C - If the game crashed when taking a screenshot (pressing the F9 / F11 key), reduce screenshot quality in plugin settings (make sure upsampling is at most at 2x)." +
                               "\n    D - Run Windows Update and update your GPU drivers. You might need to clean old drivers with DDU first." +
                               "\n    E - Scan your HDD for errors and check if it's not dying with a SMART tool. If the drive is fine, reinstall the game to a new directory, and only move over your old UserData folder.");
            }

            if (Contains("Mismatched serialization in the builtin class") || Regex.IsMatch(text, @"^The file 'archive:/CAB-[^']+' is corrupted! Remove it and launch unity again!"))
            {
                listOfSins.Add("It looks like some of the game files are corrupted." +
                               "\n    A - Reinstall the game to a new empty directory (move your old UserData folder over to keep all cards and progress)." +
                               "\n    B - If the game worked fine before and you didn't change anything, there's a good chance your hard drive is dying. Check if your hard drive is in good health, and scan it for errors (google is your friend)." +
                               "\n    C - If the game never worked correctly, make sure that the game and patch installation files are not corrupted. Redownload them if necessary or in doubt.");
            }
            
            if (Contains("FileNotFoundException: Could not load file or assembly 'UnityEngine.CoreModule"))
                listOfSins.Add("It looks like you have a KKS Plugin in your KK Plugin list. Please remove this plugin, known symptoms in an empty plugin menu in charastudio");
        }

        /// <summary>
        /// Check if the <paramref name="text"/> contains version numbers of mods<para/>
        /// Returns true if the text contains any outdated mods
        /// </summary>
        /// ///
        /// <param name="text">Text that may or may not contains mod version numbers</param>
        /// <param name="listOfSins"></param>
        public static void CheckModsVersion(string text, List<string> listOfSins)
        {
            bool Contains(string testStr)
            {
                return text.Contains(testStr, StringComparison.OrdinalIgnoreCase);
            }

            if (text != null && Contains("loading ["))
            {
                var outdatedMods = new HashSet<string>();

                const string regexFindVer = "Loading \\[(.*?) ([0-9].*?)]";
                var rx = new Regex(regexFindVer,
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var matches = rx.Matches(text);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 2)
                    {
                        var modName = match.Groups[1].ToString().Trim();
                        var verFromText = match.Groups[2].ToString().Replace(" ", "");
                        //Logger.Log("modName : " + modName);
                        //Logger.Log("verFromText : " + verFromText);
                        var latestVer = IsThisLatestModVersion(modName, new Version(verFromText));
                        if (latestVer != null)
                        {
                            outdatedMods.Add(modName); // $"{modName} (v{verFromText} instead of v{latestVer}"
                        }
                    }
                }

                if (outdatedMods.Count > 0)
                {
                    listOfSins.Add(outdatedMods.Count < 10
                        ? $"There are some old plugins, updating them might fix some issues. Plugins: {string.Join("; ", outdatedMods)}"
                        : "A lot of plugins are old, updating them might fix some issues. You can use HF Patch to update everything: <https://github.com/ManlyMarco/KK-HF_Patch/releases/latest>");
                }
            }
        }

        // Can use regex to extract these from log: .+Loading \[(.*?) ([0-9].*?)\]
        // then replace with: \{"$1" , new Version\("$2"\)\},
        private static readonly Dictionary<string, Version> _versionDict = new Dictionary<string, Version>
            {
{"Additional Skin Effects" , new Version("1.7.2")},
{"Animation Overdrive" , new Version("1.1")},
{"Anime Ass Assistant" , new Version("1.0.0")},
{"BepInEx.IPALoader" , new Version("1.2.1")},
{"Better Color Picker" , new Version("2.0.1")},
{"C# Script Loader" , new Version("1.2.1")},
{"Camera Target Fix" , new Version("13.2")},
{"Card Author Data" , new Version("1.12.2")},
{"Centered HScene Cursor" , new Version("1.12")},
{"Chara Overlays Based On Coordinate" , new Version("20.2.18.0")},
{"CharaStateX" , new Version("1.0.2.0")},
{"Character List Optimizations" , new Version("1.12")},
{"Character Maker Loaded Sound" , new Version("1.0")},
{"CharacterRandomizer" , new Version("1.0.0")},
{"Cheat Tools" , new Version("2.7")},
{"Clothes Overlay Mod" , new Version("5.1.2")},
{"Clothing State Menu" , new Version("3.0")},
{"Clothing Unlocker" , new Version("2.0.1")},
{"Colliders" , new Version("1.1")},
{"Color Adjuster" , new Version("0.6")},
{"Color Filter Remover" , new Version("14.1")},
{"Configuration Manager wrapper for Koikatsu" , new Version("14.1")},
{"Configuration Manager" , new Version("16.0")},
{"DefaultParamEditor" , new Version("1.1.0.136")},
{"Drag & Drop" , new Version("1.2")},
{"EnableResize" , new Version("1.4")},
{"Expand Male Maker" , new Version("1.0")},
{"Extended Save" , new Version("14.1")},
{"Eye Shaking" , new Version("1.0")},
{"FK and IK" , new Version("1.1")},
{"FPS Counter" , new Version("3.0")},
{"Fix Shader Dropdown Menu" , new Version("13.2")},
{"Floor Collider" , new Version("0.1")},
{"Force High Poly" , new Version("1.2.2")},
{"Free H Random" , new Version("1.1.1")},
{"Game and Studio Data Corruption Fixes" , new Version("13.2")},
{"GamepadSupport.GamepadController" , new Version("1.0.1")},
{"H Character Adjustment" , new Version("2.0")},
{"Hair Accessory Customizer" , new Version("1.1.5")},
{"Head Fix" , new Version("13.2")},
{"HeightBarX" , new Version("3.3")},
{"HideAllUI" , new Version("2.1.0")},
{"Image Series Recorder" , new Version("1.0")},
{"Input Hotkey Block" , new Version("1.2")},
{"Input Length Unlocker" , new Version("14.1")},
{"Invalid Scene Protection" , new Version("13.2")},
{"Invisible Body" , new Version("1.3.2")},
{"Janitor Replacer" , new Version("1.5")},
{"KKABMX (BonemodX)" , new Version("4.3")},
{"KKPE" , new Version("2.11.1")},
{"KKUS" , new Version("1.8.1")},
{"KKVMDPlayExtSavePlugin" , new Version("0.0.11")},
{"KK_AccStateSync" , new Version("1.0.0.0")},
{"KK_Ahegao" , new Version("1.8")},
{"KK_ClothesLoadOption" , new Version("0.2.1")},
{"KK_CrossEye" , new Version("1.6")},
{"KK_HCameraLight" , new Version("1.3")},
{"KK_Pregnancy" , new Version("1.2")},
{"KK_QuickAccessBox" , new Version("2.2")},
{"KK_QuickLoadOption" , new Version("1.0")},
{"Koikatsu Experience Logic" , new Version("1.0.1")},
{"Koikatsu: Become Trap" , new Version("2.0")},
{"Koikatu Gameplay Tweaks and Improvements" , new Version("1.4.2")},
{"List Override" , new Version("1.0")},
{"LockOnPlugin" , new Version("1.0.0")},
{"Maker/Studio Browser Folders" , new Version("2.0.1")},
{"MakerBridge" , new Version("1.0.1")},
{"Male Juice" , new Version("1.1")},
{"Manifest Corrector" , new Version("13.2")},
{"Material Editor" , new Version("2.0.6")},
{"Message Center" , new Version("1.1")},
{"Moan softly when I H you" , new Version("1.0")},
{"Mod Bone Implantor" , new Version("0.2.3")},
{"Modding API" , new Version("1.12.2")},
{"More Accessory Parents" , new Version("1.0")},
{"MoreAccessories" , new Version("1.0.9")},
{"NodesConstraints" , new Version("1.1.0")},
{"Null Checks" , new Version("13.2")},
{"Party Card Compatibility" , new Version("13.2")},
{"Personality Corrector" , new Version("1.12")},
{"Pose Folders" , new Version("1.0")},
{"Pose Load Fix" , new Version("13.2")},
{"Pose Quick Load" , new Version("1.0")},
{"Pushup" , new Version("1.1.3.1")},
{"Random Name Provider" , new Version("1.0.0.0")},
{"RealPov" , new Version("1.0.2.136")},
{"Reload Character List On Change" , new Version("1.5.1")},
{"Remove Cards To Recycle Bin" , new Version("1.1.1")},
{"Screenshot Manager" , new Version("14.1")},
{"Settings Fix" , new Version("13.2")},
{"Sideloader" , new Version("15.0")},
{"Skin Overlay Mod" , new Version("5.1.2")},
{"Slider Unlocker" , new Version("14.1")},
{"Stiletto" , new Version("1.3")},
{"Studio Auto Close Loading Scene Window" , new Version("19.11.8.0")},
{"Studio Chara Light Linked To Camera" , new Version("20.3.30.1")},
{"Studio Chara Only Load Body" , new Version("19.11.2.3")},
{"Studio Coordinate Load Option" , new Version("20.5.16.0")},
{"Studio Object Move Hotkeys" , new Version("1.0")},
{"Studio Optimizations" , new Version("13.2")},
{"Studio Reflect FK Fix" , new Version("19.11.1.0")},
{"Studio Save Workspace Order Fix" , new Version("20.5.15.0")},
{"Studio Scene Loaded Sound" , new Version("1.1")},
{"Studio Text Plugin" , new Version("20.4.27.0")},
{"Subtitles" , new Version("1.5.1")},
{"Text Resource Redirector" , new Version("1.2.3")},
{"Title shortcuts" , new Version("1.1.1")},
{"Uncensor Selector" , new Version("3.9")},
{"Unlimited Map Lights" , new Version("13.2")},
{"UnlockHPositions" , new Version("1.1.0")},
{"VideoExport" , new Version("1.1.0")},
{"XUnity Auto Translator" , new Version("4.11.2")},
{"XUnity Resource Redirector" , new Version("1.1.2")},
        };

        private static readonly string[] _acceptableProcessNames = { "Koikatu", "Koikatsu Party", "KoikatuVR", "Koikatsu Party VR", "CharaStudio" };

        /// <summary>
        /// Check for the <paramref name="modName"/> if <paramref name="otherVer"/> is the latest version.<para/>
        /// Returns the latest version as a string, null if <paramref name="otherVer"/> is the latest.
        /// </summary>
        /// <param name="modName">Name of the mod to check</param>
        /// <param name="otherVer">Text that should be equal to the mod version, outdated or not.</param>
        /// <returns></returns>
        private static Version IsThisLatestModVersion(string modName, Version otherVer)
        {
            if (_versionDict.TryGetValue(modName, out var ver) && ver > otherVer)
                return ver;

            return null;
        }
    }
}
