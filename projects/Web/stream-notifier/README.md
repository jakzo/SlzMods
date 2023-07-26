Posts a link to your latest currently-running Youtube live stream to Discord.

## Usage

- Create a file in the current directory named `discord_webhook.json` then create a Discord webhook URL for a channel and put in this file as a JSON string
  - Contents should look like `"https://discordapp.com/api/webhooks/123.../xyz..."`
- Start with: `npx @jakzo/stream-notifier`
