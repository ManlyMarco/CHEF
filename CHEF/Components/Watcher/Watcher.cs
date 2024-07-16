using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CHEF.Extensions;
using Discord;
using Discord.WebSocket;

namespace CHEF.Components.Watcher
{
    public class Watcher : Component
    {
        private const string _canBeHelpedListFilename = "canbehelpedlist.txt";
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
            ReadCanHelpList();

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

        private static void ReadCanHelpList()
        {
            try
            {
                var fn = Path.GetFullPath(_canBeHelpedListFilename);
                if (File.Exists(fn))
                {
                    Console.WriteLine($"Reading {fn}");
                    _userCanBeHelped = File.ReadAllLines(fn).Select(x => x.Split(',', 2, StringSplitOptions.None)).Where(x => x.Length == 2 && !string.IsNullOrWhiteSpace(x[1])).ToDictionary(x => ulong.Parse(x[0]), x => bool.Parse(x[1]));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to read {_canBeHelpedListFilename} - {e}");
            }
        }

        private static void WriteCanHelpList()
        {
            try
            {
                var fn = Path.GetFullPath(_canBeHelpedListFilename);
                Console.WriteLine($"Writing {fn}");
                File.WriteAllLines(fn, _userCanBeHelped.Select(x => x.Key + "," + x.Value));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to write {_canBeHelpedListFilename} - {e}");
            }
        }

        private static void SetCanBeHelped(ulong userId, bool value)
        {
            _userCanBeHelped[userId] = value;
            WriteCanHelpList();
        }

        private static Dictionary<ulong, bool> _userCanBeHelped = new Dictionary<ulong, bool>();

        private static readonly string OutputLogPleaseGive =
            "If you are getting crashes or other errors in the game, please send the `output_log.txt` file from the game directory, it will help us help you. Type `how to send output_log` if you need more info.";

        private static readonly string OutputLogHowToSend =
            "output_log.txt should exist in your game's root directory (next to the Koikatsu exe). Simply drag this file over to discord to upload it. If the file is too large to upload then you can compress it, or restart your game and try to reproduce the issue as quickly as possible (this should produce a much smaller log file).\nIf you can't see `output_log.txt` in your game directory then your game might be outdated. In that case either use the latest HF Patch to update, or look for the `output_log.txt` file inside of the `_Data` folder of application you had issues with (e.g. in `CharaStudio_Data` if you had issues with studio). You can also click the 'Log' button in the plugin settings window.\nIf you can't find the game's root directory type `where is the game installed?` and I will help you.";

        private int _maxLogFileSize = 9 * 1024 * 1024;
        internal static readonly string FaqsChannelId = "520061230279294976";
        internal static readonly string GuidesChannelId = "484091227659173898";
        internal static readonly string CardSharingChannelId = "447115249997578241";
        internal static readonly string SceneSharingChannelId = "447116302096662528";
        internal static readonly string ClassChatterChannelId = "447133555844448267";
        internal static readonly string CharaRequestsChannelId = "511308816886005764";
        internal static readonly string BotControlChannelId = "508984374600138763";
        internal static readonly string HelpChannelId = "555528419442425883";
        private static readonly string[] _emotePeek = { "<:peeeek:588304197175214092>", "<:peeeek:730454944997441536>" };
        private static readonly string[] _emoteYay = { "<:nepyay:585938344136015884>", "<:nepyay:734048849618010164>" };
        private static readonly string[] _emoteCry = { "<:aquacri:447131902839619604>", "<:aquacri:734049329685200908>" };

        public bool UserIsCounselor(SocketGuildUser user)
        {
            var role = (user as IGuildUser).Guild.Roles.FirstOrDefault(x => x.Name == "Counselor");
            return user.Roles.Contains(role);
        }

        private async Task MsgWatcherAsync(SocketMessage smsg)
        {
            if (smsg.Author.IsBot ||
                smsg.Author.IsWebhook ||
                !(smsg is SocketUserMessage msg))
            {
                return;
            }

            var channel = smsg.Channel;
            var isDM = channel is SocketDMChannel;
            if (channel is not SocketTextChannel && !isDM)
            {
                return;
            }

            var isTestChannel = channel.Name == "help-test";
            var isHelp = isTestChannel || channel.Name == "help";
            var isOther = channel.Name == "mod-programming" || isDM;

            if (!isHelp && !isOther) return;

            var content = msg.Content?.SafeNormalize().Replace("\r\n", "\n") ?? string.Empty;

            bool ContainsAny(params string[] testStrings)
            {
                return testStrings.Any(testStr => content.Contains(testStr, StringComparison.InvariantCultureIgnoreCase));
            }

            bool ContainsAll(params string[] testStrings)
            {
                return testStrings.All(testStr => content.Contains(testStr, StringComparison.InvariantCultureIgnoreCase));
            }

            bool canBeHelped = false;

            // Process commands
            if (isHelp)
            {
                if (content == "nohelp" || ContainsAny("i hate robots", "i hate bots", "i hate chika"))
                {
                    SetCanBeHelped(msg.Author.Id, false);
                    await msg.ReplyAsync($"I will no longer try to help you");
                    return;
                }

                if (content == "yeshelp")
                {
                    SetCanBeHelped(msg.Author.Id, true);
                    await msg.ReplyAsync($"I will try to help you again");
                    return;
                }

                if (content == "givelog")
                {
                    await channel.SendMessageAsync(OutputLogPleaseGive);
                    return;
                }

                if (!_userCanBeHelped.TryGetValue(msg.Author.Id, out canBeHelped))
                {
                    canBeHelped = !UserIsCounselor(msg.Author as SocketGuildUser);
                    SetCanBeHelped(msg.Author.Id, canBeHelped);
                }

                if (content == "chikahelp" || msg.MentionedUsers.Any(x => x.Id == Client.CurrentUser.Id))
                    await msg.ReplyAsync($"Hello!\nI **will{(canBeHelped != true ? " not" : "")}** automatically try to help when you post a question or send your output_log.txt.\nHere are my commands:\nnohelp - I will no longer try to help you (default for Counsellors)\nyeshelp - I will resume trying to help you\ngivelog - Show instructions on how to get the output_log.txt file\ngood bot - Mark my last reply as useful\nbad bot - Mark my last reply as needing improvement");
            }

            if (isTestChannel || isDM) canBeHelped = true;

            // Vanity stuff
            if (content.Equals("chika", StringComparison.OrdinalIgnoreCase) ||
                content.Equals("bot", StringComparison.OrdinalIgnoreCase) ||
                ContainsAny("chikarin", "techinician chikarin", "help bot", "i hate robots"))
            {
                await AddReaction(msg, _emotePeek);
            }
            else if (content.Equals("good bot", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Log("I was called a **good bot** - " + msg.GetJumpUrl());
                await AddReaction(msg, _emoteYay);
            }
            else if (content.Equals("bad bot", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Log("I was called a **bad bot** - " + msg.GetJumpUrl());
                await AddReaction(msg, _emoteCry);
            }

            // Get attachment contents + pastebin
            var textAttachments = msg.Attachments.Where(x =>
            {
                var fileType = Path.GetExtension(x.Url.Split('?')[0]).ToLowerInvariant();
                return fileType == ".txt" || fileType == ".log" || fileType == ".cs" || fileType == ".md";
            }).ToList();

            var textsToProcess = new List<string>();
            var kkmanLog = string.Empty;
            var listOfPastebins = new List<string>();

            foreach (var textAttachment in textAttachments)
            {
                var fileContent = await _httpClient.GetStringAsync(textAttachment.Url);

                if (textAttachment.Size > _maxLogFileSize) continue;

                fileContent = fileContent.SafeNormalize().Replace("\r\n", "\n");

                if (FileIsValidLogFile(textAttachment))
                {
                    fileContent = CleanUpLogDuplicates(fileContent);
                    textsToProcess.Add(fileContent);
                }
                else if (textAttachment.Filename.StartsWith("kkmanager", StringComparison.OrdinalIgnoreCase))
                    kkmanLog = fileContent;

                // todo reenable?
                //try
                //{
                //    var cleanFileContent = CleanUpLogForPastebin(fileContent);
                //    var pasteBinUrl = await _autoPastebin.Try(cleanFileContent).WithTimeout(TimeSpan.FromSeconds(3), CancellationToken.None);
                //    if (!string.IsNullOrWhiteSpace(pasteBinUrl))
                //        listOfPastebins.Add($"Pastebin link for {(fileContent.Length > cleanFileContent.Length ? "cleaned " : "")}{textAttachment}: {pasteBinUrl}");
                //}
                //catch (Exception e)
                //{
                //    Logger.Log($"Automatic pastebin for {msg.GetJumpUrl()} failed: {e}");
                //}
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
            if (isHelp || isDM)
            {
                var listOfSins = new List<string>();
                var canBeFixedWithHfpatch = false;

                if (!string.IsNullOrWhiteSpace(kkmanLog))
                {
                    if (kkmanLog.Contains("Failed to parse update manifest file") &&
                        kkmanLog.Contains("at Updates KKManager.Updater.Data.UpdateInfo.Deserialize"))
                        listOfSins.Add("Your KKManager might be outdated, which causes updates to fail. Get [the latest version of KKManager](<https://github.com/IllusionMods/KKManager/releases/latest>) and update your install.");
                }

                foreach (var textAttachment in textAttachments)
                {
                    if (textAttachment.Size > _maxLogFileSize && FileIsValidLogFile(textAttachment))
                    {
                        listOfSins.Add(
                            $"Your log file {textAttachment.Filename} is extremely large and can't be parsed. Usually this means that something is very wrong with your game. Restart the game and reproduce your issue as quickly as possible to keep the log size small.");
                        canBeFixedWithHfpatch = true;
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
                        CommonIssues.CheckCommonLogError(logText, listOfSins, ref canBeFixedWithHfpatch);

                        CommonIssues.CheckModsVersion(logText, listOfSins);
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"Exception while checking for common errors in {msg.GetJumpUrl()}\n{e}");
                    }
                }

                static string CleanUpException(string exc)
                {
                    exc = exc.Trim();
                    exc = Regex.Replace(exc, @"\n(Stack trace:)?\n", "\n", RegexOptions.Multiline);
                    // Trim away useless [0x00000] in <filename unknown>:0 and <0x0001c> at the end
                    exc = Regex.Replace(exc, @" *[\[<]\dx\d+.+$", string.Empty, RegexOptions.Multiline);
                    return exc;
                }

                // Check for exceptions
                var allLogs = textsToProcess.Aggregate(string.Empty, (s1, s2) => s1 + s2);
                var exceptions = Regex.Matches(allLogs, @"[^\]\n]*Exception.*(\n.*)?(((\n *at .*)+)|((\n\w+.*\))+))");
                var prettyExceptions = exceptions.Select(x => x.Value).Where(x =>
                    !x.Contains("System.Exception and name PrepForRemoting", StringComparison.Ordinal) && !x.Contains("AccessTools.Method:")).Select(CleanUpException).ToList();
                if (prettyExceptions.Count > 0)
                {
                    var uniqueExceptions = prettyExceptions
                        .Distinct(StringComparer.Ordinal)
                        .ToList();

                    static string ClampException(string exc)
                    {
                        return (exc.Length > 453 ? exc.Substring(0, 450) + "..." : exc);
                    }

                    var totalLength = 0;
                    await channel.SendMessageAsync($"Found {prettyExceptions.Count} exceptions in attached log files ({uniqueExceptions.Count} are unique). Here are the earliest exceptions:\n```\n{string.Join("``````\n", uniqueExceptions.Select(ClampException).TakeWhile(s => (totalLength += s.Length + 13) < 1500))}```");

                    if (exceptions.Count > 400)
                    {
                        listOfSins.Add("It looks like something in your game is crashing on every frame. This can drastically reduce FPS and/or introduce stuttering/lag. This is usually caused by outdated or incompatible plugins/mods, and in rare cases by corrupted game files.");
                        canBeFixedWithHfpatch = true;
                    }
                }

                if (textAttachments.Count == 0)
                {
                    if (ContainsAll("blue tongue"))
                    {
                        listOfSins.Add("Blue tongues on characters are caused by crashes during the load process. " + OutputLogPleaseGive);
                    }
                    else if (ContainsAny("blue") && (ContainsAny("characters", "ghost", "persons", "people") ||
                                                     ContainsAll("all", "girls")))
                    {
                        listOfSins.Add("The blue characters around the school are just random mobs to make the school feel less empty, similar to Persona 5. You can turn them off in plugin settings (F1 > Plugin settings > Search for `mob`).");
                    }
                    else if (ContainsAll("how to send output_log"))
                    {
                        await msg.ReplyAsync($"{OutputLogHowToSend}");
                        return;
                    }
                    else if (ContainsAll("slow") && ContainsAny("download", "update"))
                    {
                        listOfSins.Add("If you are experiencing slow downloads when updating modpacks through KKManager, it's most likely because a lot of people are trying to update at the same time. Try updating later. If only the last download is slow, it means one of the servers is overloaded, in that case you can try to restart the update so it picks a different server.");
                    }

                    if (listOfSins.Count == 0)
                    {
                        var hasImages = msg.Attachments.Any(x => string.Equals(Path.GetExtension(x.Url), ".png",
                            StringComparison.OrdinalIgnoreCase));

                        if (!ContainsAll("missing", "mod") &&
                            ContainsAny("crash", "bug", "error", "issue") &&
                            (ContainsAny("help", "won't start", "game", "studio") || hasImages))
                        {
                            await msg.ReplyAsync($"{OutputLogPleaseGive}");
                            return;
                        }
                    }
                }

                if (msg.Attachments.Count == 0)
                {
                    var sentences = content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var sentence in sentences)
                    {
                        SearchForCommonQuestions(sentence, listOfSins);
                    }
                }

                //var yandexRes = await _imageParser.Try(msg);

                //if (yandexRes.Length > 1)
                //    await channel.SendMessageAsync(yandexRes);

                if (listOfSins.Count > 0)
                {
                    var m = await msg.ReplyAsync($"I found answers to some common issues that might be helpful to you:\n• {string.Join("\n• ", listOfSins)}");

                    var guildName = channel is SocketTextChannel stc ? stc.Guild.Name : ((SocketDMChannel)channel).Recipient.Username;
                    Logger.Log($"Tried to help [{msg.Author.Username}] in [{guildName}\\{channel.Name}] with {listOfSins.Count} hits - " + m.GetJumpUrl());

                    if (canBeFixedWithHfpatch)
                        await channel.SendMessageAsync(
                            $"It looks like some or all of your issues can be automatically fixed by installing HF Patch. Check pinned messages in this channel or the <#{FaqsChannelId}> channel for download links and more info.");

                    return;
                }
                else if (textsToProcess.Count > 0)
                {
                    await channel.SendMessageAsync(
                        $"I couldn't find anything wrong in the attached log file(s). Say `bad bot` if I missed something important.");
                    Logger.Log($"Found nothing in log file(s) - " + msg.GetJumpUrl());
                    return;
                }
                else if (isDM)
                {
                    await channel.SendMessageAsync(
                        $"Please send a log file you want me to analyze. You can try asking some questions, but I'm unlikely to help you, it's better to ask in <#{HelpChannelId}> instead.");
                    return;
                }
            }

            // Only be a smartass if nothing else triggered
            if (content.Length < 35 && ContainsAny("can i ask something?", "can someone help me?", "can anyone help me?"))
            {
                await msg.ReplyAsync($"<https://dontasktoask.com/>");
            }

            if (ContainsAny("flashbangz"))
            {
                await msg.ReplyAsync($"If you are using a FlashBangZ repack, it's strongly recommend that you remove it and download a fresh version of the game. Until you get rid of this repack you will be very unlikely to receive any help here, and any cards you post might be removed on sight. His repacks have been caught to contain malware in the past, and they are infamous for being riddled with issues. You can read more about it [here](<https://discordapp.com/channels/447114928785063977/447120583189331968/506923193454428182>).");
            }

            if (ContainsAny("what is the meaning of life"))
            {
                await msg.ReplyAsync($"The meaning of life is to play Koikatsu and worship Chikarin");
            }
        }

        private string CleanUpLogDuplicates(string fileContent)
        {
            var sb = new StringBuilder(fileContent.Length);

            // Remove duplicate log lines
            var lastLine = string.Empty;
            foreach (var line in fileContent.Split('\n'))
            {
                if (line.StartsWith('[') && line.Equals(lastLine))
                {
                    lastLine = null;
                }
                else
                {
                    sb.Append(line);
                    sb.Append('\n');
                    lastLine = line;
                }
            }

            return sb.ToString();
        }

        private string CleanUpLogForPastebin(string fileContent)
        {
            var sb = new StringBuilder(fileContent.Length);

            foreach (var line in fileContent.Split('\n'))
            {
                // Remove very rarely useful logs
                //if (lastLine != null && lastLine.StartsWith('[') && line.StartsWith("Non platform assembly:"))
                if (line.StartsWith("Platform assembly:") || line.StartsWith("Non platform assembly:"))
                    continue;
                if (Regex.IsMatch(line, @"^\[Debug\s*:\s*XUnity\."))
                    continue;

                sb.Append(line);
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static async Task AddReaction(SocketUserMessage msg, string[] emoteStrings)
        {
            SocketTextChannel channel = (SocketTextChannel)msg.Channel;

            Emote resultEmote = null;
            foreach (var emoteString in emoteStrings)
            {
                if (Emote.TryParse(emoteString, out var em) && !channel.Guild.Emotes.All(x => x.Id != em.Id))
                {
                    resultEmote = em;
                    break;
                }
            }

            if (resultEmote == null)
                throw new InvalidDataException($"No valid emoji found to react with from the list: " + string.Join("; ", emoteStrings));

            await msg.AddReactionAsync(resultEmote);
        }

        private static void SearchForCommonQuestions(string text, List<string> listOfSins)
        {
            bool ContainsAny(params string[] testStrings)
            {
                return testStrings.Any(testStr => text.Contains(testStr, StringComparison.InvariantCultureIgnoreCase));
            }

            bool ContainsAll(params string[] testStrings)
            {
                return testStrings.All(testStr => text.Contains(testStr, StringComparison.InvariantCultureIgnoreCase));
            }

            if (ContainsAll("how", "update", "kkmanager"))
            {
                listOfSins.Add($"If you have issues updating KKManager, check [this guide](<https://youtu.be/ceg2XXGNwcU>).");
            }

            if (ContainsAny("what is"))
            {
                if (ContainsAny(" kkp") || ContainsAll("koikat", "party"))
                {
                    listOfSins.Add(
                        $"Koikatsu Party is the Steam version of Koikatu. It contains professional English and Chinese translations. Check <#{FaqsChannelId}> for more info.");
                }
            }

            if (ContainsAny("subtitle", "subs") && ContainsAny("missing", "disappear", "hf patch", "enable", "gone", "dont have", "don't have", "stopped work", "not work", "no longer"))
                listOfSins.Add(
                    "If you want to turn on subtitles during H-Scenes and/or chara maker:" +
                    "\n    A - If the subtitles disappeared after installing HF Patch - you have to install HF Patch again and this time check the optional \"Subtitles\" plugin (it's off by default because the translations are very low quality)." +
                    "\n    B - Go to the plugin settings and search for the \"Show Subtitles\" setting and turn it on. If the setting is missing, you need to install the KK_Subtitles plugin either by installing HF Patch or by installing it manually from [here](<https://github.com/DeathWeasel1337/KK_Plugins#subtitles>).");

            if (ContainsAll("overlay", "guide") && ContainsAll("any", "find"))
                listOfSins.Add(
                    $"You can find guides for making overlays [here](<https://github.com/ManlyMarco/Illusion-Overlay-Mods/tree/master/Guide>) and in the <#{GuidesChannelId}> channel.");

            if (ContainsAll("crash") && ContainsAny("card sav", "saving card"))
                listOfSins.Add(
                    "If your game crashes when saving cards in maker, try to lower the card image upsamplng ratio to 1 (Press F1 > Click on Plugin Settings > Search for `screenshot` to find the setting).");

            if (ContainsAll("black") && ContainsAny("title", "main menu"))
                listOfSins.Add(
                    "If your title screen turned black and the game is broken after Steam updated your game, you have to either install HF Patch v3.8 or newer, or roll back to a previous update (check pinned messages for more information).");

            if (ContainsAll("shoulders") && ContainsAny("collapsed", "messed up", "broken", "injured", "wonky", "out of whack", "back to normal"))
                listOfSins.Add(
                    "Broken/drooping shoulders in character maker are caused by Stiletto. To fix: F1 > Plugin Settings > Disable Maker IK (uncheck) > Restart the game. If that doesn't work, go into Bepinex/plugins and delete the Stiletto.dll file.");

            if (ContainsAll("character", "missing") && ContainsAny("head", "clothes", "hair"))
                listOfSins.Add(
                    "If your characters are missing their heads or parts of clothes or hair in story mode, try to turn on High Poly mode and turn off Async clothes loading (Press F1 > Click on Plugin Settings > Search for the setting names).");

            if (ContainsAll("trap", "penis") && ContainsAny("not", "issue", "disappe"))
                listOfSins.Add(
                    "If peni$ of your trap character doesn't show in story mode try to turn off the Clothing state persistence setting (Press F1 > Click on Plugin Settings > Search for `skin effects` to find the setting).");
            if (ContainsAny("PMX"))
                listOfSins.Add(
                    "PMX Exporter plugin is very outdated and will break the game if installed. Remove it to fix your issues. Additionally, we do not help with export plugins here because they have been severely abused in the past, so you're on your own in that regard.");
            if (ContainsAll("stuck", "tpose"))
                listOfSins.Add(
                    "If you use the PMX Exporter plugin or any other export plugin, remove it and see if that fixes the issue. We can not give any more help with export plugins here.");

            if (ContainsAny("where", "how to", "howto", "how can", "how i can", "how do", "have link to", "have a link to"))
            {
                //todo ThenContainsAny?
                if (ContainsAny("buy", "download", " dl ", "get", "find"))
                {
                    if (ContainsAny("cards", "scenes"))
                        listOfSins.Add(
                            $"You can download character cards and studio scenes in <#{CardSharingChannelId}> and <#{SceneSharingChannelId}> channels, or at the sites listed in <#{FaqsChannelId}>. If you are looking for specific cards, ask in the <#{ClassChatterChannelId}> channel. Cards and scenes are stored in special .png files with embedded game data - when downloading them make sure to get the real card file instead of a thumbnail. On Discord click on the card image and then on open original to get the real file as shown [here](<https://cdn.discordapp.com/attachments/555528419442425883/713670102456991794/open_original_example.png>). On Pixiv the card files are usually linked in the description.");
                    else if (ContainsAny("the game", "this game", "the dlc", "the expansion", "afterschool", "after school", "koikat",
                        " kk fa"))
                    {
                        listOfSins.Add($"Check the <#{FaqsChannelId}> channel for links to buy the game and expansions.");

                        if (ContainsAny("for free", "pirate", "without paying", "without having to pay"))
                            listOfSins.Add(
                                "We do not support piracy on the server, asking for and sharing links to pirate downloads of the game is against server rules and can net you a warning.");
                    }

                    if (ContainsAny("darkness"))
                        listOfSins.Add(
                            "The Darkness expansion was a preorder exclusive and can no longer be legally acquired. It offers very little contents, so you aren't missing all that much if you don't have it.");

                    if (ContainsAny("hf patch", "hfpatch"))
                    {
                        listOfSins.Add($"You can download the latest versions of HF Patches [here](<https://www.patreon.com/collection/52344?view=expanded>) (each patch is for a different game). If you have trouble with downloading the torrent file then try using qBittorrent. Check the <#{FaqsChannelId}> channel for more info.");
                    }
                }
                else if (ContainsAny("darkness"))
                {
                    listOfSins.Add(
                        "If you want to trigger the Darkness scene in story mode: Take a non-virgin girl with the correct personality to the third floor door while there's no teacher nearby and interact with the icon next to one of the doors.");
                }

                if (ContainsAny("where") && ContainsAny("place", "located", "location", "userdata", "installed", "store", "saved", "find", " put ", "move"))
                {
                    if (ContainsAny("game", "koikat", "honeyco", "honey co", "emotion", "studio", "install "))
                    {
                        listOfSins.Add("To find where the game is installed:" +
                                       "\n    A - Go to properties of the game shortcut and check the `Target` field." +
                                       "\n    B - If you have the Steam version, open Steam library > Properties of the game > Local files > Browse");
                    }

                    if (ContainsAny("card", "chara", "scene", "save ", "userdata", "outfit"))
                    {
                        listOfSins.Add(
                            "Almost all user data (e.g. cards, scenes, screenshots) is saved in subfolders inside the UserData folder inside your game directory. Read [this](<https://pastebin.com/vMP84w9k>) to see which files go to which subfolder.");
                    }

                    if (ContainsAny("zipmod"))
                    {
                        listOfSins.Add(".zipmod files contain varous content mods and need to be placed inside the 'mods' folder located directly inside the game directory. You can place it in any subfolder ('MyMods' or similar is recommended), but avoid modyfying the 'Sideloader Modpack' subfolders by hand.\n" + 
                                       "At least the latest versions of BepInEx5 and BepisPlugins are required for zipmods to be loaded. Some zipmods may require additional plugins like for example AnimationLoader or MaterialEditor.");
                    }
                    
                    if (ContainsAny("plugin", "dll", "patcher"))
                    {
                        listOfSins.Add("If there is a readme file included with the download, follow the installation steps in it. If there is none:" + 
                                       "Most plugins are distributed in .zip archives that you can extract directly into your game directory (there should be a BepInEx folder in them). If you have a single dll file, you have to place it in `BepInEx/plugins` if it's a plugin, or `BepInEx/patchers` if it's a patcher.\n" + 
                                       "At least the latest version of BepInEx5 is required for most games (latest BepInEx6 nightly build is required for HoneyCome).");
                    }
                }


                if (ContainsAny("plugin hotkeys"))
                    listOfSins.Add(
                        "You can check and modify hotkeys of many plugins from the plugin settings screen. You can open plugin settings in main game by entering game settings (usually by pressing F1) and clicking the Plugin Settings button in top right corner. You can open plugin settings in studio by pressing F1.");
                else if (ContainsAny("open plugin settings"))
                    listOfSins.Add(
                        "You can open plugin settings in main game by entering game settings (usually by pressing F1) and clicking the Plugin Settings button in top right corner. You can open plugin settings in studio by pressing F1.");

                if (ContainsAny("update bepinex"))
                    listOfSins.Add(
                        "It's recommended that you don't update BepInEx manually. Some plugins might need to be updated or removed, and some configuration might need to be changed when updating it. Doing this improperly can seriously break things. Instead update by grabbing the latest version of a mod pack like for example the HF Patch.");

                if (ContainsAny("update kkmanager"))
                    listOfSins.Add("You can get the latest version of KKManager [here](<https://github.com/IllusionMods/KKManager/releases/latest>). Follow the instructions in the release post to update your existing KKManager installation.");

                if (ContainsAny("character") && ContainsAny("request"))
                    listOfSins.Add(
                        $"If you want to request a character to be made, you can ask in the <#{CharaRequestsChannelId}> channel. Note that you need to be at least level 10 on the server to post there. You can earn levels by talking on the server and posting your creations. To check your current level go to <#{BotControlChannelId}> and use the `.xp` command.");

                if (ContainsAny("uncensor"))
                    listOfSins.Add(
                        $"If you want to uncensor the game, or have issues with uncensors (e.g. scrunching peni$) check the <#{FaqsChannelId}> channel for more info. The easiest way to uncensor the game is to install the latest version of HF Patch.");

                if (ContainsAny("output_log", "output log", "log file"))
                {
                    listOfSins.Add(OutputLogPleaseGive);
                }

                if (ContainsAny("hf patch", "hfpatch") && ContainsAny("password"))
                {
                    listOfSins.Add(
                        "You can find the HF Patch password right next to the download links in the patreon post you downloaded it from.");
                }
            }

            if (ContainsAny("custom intro", "meme intro", "stupid intro") && ContainsAny("disable", "remove", "uninstall"))
            {
                listOfSins.Add(
                    "If you want to remove the custom intro sounds (meme sounds on game start) please delete the `BepInEx\\Plugins\\IntroClips` folder.");
            }
        }

        private static bool FileIsValidLogFile(Attachment textAttachment)
        {
            return textAttachment.Filename.StartsWith("output_log", StringComparison.OrdinalIgnoreCase) ||
                   textAttachment.Filename.StartsWith("LogOutput", StringComparison.OrdinalIgnoreCase);
        }
    }
}
