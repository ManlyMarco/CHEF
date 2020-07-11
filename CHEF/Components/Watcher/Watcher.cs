using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace CHEF.Components.Watcher
{
    public class Watcher : Component
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        private readonly AutoPastebin _autoPastebin;
        //private readonly ImageParser _imageParser;

        public Watcher(DiscordSocketClient client) : base(client)
        {
            _autoPastebin = new AutoPastebin();
            //_imageParser = new ImageParser();
        }

        public override async Task SetupAsync()
        {
            Client.MessageReceived += msg =>
            {
                try
                {
                    return MsgWatcherAsync(msg);
                }
                catch (Exception e)
                {
                    Logger.Log($"Failed to handle message {msg.GetJumpUrl()}\n{e}");
                    return Task.CompletedTask;
                }
            };

            await Task.CompletedTask;
        }

        private static readonly Dictionary<ulong, bool?> _userCanBeHelped = new Dictionary<ulong, bool?>();

        private static readonly string OutputLogHowToGet =
            "Please send your `output_log.txt` file, it will help us help you. This file should exist in your game's root directory (next to the Koikatsu exe). Simply drag this file over to discord to upload it. If the file is too large to upload then compress it and try again. If you can't see `output_log.txt` in your game directory then your game might be outdated. In that case either use the latest HF Patch to update, or look for the `output_log.txt` file inside of the `_Data` folder of application you had issues with (e.g. in `CharaStudio_Data` if you had issues with studio).";

        private int _maxLogFileSize = 9 * 1024 * 1024;
        internal static readonly string FaqsChannelId = "520061230279294976";
        internal static readonly string GuidesChannelId = "484091227659173898";
        internal static readonly string CardSharingChannelId = "447115249997578241";
        internal static readonly string SceneSharingChannelId = "447116302096662528";
        internal static readonly string ClassChatterChannelId = "447133555844448267";
        internal static readonly string CharaRequestsChannelId = "511308816886005764";
        internal static readonly string BotControlChannelId = "508984374600138763";

        public bool UserIsCounselor(SocketGuildUser user)
        {
            var role = (user as IGuildUser).Guild.Roles.FirstOrDefault(x => x.Name == "Counselor");
            return user.Roles.Contains(role);
        }

        private async Task MsgWatcherAsync(SocketMessage smsg)
        {
            if (smsg.Author.IsBot ||
                smsg.Author.IsWebhook ||
                !(smsg is SocketUserMessage msg) ||
                !(smsg.Channel is SocketTextChannel channel))
            {
                return;
            }

            bool ContainsAny(params string[] testStrings)
            {
                return testStrings.Any(testStr => msg.Content.Contains(testStr, StringComparison.InvariantCultureIgnoreCase));
            }

            bool ContainsAll(params string[] testStrings)
            {
                return testStrings.All(testStr => msg.Content.Contains(testStr, StringComparison.InvariantCultureIgnoreCase));
            }

            var isHelp = channel.Name == "help";
            var isOther = channel.Name == "mod-programming";

            bool? canBeHelped = null;

            if (isHelp)
            {
                if (msg.Content == "nohelp" || ContainsAny("i hate robots", "i hate bots", "i hate chika"))
                {
                    _userCanBeHelped[msg.Author.Id] = false;
                    await channel.SendMessageAsync($"{msg.Author.Mention} I will no longer try to help you");
                    return;
                }

                if (msg.Content == "yeshelp")
                {
                    _userCanBeHelped[msg.Author.Id] = true;
                    await channel.SendMessageAsync($"{msg.Author.Mention} I will try to help you again");
                    return;
                }

                if (msg.Content == "givelog")
                {
                    await channel.SendMessageAsync(OutputLogHowToGet);
                    return;
                }

                _userCanBeHelped.TryGetValue(msg.Author.Id, out canBeHelped);
                if (!canBeHelped.HasValue)
                {
                    canBeHelped = !UserIsCounselor(msg.Author as SocketGuildUser);
                    _userCanBeHelped[msg.Author.Id] = canBeHelped;
                }

                if (msg.Content == "chikahelp" || msg.MentionedUsers.Any(x => x.Id == Client.CurrentUser.Id))
                    await channel.SendMessageAsync(
                        $"{msg.Author.Mention} Hello!\nI **will{(canBeHelped != true ? " not" : "")}** automatically try to help when you post a question or send your output_log.txt.\nHere are my commands:\nnohelp - I will no longer try to help you (default for Counsellors)\nyeshelp - I will resume trying to help you");
            }

            if (isHelp || isOther)
            {
                // Vanity stuff
                if (msg.Content == "chika" || msg.Content == "bot" ||
                    ContainsAny("chikarin", "techinician chikarin", "help bot", "i hate robots"))
                {
                    if (!Emote.TryParse("<:peeeek:588304197175214092>", out var em) || channel.Guild.Emotes.All(x => x.Id != em.Id))
                        if (!Emote.TryParse("<:peeeek:730454944997441536>", out em) || channel.Guild.Emotes.All(x => x.Id != em.Id))
                            throw new InvalidDataException("No valid peeeek emoji found to react with");

                    await msg.AddReactionAsync(em);
                }

                // Get attachment contents + pastebin
                var textAttachments = msg.Attachments.Where(x =>
                {
                    var fileType = Path.GetExtension(x.Url).ToLowerInvariant();
                    return fileType == ".txt" || fileType == ".log" || fileType == ".cs" || fileType == ".md";
                }).ToList();

                var textsToProcess = new List<string>();
                var listOfPastebins = new List<string>();

                foreach (var textAttachment in textAttachments)
                {
                    var fileContent = await _httpClient.GetStringAsync(textAttachment.Url);

                    if (textAttachment.Size > _maxLogFileSize) continue;


                    if (FileIsValidLogFile(textAttachment))
                        textsToProcess.Add(fileContent);

                    try
                    {
                        var pasteBinUrl = await _autoPastebin.Try(fileContent);
                        if (!string.IsNullOrWhiteSpace(pasteBinUrl))
                            listOfPastebins.Add($"Pastebin link for {textAttachment}: {pasteBinUrl}");
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"Automatic pastebin for {msg.GetJumpUrl()} failed: {e}");
                    }
                }

                if (listOfPastebins.Any())
                {
                    await channel.SendMessageAsync(string.Join("\n", listOfPastebins));
                }

                if (canBeHelped != true)
                {
                    return;
                }

                // try to find if we can help
                if (isHelp)
                {
                    var listOfSins = new List<string>();

                    foreach (var textAttachment in textAttachments)
                    {
                        if (textAttachment.Size > _maxLogFileSize && FileIsValidLogFile(textAttachment))
                        {
                            listOfSins.Add($"Your log file {textAttachment.Filename} is extremely large and can't be parsed. Usually this means that something is very wrong with your game. Restart the game and reproduce your issue as quickly as possible to keep the log size small. You can also install latest HF Patch, it can fix most issues automatically.");
                        }
                        else
                        {
                            if (textAttachments.All(x =>
                                !string.Equals(x.Filename, "output_log.txt", StringComparison.OrdinalIgnoreCase)))
                            {
                                if (string.Equals(textAttachment.Filename, "LogOutput.log",
                                    StringComparison.OrdinalIgnoreCase))
                                {
                                    listOfSins.Add(
                                        "It looks like you uploaded the `BepInEx\\LogOutput.log` log file. Please send your `output_log.txt` file instead, it's much more useful for us. This file should exist in your game's root directory (next to the Koikatsu exe).");
                                }
                                else if (string.Equals(textAttachment.Filename, "error.log",
                                    StringComparison.OrdinalIgnoreCase))
                                {
                                    listOfSins.Add(
                                        "It looks like you uploaded the `error.log` log file. Please send your `output_log.txt` file instead, it's much more useful for us. This file should exist in your game's root directory (next to the Koikatsu exe).");
                                }
                            }
                        }
                    }

                    foreach (var logText in textsToProcess)
                    {
                        try
                        {
                            CommonIssues.CheckCommonLogError(logText, listOfSins);

                            var outdatedPlugsMsg = CommonIssues.CheckModsVersion(logText);
                            if (!string.IsNullOrEmpty(outdatedPlugsMsg))
                                listOfSins.Add(outdatedPlugsMsg);
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"Exception while checking for common errors in {msg.GetJumpUrl()}\n{e}");
                        }
                    }

                    // Check for exceptions
                    var allLogs = textsToProcess.Aggregate(string.Empty, (s1, s2) => s1 + s2);
                    var exceptions = Regex.Matches(allLogs, @"[^\]\n]*Exception.*(\r?\n.*)?(((\r?\n *at .*)+)|((\r?\n\w+ .*\))+))");
                    if (exceptions.Count > 0)
                    {
                        string CleanUpException(string exc)
                        {
                            exc = exc.Replace("\r\n", "\n").Trim();
                            exc = Regex.Replace(exc, @"\n(Stack trace:)?\n", "\n", RegexOptions.Multiline);
                            return exc;
                        }

                        var exceptionStrings = exceptions.Select(x => x.Value).Select(CleanUpException).GroupBy(s => s, StringComparer.Ordinal).ToList();

                        await channel.SendMessageAsync(
                            $"Found {exceptionStrings.Sum(gr => gr.Count())} instances of {exceptionStrings.Count} kinds of exceptions in attached log files. Showing the least common exceptions below.");

                        string PrettyUpException(string exc)
                        {
                            // Trim away useless  [0x00000] in <filename unknown>:0  at the end
                            exc = Regex.Replace(exc, " *\\[\\dx\\d+.+$", string.Empty, RegexOptions.Multiline);
                            return (exc.Length > 453 ? exc.Substring(0, 450) + "..." : exc);
                        }

                        var exceptionOutputStrings = exceptionStrings
                            .OrderBy(x => x.Count()) // Show single exceptions first
                            .Select(gr => $"Thrown {gr.Count()} times - {PrettyUpException(gr.Key)}").ToList();

                        var totalLength = 0;
                        await channel.SendMessageAsync(
                            $"```\n{string.Join("``````\n", exceptionOutputStrings.TakeWhile(s => (totalLength += s.Length + 13) < 2000))}```");
                    }

                    if (textAttachments.Count == 0)
                    {
                        var hasImages = msg.Attachments.Any(x => string.Equals(Path.GetExtension(x.Url), ".png",
                            StringComparison.OrdinalIgnoreCase));

                        if (ContainsAny("crash", "bug") && (ContainsAny("help") || hasImages))
                        {
                            await channel.SendMessageAsync($"{msg.Author.Mention}" + OutputLogHowToGet);
                        }
                        else if (ContainsAny("blue") && (ContainsAny("characters", "ghost", "persons", "people") || ContainsAll("all", "girls")))
                        {
                            listOfSins.Add(
                                "The blue characters around the school are just random mobs to make the school feel less empty, similar to Persona 5. You can turn them off in plugin settings (F1 > Plugin settings > Search for `mob`).");
                        }
                    }

                    if (msg.Attachments.Count == 0)
                    {
                        if (ContainsAny("where") && ContainsAny("placed", "to place", "located", "save", "userdata"))
                            listOfSins.Add("If you want to know where the game stores cards and such: Almost all data is saved in the UserData folder inside game directory. Check this link for explanations of what goes to which folder inside UserData <https://cdn.discordapp.com/attachments/562289883280965674/730892661296332942/What_are_these_folders.txt>");

                        if (ContainsAny("what is"))
                        {
                            if (ContainsAny(" kkp") || ContainsAll("koikat", "party"))
                            {
                                listOfSins.Add($"Koikatsu Party is the Steam version of Koikatu. It contains professional English and Chinese translations. Check <#{FaqsChannelId}> for more info.");
                            }
                        }

                        if (ContainsAll("overlay", "guide") && ContainsAll("any", "find"))
                            listOfSins.Add($"You can find guides for making overlays here <https://github.com/ManlyMarco/Illusion-Overlay-Mods/tree/master/Guide> and in the <#{GuidesChannelId}> channel.");

                        if (ContainsAll("crash") && ContainsAny("card sav", "saving card"))
                            listOfSins.Add("If your game crashes when saving cards in maker, try to lower the card image upsamplng ratio to 1 (Press F1 > Click on Plugin Settings > Search for `screenshot` to find the setting).");

                        if (ContainsAll("character", "missing") && ContainsAny("head", "clothes", "hair"))
                            listOfSins.Add("If your characters are missing their heads or parts of clothes or hair in story mode, try to turn on High Poly mode and turn off Async clothes loading (Press F1 > Click on Plugin Settings > Search for the setting names).");

                        if (ContainsAll("trap", "penis") && ContainsAny("not", "issue", "disappe"))
                            listOfSins.Add("If peni$ of your trap character doesn't show in story mode try to turn off the Clothing state persistence setting (Press F1 > Click on Plugin Settings > Search for `skin effects` to find the setting).");

                        if (ContainsAny("where", "how to", "howto", "how can", "how i can", "how do"))
                        {
                            //todo ThenContainsAny?
                            if (ContainsAny("buy", "download", " dl ", "get"))
                            {
                                if (ContainsAny("cards", "scenes"))
                                    listOfSins.Add($"You can download character cards and studio scenes in <#{CardSharingChannelId}> and <#{SceneSharingChannelId}> channels, or at <https://illusioncards.booru.org/>. If you are looking for specific cards, ask in the <#{ClassChatterChannelId}> channel. Cards and scenes are stored in special .png files with embedded game data - when downloading them make sure to get the real card file instead of a thumbnail. On Discord click on the card image and then on open original to get the real file as shown here: <https://cdn.discordapp.com/attachments/555528419442425883/713670102456991794/open_original_example.png>");
                                else if (ContainsAny("the game", "the dlc", "the expansion", "afterschool", "after school", "koikat", " kk fa"))
                                {
                                    listOfSins.Add($"Check the <#{FaqsChannelId}> channel for links to buy the game and expansions.");

                                    if (ContainsAny("for free", "pirate"))
                                        listOfSins.Add("We do not support piracy on the server, asking for and sharing links to pirate downloads of the game is against server rules and can net you a warning.");
                                }

                                if (ContainsAny("darkness"))
                                    listOfSins.Add("The Darkness expansion was a preorder exclusive and can no longer be legally acquired. It offers very little contents, so you aren't missing all that much if you don't have it.");

                                if (ContainsAny("hf patch", "hfpatch"))
                                {
                                    listOfSins.Add(
                                        $"You can download the latest version of HF Patch here: <https://github.com/ManlyMarco/KK-HF_Patch/releases/latest>. If you have trouble with downloading the torrent file then try using qBittorrent. Check the <#{FaqsChannelId}> channel for more info.");
                                }
                            }
                            else
                            {
                                if (ContainsAny("darkness"))
                                    listOfSins.Add("If you want to trigger the Darkness scene in story mode: Take a non-virgin girl with the correct personality to the third floor door while there's no teacher nearby and interact with the icon next to one of the doors.");
                            }

                            if (ContainsAny("plugin hotkeys"))
                                listOfSins.Add("You can check and modify hotkeys of many plugins from the plugin settings screen. You can open plugin settings in main game by entering game settings (usually by pressing F1) and clicking the Plugin Settings button in top right corner. You can open plugin settings in studio by pressing F1.");
                            else if (ContainsAny("open plugin settings"))
                                listOfSins.Add("You can open plugin settings in main game by entering game settings (usually by pressing F1) and clicking the Plugin Settings button in top right corner. You can open plugin settings in studio by pressing F1.");

                            if (ContainsAny("update bepinex"))
                                listOfSins.Add("It's recommended that you don't update BepInEx manually. Some plugins might need to be updated or removed, and some configuration might need to be changed when updating it. Doing this improperly can seriously break things. Instead update by grabbing the latest version of a mod pack like for example the HF Patch.");

                            if (ContainsAny("update kkmanager"))
                                listOfSins.Add("You can get the latest version of KKManager here: <https://github.com/IllusionMods/KKManager/releases/latest>. Follow the instructions in the release post to update your existing KKManager installation.");

                            if (ContainsAny("character") && ContainsAny("request"))
                                listOfSins.Add($"If you want to request a character to be made, you can ask in the <#{CharaRequestsChannelId}> channel. Note that you need to be at least level 10 on the server to post there. You can earn levels by talking on the server and posting your creations. To check your current level go to <#{BotControlChannelId}> and use the `.xp` command.");

                            if (ContainsAny("uncensor"))
                                listOfSins.Add($"If you want to uncensor the game, or have issues with uncensors (e.g. scrunching peni$) check the <#{FaqsChannelId}> channel for more info. The easiest way to uncensor the game is to install the latest version of HF Patch.");

                            if (textAttachments.Count == 0
                                && (ContainsAny("output_log", "output log", "log file")))
                            {
                                listOfSins.Add(OutputLogHowToGet);
                            }

                            if ((ContainsAny("hf patch", "hfpatch")) && ContainsAny("password"))
                            {
                                listOfSins.Add(
                                    "You can find the HF Patch password right next to the download links in the patreon post you downloaded it from.");
                            }
                        }

                        if (ContainsAny("custom intro", "meme intro", "stupid intro") && ContainsAny("disable", "remove", "uninstall"))
                        {
                            listOfSins.Add("If you want to remove the custom intro sounds (meme sounds on game start) please delete the `BepInEx\\Plugins\\IntroClips` folder.");
                        }
                    }

                    //var yandexRes = await _imageParser.Try(msg);

                    //if (yandexRes.Length > 1)
                    //    await channel.SendMessageAsync(yandexRes);

                    if (listOfSins.Count > 0)
                    {
                        //if (listOfSins.Count == 1)
                        //{
                        //    var m = await channel.SendMessageAsync($"{msg.Author.Mention} {listOfSins.First()}");
                        //    Logger.Log($"Tried to help with {listOfSins.Count} hits - " + m.GetJumpUrl());
                        //}
                        //else
                        {
                            var m = await channel.SendMessageAsync(
                                $"{msg.Author.Mention} I found answers to some common issues that might be helpful to you:\n• {string.Join("\n• ", listOfSins)}");
                            Logger.Log($"Tried to help with {listOfSins.Count} hits - " + m.GetJumpUrl());
                        }

                        return;
                    }
                }

                // Only be a smartass if nothing else triggered
                if (ContainsAny("can i ask something?", "can someone help me?", "can anyone help me?"))
                {
                    await channel.SendMessageAsync($"{msg.Author.Mention} https://dontasktoask.com/");
                }

                if (ContainsAny("flashbangz"))
                {
                    await channel.SendMessageAsync($"{msg.Author.Mention} If you are using a FlashBangZ repack, it's strongly recommend that you remove it and download a fresh version of the game. Until you get rid of this repack you will be very unlikely to receive any help here, and any cards you post might be removed on sight. His repacks have been caught to contain malware in the past, and they are infamous for being riddled with issues. You can read more about it here <https://discordapp.com/channels/447114928785063977/447120583189331968/506923193454428182>.");
                }

                if (ContainsAny("what is the meaning of life"))
                {
                    await channel.SendMessageAsync($"{msg.Author.Mention} The meaning of life is to play Koikatsu and worship Chikarin");
                }
            }
        }

        private static bool FileIsValidLogFile(Attachment textAttachment)
        {
            return textAttachment.Filename.StartsWith("output_log", StringComparison.OrdinalIgnoreCase) ||
                   textAttachment.Filename.StartsWith("LogOutput", StringComparison.OrdinalIgnoreCase);
        }
    }
}
