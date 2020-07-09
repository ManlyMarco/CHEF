using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace CHEF.Components.Watcher
{
    public static class CommonIssues
    {
        public static void CheckCommonLogError(string text, List<string> listOfSins)
        {
            bool Contains(string testStr)
            {
                return text.Contains(testStr, StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(text)) return;

            if (!Contains("] BepInEx 5."))
            {
                if (Contains("Chainloader"))
                    listOfSins.Add("It looks like you have a very old version of BepInEx (older than v5.0). Please update your game and your mods (either manually or by installing the latest HF Patch).");
                else
                    listOfSins.Add("It looks like BepInEx is not starting. Try to restart your PC and run the game as administrator. If running as administrator helps then try to fix file permissions of your game folder (either manually or by running HF Patch).");
            }

            var pathMatch = Regex.Match(text, "Platform assembly: (.+) \\(this message is harmless\\)");
            if (pathMatch.Success)
            {
                var gamePath = Path.GetDirectoryName(pathMatch.Captures.Last().Value);
                if (gamePath.Length > 130)
                    listOfSins.Add("Your game directory path is too long. This can cause serious issues. Move the game folder closer to the root of your hard drive (for example `D:\\Games\\Koikatsu`).");

                if (gamePath.Any(c => c >= 128))
                    listOfSins.Add("Your game directory path contains non-ascii characters. This can cause serious issues. Move the game to a path that only uses English characters (for example `D:\\Games\\Koikatsu`).");

                if (gamePath.Contains("FlashBangZ", StringComparison.OrdinalIgnoreCase))
                    listOfSins.Add("It looks like you are using a FlashBangZ repack. It's strongly recommend that you remove it and download a fresh version of the game. His repacks have been caught to contain malware in the past, and they are known to be badly put together and have many issues. You can read more about it here <https://discordapp.com/channels/447114928785063977/447120583189331968/506923193454428182>.");
            }

            if (Regex.Matches(text, "Multiple versions detected, only").Count > 30)
                listOfSins.Add("Your `mods` folder looks to be very messy and there are many duplicate mods. This can cause issues, please consider cleaning it up.");

            var pluginSkippedCount = Regex.Matches(text, "Skipping because a newer version exists ").Count;
            if (pluginSkippedCount > 0)
                listOfSins.Add("It looks like you have duplicated plugin dlls in your `BepInEx\\plugins` directory. This might cause some issues, please consider removing the duplicates.");

            var memAmount = Regex.Match(text, "Processor:.+RAM: (\\d+)MB");
            if (memAmount.Success)
            {
                var memCount = int.Parse(memAmount.Groups[1].Value);
                if (memCount < 6000)
                {
                    listOfSins.Add($"You have only {Math.Round(memCount / 1024m)}MB of RAM. To work without issues at least 6 GB of RAM is recommended.");
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

            if (Contains("D3D11: Failed to create RenderTexture"))
            {
                listOfSins.Add("You might be running out of VRAM / RAM, or your GPU or GPU drivers might be having issues.");
            }
        }

        /// <summary>
        /// Check if the <paramref name="text"/> contains version numbers of mods<para/>
        /// Returns true if the text contains any outdated mods
        /// </summary>
        /// /// <param name="text">Text that may or may not contains mod version numbers</param>
        public static string CheckModsVersion(string text)
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
                        var modName = match.Groups[1].ToString();
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
                    return outdatedMods.Count < 15
                        ? $"These plugins are old and should be updated: {string.Join("; ", outdatedMods)}"
                        : "A lot of plugins are old and need to be updated. Either manually update everything or install latest HF Patch.";
                }
            }

            return null;
        }

        /// <summary>
        /// Check for the <paramref name="modName"/> if <paramref name="otherVer"/> is the latest version.<para/>
        /// Returns the latest version as a string, null if <paramref name="otherVer"/> is the latest.
        /// </summary>
        /// <param name="modName">Name of the mod to check</param>
        /// <param name="otherVer">Text that should be equal to the mod version, outdated or not.</param>
        /// <returns></returns>
        private static Version IsThisLatestModVersion(string modName, Version otherVer)
        {
            // Can use regex to extract these from log: .+Loading \[(.*?) ([0-9].*?)\]
            // then replace with: \{"$1" , new Version\("$2"\)\},
            var versionDict = new Dictionary<string, Version>
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
{"Chase Me" , new Version("0.9.0")},
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
{"Demosaic" , new Version("1.1")},
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
{"Graphics Settings" , new Version("1.1.0")},
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
{"KK Uniform Uniforms" , new Version("1.0.0.5")},
{"KKABMX (BonemodX)" , new Version("4.3")},
{"KKPE" , new Version("2.11.1")},
{"KKUS" , new Version("1.8.1")},
{"KKVMDPlayExtSavePlugin" , new Version("0.0.11")},
{"KK_AccStateSync" , new Version("1.0.0.0")},
{"KK_Ahegao" , new Version("1.8")},
{"KK_ClothesLoadOption" , new Version("0.2.1")},
{"KK_CrossEye" , new Version("1.6")},
{"KK_HCameraLight" , new Version("1.3")},
{"KK_OrthographicCamera" , new Version("1.1.1")},
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
{"Night Darkener" , new Version("1.1.1")},
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
{"Resource Unload Optimizations" , new Version("13.2")},
{"Runtime Unity Editor" , new Version("2.1")},
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
{"StudioSceneSettings" , new Version("1.1.1")},
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

            if (versionDict.TryGetValue(modName, out var ver) && ver > otherVer)
                return ver;

            return null;
        }
    }
}
