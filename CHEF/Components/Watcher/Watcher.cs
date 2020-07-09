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
            "Please send your `output_log.txt` file, it will help us help you. This file should exist in your game's root directory (next to the Koikatsu exe). Simply drag this file over to discord to upload it. If the file is too large to upload then compress it and try again. If you can't see `output_log.txt` in your game directory then your game is most likely very outdated. In that case either use the latest HF Patch to update, or try looking for the `output_log.txt` file inside of the `*_Data` folders in the game directory.";

        private int _maxLogFileSize = 9 * 1024 * 1024;

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

            bool Contains(string testStr)
            {
                return msg.Content.Contains(testStr, StringComparison.InvariantCultureIgnoreCase);
            }

            var isHelp = channel.Name == "help";
            var isOther = channel.Name == "mod-programming";

            bool? canBeHelped = null;

            if (isHelp)
            {
                if (msg.Content == "nohelp")
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
                if (msg.Content == "chika" ||
                    msg.Content == "bot" ||
                    Contains("chikarin") ||
                    Contains("techinician chikarin") ||
                    Contains("help bot") ||
                    Contains("i hate robots"))
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
                            $"```as\n{string.Join("``````as\n", exceptionOutputStrings.TakeWhile(s => (totalLength += s.Length + 13) < 2000))}```");
                    }

                    if (textAttachments.Count == 0)
                    {
                        var hasImages = msg.Attachments.Any(x => string.Equals(Path.GetExtension(x.Url), ".png",
                            StringComparison.OrdinalIgnoreCase));

                        if ((Contains("crash") || Contains("bug")) && (Contains("help") || hasImages))
                        {
                            await channel.SendMessageAsync($"{msg.Author.Mention}" + OutputLogHowToGet);
                        }
                        else if (Contains("blue") &&
                                 (Contains("characters") || Contains("ghost") || Contains("persons") ||
                                  Contains("people") || (Contains("all") && Contains("girls"))))
                        {
                            listOfSins.Add(
                                "The blue characters around the school are just random mobs to make the school feel less empty, similar to Persona 5. You can turn them off in plugin settings (F1 > Plugin settings > Search for `mob`).");
                        }
                    }

                    if (msg.Attachments.Count == 0)
                    {
                        if (Contains("where") || Contains("how to") || Contains("howto") ||
                            Contains("how can") || Contains("how i can") || Contains("how do"))
                        {
                            if (Contains("buy") || Contains("download") || Contains(" dl ") || Contains("get"))
                            {
                                if (Contains("the game") || Contains("the dlc") || Contains("the expansion") ||
                                    Contains("afterschool") || Contains("after school") || Contains("koikat"))
                                {
                                    listOfSins.Add(
                                        "Check the #faqs channel for links to buy the game and expansions");
                                }

                                if (Contains("hf patch") || Contains("hfpatch"))
                                {
                                    listOfSins.Add(
                                        "You can download the latest version of HF Patch here: <https://github.com/ManlyMarco/KK-HF_Patch/releases/latest>. If you have trouble with downloading the torrent file then try using qBittorrent. Check the #faqs channel for more info.");
                                }
                            }

                            if(Contains("uncensor"))
                                listOfSins.Add("If you want to uncensor the game check the #faqs channel for more info.");

                            if (textAttachments.Count == 0
                                && (Contains("output_log") || Contains("output log") || Contains("log file")))
                            {
                                listOfSins.Add(OutputLogHowToGet);
                            }

                            if ((Contains("hf patch") || Contains("hfpatch")) && Contains("password"))
                            {
                                listOfSins.Add(
                                    "You can find the HF Patch password right next to the download links in the patreon post you downloaded it from.");
                            }
                        }

                        if ((Contains("custom intro") || Contains("meme intro") || Contains("stupid intro")) &&
                            (Contains("disable") || Contains("remove") || Contains("uninstall")))
                        {
                            listOfSins.Add(
                                "If you want to remove the custom intro sounds (meme sounds on game start) please delete the `BepInEx\\Plugins\\IntroClips` folder.");
                        }
                    }

                    //var yandexRes = await _imageParser.Try(msg);

                    //if (yandexRes.Length > 1)
                    //    await channel.SendMessageAsync(yandexRes);

                    if (listOfSins.Count > 0)
                    {
                        if (listOfSins.Count == 1)
                        {
                            await channel.SendMessageAsync($"{msg.Author.Mention} {listOfSins.First()}");
                        }
                        else
                        {
                            await channel.SendMessageAsync(
                                $"{msg.Author.Mention} I found answers to some common issues that might be helpful to you:\n• {string.Join("\n• ", listOfSins)}");
                        }

                        return;
                    }
                }

                // Only be a smartass if nothing else triggered
                if (Contains("can i ask") || Contains("can someone help me"))
                {
                    await channel.SendMessageAsync($"{msg.Author.Mention} https://dontasktoask.com/");
                }

                if(Contains("what is the meaning of life"))
                {
                    await channel.SendMessageAsync($"{msg.Author.Mention} To play Koikatsu");
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
