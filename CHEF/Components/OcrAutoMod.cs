using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Tesseract;

namespace CHEF.Components
{
    /// <summary>
    /// OCR-based AutoMod component that scans images in messages for text violations.
    /// Based on AOCR implementation from https://github.com/SomeAspy/AOCR
    /// </summary>
    public class OcrAutoMod : Component
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private TesseractEngine _ocrEngine;
        private const string TessDataPath = "tessdata";
        
        public OcrAutoMod(DiscordSocketClient client) : base(client)
        {
        }

        public override async Task SetupAsync()
        {
            try
            {
                // Initialize Tesseract OCR engine
                // Note: Requires tessdata folder with eng.traineddata file
                if (Directory.Exists(TessDataPath))
                {
                    _ocrEngine = new TesseractEngine(TessDataPath, "eng", EngineMode.Default);
                    Logger.Log("OCR AutoMod: Tesseract engine initialized successfully");
                }
                else
                {
                    Logger.Log($"OCR AutoMod: tessdata folder not found at {Path.GetFullPath(TessDataPath)}. OCR AutoMod will not function.");
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Log($"OCR AutoMod: Failed to initialize Tesseract engine: {e.Message}");
                return;
            }

            // Register message handlers
            Client.MessageReceived += OnMessageReceived;
            Client.MessageUpdated += OnMessageUpdated;

            await Task.CompletedTask;
        }

        private Task OnMessageReceived(SocketMessage message)
        {
            try
            {
                return ProcessMessageAsync(message);
            }
            catch (Exception e)
            {
                Logger.Log($"OCR AutoMod: Error processing message {message.GetJumpUrl()}: {e}");
                return Task.CompletedTask;
            }
        }

        private Task OnMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            try
            {
                return ProcessMessageAsync(after);
            }
            catch (Exception e)
            {
                Logger.Log($"OCR AutoMod: Error processing updated message: {e}");
                return Task.CompletedTask;
            }
        }

        private async Task ProcessMessageAsync(SocketMessage smsg)
        {
            if (_ocrEngine == null) return;
            
            // Skip bot messages
            if (smsg.Author.IsBot || smsg.Author.IsWebhook) return;

            // Must be in a guild
            if (!(smsg is SocketUserMessage msg) || !(msg.Channel is SocketTextChannel channel)) return;

            var member = msg.Author as SocketGuildUser;
            if (member == null) return;

            // Skip moderators/administrators (similar to Discord AutoMod behavior)
            if (member.GuildPermissions.ManageGuild || member.GuildPermissions.Administrator) return;

            // Check if message has images to scan
            var imagesToCheck = new List<string>();

            // Check attachments
            foreach (var attachment in msg.Attachments)
            {
                if (IsImageAttachment(attachment))
                {
                    imagesToCheck.Add(attachment.Url);
                }
            }

            // Check embeds
            foreach (var embed in msg.Embeds)
            {
                if (!string.IsNullOrEmpty(embed.Image?.Url))
                {
                    imagesToCheck.Add(embed.Image.Value.Url);
                }
                if (!string.IsNullOrEmpty(embed.Thumbnail?.Url))
                {
                    imagesToCheck.Add(embed.Thumbnail.Value.Url);
                }
            }

            // Process each image
            foreach (var imageUrl in imagesToCheck)
            {
                await ProcessImageAsync(member, msg, channel, imageUrl);
            }
        }

        private bool IsImageAttachment(Attachment attachment)
        {
            var extension = Path.GetExtension(attachment.Url.Split('?')[0]).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || 
                   extension == ".gif" || extension == ".webp" || extension == ".bmp";
        }

        private async Task ProcessImageAsync(SocketGuildUser member, SocketUserMessage message, 
            SocketTextChannel channel, string imageUrl)
        {
            try
            {
                // Download image
                byte[] imageBytes;
                using (var response = await _httpClient.GetAsync(imageUrl))
                {
                    if (!response.IsSuccessStatusCode) return;
                    imageBytes = await response.Content.ReadAsByteArrayAsync();
                }

                // Perform OCR
                string ocrText;
                using (var img = Pix.LoadFromMemory(imageBytes))
                using (var page = _ocrEngine.Process(img))
                {
                    ocrText = page.GetText();
                }

                if (string.IsNullOrWhiteSpace(ocrText)) return;

                // Get guild's AutoMod rules
                var autoModRules = await channel.Guild.GetAutoModRulesAsync();

                // Check against each rule
                foreach (var rule in autoModRules)
                {
                    if (!rule.Enabled) continue;
                    
                    // Skip mention spam and spam trigger types
                    if (rule.TriggerType == AutoModTriggerType.MentionSpam || 
                        rule.TriggerType == AutoModTriggerType.Spam) continue;

                    // Check if member has exempt role
                    var hasExemptRole = false;
                    foreach (SocketRole exemptRole in rule.ExemptRoles)
                    {
                        foreach (SocketRole memberRole in member.Roles)
                        {
                            if (memberRole.Id == exemptRole.Id)
                            {
                                hasExemptRole = true;
                                break;
                            }
                        }
                        if (hasExemptRole) break;
                    }
                    if (hasExemptRole) continue;

                    // Check if channel is exempt  
                    var isExemptChannel = false;
                    foreach (SocketGuildChannel exemptChannel in rule.ExemptChannels)
                    {
                        if (channel.Id == exemptChannel.Id)
                        {
                            isExemptChannel = true;
                            break;
                        }
                    }
                    if (isExemptChannel) continue;

                    // Prepare text for checking
                    string cleanedText = ocrText.Replace("\n", "").Replace("\r", "").ToLowerInvariant();

                    // Apply allow list
                    foreach (var allowedWord in rule.AllowList)
                    {
                        cleanedText = cleanedText.Replace(allowedWord.ToLowerInvariant(), "");
                    }

                    bool violated = false;

                    // Check keyword filter
                    foreach (var keyword in rule.KeywordFilter)
                    {
                        if (cleanedText.Contains(keyword.ToLowerInvariant()))
                        {
                            violated = true;
                            break;
                        }
                    }

                    // Check regex patterns
                    if (!violated)
                    {
                        foreach (var pattern in rule.RegexPatterns)
                        {
                            try
                            {
                                if (Regex.IsMatch(cleanedText, pattern, RegexOptions.IgnoreCase))
                                {
                                    violated = true;
                                    break;
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Log($"OCR AutoMod: Invalid regex pattern '{pattern}': {e.Message}");
                            }
                        }
                    }

                    if (violated)
                    {
                        await ExecuteAutoModActionsAsync(member, message, rule, ocrText, imageUrl);
                        return; // Stop after first violation
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log($"OCR AutoMod: Error processing image {imageUrl}: {e}");
            }
        }

        private async Task ExecuteAutoModActionsAsync(SocketGuildUser member, SocketUserMessage message,
            IAutoModRule rule, string ocrText, string imageUrl)
        {
            foreach (var action in rule.Actions)
            {
                try
                {
                    switch (action.Type)
                    {
                        case AutoModActionType.BlockMessage:
                            // Delete the message
                            if (message.Channel is ITextChannel textChannel)
                            {
                                try
                                {
                                    await message.DeleteAsync();
                                    Logger.Log($"OCR AutoMod: Deleted message from {member.Username} (Rule: {rule.Name})");
                                }
                                catch (Exception e)
                                {
                                    Logger.Log($"OCR AutoMod: Failed to delete message: {e.Message}");
                                }

                                // Try to notify user via DM
                                try
                                {
                                    var dmChannel = await member.CreateDMChannelAsync();
                                    var embed = new EmbedBuilder()
                                        .WithTitle("AutoMod Alert")
                                        .WithDescription(action.CustomMessage.GetValueOrDefault() ?? "Your message violated AutoMod rules")
                                        .AddField("OCR Detected Text", ocrText.Length > 1000 ? ocrText.Substring(0, 1000) + "..." : ocrText)
                                        .WithColor(Color.Red)
                                        .WithTimestamp(DateTimeOffset.Now)
                                        .Build();
                                    await dmChannel.SendMessageAsync(embed: embed);
                                }
                                catch
                                {
                                    // Silently fail if can't DM user
                                }
                            }
                            break;

                        case AutoModActionType.SendAlertMessage:
                            // Send alert to specified channel
                            if (action.ChannelId != null)
                            {
                                try
                                {
                                    var alertChannel = member.Guild.GetTextChannel(action.ChannelId.Value);
                                    if (alertChannel != null)
                                    {
                                        var embed = new EmbedBuilder()
                                            .WithAuthor(member.Username, member.GetAvatarUrl())
                                            .WithTitle($"OCR AutoMod Alert - Rule: {rule.Name}")
                                            .AddField("OCR Recognized Text", ocrText.Length > 1000 ? ocrText.Substring(0, 1000) + "..." : ocrText)
                                            .AddField("Image", imageUrl)
                                            .WithColor(Color.Orange)
                                            .WithTimestamp(DateTimeOffset.Now)
                                            .Build();
                                        await alertChannel.SendMessageAsync(embed: embed);
                                        Logger.Log($"OCR AutoMod: Alert sent for {member.Username} (Rule: {rule.Name})");
                                    }
                                }
                                catch (Exception e)
                                {
                                    Logger.Log($"OCR AutoMod: Failed to send alert: {e.Message}");
                                }
                            }
                            break;

                        case AutoModActionType.Timeout:
                            // Timeout the user
                            if (action.TimeoutDuration != null && member.Guild.CurrentUser.GuildPermissions.ModerateMembers)
                            {
                                try
                                {
                                    await member.SetTimeOutAsync(action.TimeoutDuration.Value, 
                                        new RequestOptions { AuditLogReason = action.CustomMessage.GetValueOrDefault() ?? $"OCR AutoMod: {rule.Name}" });
                                    Logger.Log($"OCR AutoMod: Timed out {member.Username} for {action.TimeoutDuration.Value.TotalMinutes} minutes (Rule: {rule.Name})");
                                }
                                catch (Exception e)
                                {
                                    Logger.Log($"OCR AutoMod: Failed to timeout user: {e.Message}");
                                }
                            }
                            break;
                    }
                }
                catch (Exception e)
                {
                    Logger.Log($"OCR AutoMod: Error executing action {action.Type}: {e}");
                }
            }
        }

        public override void Dispose()
        {
            _ocrEngine?.Dispose();
            base.Dispose();
        }
    }
}
