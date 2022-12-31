import fs from "fs/promises";
import { notify } from "./notify.js";

const discordWebhookUrl = JSON.parse(
  await fs.readFile("discord_webhook.json", "utf8")
);

await notify({ discordWebhookUrl });
