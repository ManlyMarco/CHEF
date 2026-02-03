# OCR AutoMod Extension

## Overview

The OCR AutoMod component extends Discord's AutoMod functionality by scanning images in messages for text that violates server rules. It uses OCR (Optical Character Recognition) to detect text in images and checks it against your server's configured AutoMod rules.

Based on the implementation from [AOCR](https://github.com/SomeAspy/AOCR).

## Features

- **Automated Image Scanning**: Automatically scans all images posted in your Discord server
- **AutoMod Integration**: Works seamlessly with Discord's native AutoMod rules
- **Multiple Trigger Types**: Supports keyword filters and regex patterns
- **Standard AutoMod Actions**: Executes all standard AutoMod actions:
  - Block/delete messages
  - Send alerts to designated channels
  - Timeout users
- **Smart Exemptions**: Respects role and channel exemptions configured in AutoMod rules
- **Moderator Exemption**: Automatically exempts users with Manage Server or Administrator permissions

## Setup Instructions

### 1. Install Tesseract Language Data

The OCR AutoMod component requires Tesseract OCR engine language data files. Follow these steps:

1. Download the English language data file:
   - Direct link: https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata
   - Or clone the repository: `git clone https://github.com/tesseract-ocr/tessdata_best.git`

2. Create a `tessdata` folder in the same directory as the CHEF executable

3. Place the `eng.traineddata` file inside the `tessdata` folder

**Directory structure should look like:**
```
CHEF/
├── CHEF.dll
├── tessdata/
│   └── eng.traineddata
└── ... (other files)
```

### 2. Configure Discord AutoMod Rules

The OCR AutoMod component automatically uses your server's existing AutoMod rules. To set up rules:

1. Go to your Discord server settings
2. Navigate to Safety Setup → AutoMod
3. Create or edit AutoMod rules with:
   - **Trigger Type**: Keyword or Keyword & Regex Patterns
   - **Keywords/Patterns**: Add words or regex patterns you want to block
   - **Allow List**: (Optional) Words that should be allowed even if they match blocked patterns
   - **Actions**: Choose what happens when a rule is violated:
     - Block Message
     - Send Alert to Channel
     - Timeout User
   - **Exemptions**: (Optional) Roles or channels that should be exempt from the rule

4. Enable the rules

The OCR AutoMod will automatically:
- Scan images posted in your server
- Extract text using OCR
- Check the text against your AutoMod rules
- Execute the configured actions if violations are found

## How It Works

1. **Message Monitoring**: The component monitors all messages posted in your Discord server

2. **Image Detection**: When a message contains:
   - Image attachments (.png, .jpg, .jpeg, .gif, .webp, .bmp)
   - Embedded images
   - Embedded thumbnails

3. **OCR Processing**: Each image is downloaded and processed through Tesseract OCR to extract any text

4. **AutoMod Rule Checking**: The extracted text is checked against your server's AutoMod rules:
   - Skip if user has Administrator or Manage Server permissions (unless overridden)
   - Skip if user has an exempt role
   - Skip if posted in an exempt channel
   - Check against keyword filters
   - Check against regex patterns
   - Apply allow list exclusions

5. **Action Execution**: If a violation is detected, the configured AutoMod actions are executed:
   - **Block Message**: Deletes the message and attempts to DM the user
   - **Send Alert**: Posts an alert embed in the configured channel showing the detected text
   - **Timeout**: Applies a timeout to the user for the configured duration

## Limitations

- **OCR Accuracy**: OCR is not perfect. Text recognition works best on clear, high-contrast images. Stylized fonts, handwriting, or low-quality images may not be recognized accurately.
- **Performance**: Processing images with OCR requires CPU resources. Large images or high message volume may impact performance.
- **Supported Trigger Types**: Only works with Keyword and Regex pattern triggers. Mention spam and generic spam triggers are skipped.
- **Language Support**: Currently configured for English language detection only (can be extended by adding other language data files).

## Troubleshooting

### "tessdata folder not found" Error

**Problem**: OCR AutoMod logs indicate that the tessdata folder is missing.

**Solution**: 
1. Ensure the `tessdata` folder exists in the same directory as CHEF.dll
2. Check that the folder name is exactly `tessdata` (lowercase)
3. Verify the folder contains the `eng.traineddata` file

### OCR Not Detecting Text

**Problem**: Images with text are not triggering AutoMod rules.

**Possible Causes**:
1. Text in image is stylized or difficult to read
2. Image quality is too low
3. AutoMod rule is not properly configured
4. User has exempt role or posted in exempt channel
5. User is a moderator/administrator

**Debugging Steps**:
1. Check the logs for any OCR errors
2. Test with a simple image containing clear, plain text
3. Verify your AutoMod rule is enabled and has the correct keywords
4. Test with a non-moderator account without exempt roles

### Actions Not Being Executed

**Problem**: OCR detects violations but actions aren't executed.

**Possible Causes**:
1. Bot lacks necessary permissions (Manage Messages for delete, Moderate Members for timeout)
2. Target user has higher roles than the bot
3. DMs are disabled for the user (only affects DM notifications)

**Solution**:
1. Ensure the bot has appropriate permissions
2. Check role hierarchy in server settings
3. Check bot logs for permission errors

## Advanced Configuration

### Adding Other Languages

To support additional languages:

1. Download language data files from https://github.com/tesseract-ocr/tessdata_best
2. Place the `.traineddata` files in the `tessdata` folder
3. Modify `OcrAutoMod.cs` line 42 to change `"eng"` to your desired language code (e.g., `"fra"` for French, `"deu"` for German)

### Performance Tuning

For high-traffic servers, consider:

1. Using faster Tesseract language data (tessdata_fast instead of tessdata_best) - less accurate but faster
2. Limiting which channels are monitored by using exempt channels in AutoMod rules
3. Running the bot on a server with more CPU resources

## Support

For issues or questions:
- Check the CHEF bot logs for error messages
- Refer to the AOCR project: https://github.com/SomeAspy/AOCR
- Review Discord.Net documentation: https://docs.discordnet.dev/

## License

This component is part of the CHEF Discord bot project and inherits its license.
