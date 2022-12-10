import fs from "fs/promises";
import http from "http";
import { google } from "googleapis";
import { youtube } from "@googleapis/youtube";
import open from "open";

const PORT = 9999;

const oauthConfig = JSON.parse(
  await fs.readFile("./youtube_oauth.json", "utf8")
);

const discordWebhookUrl = JSON.parse(
  await fs.readFile("./discord_webhook.json", "utf8")
);

const oauth2Client = new google.auth.OAuth2(
  oauthConfig.installed.client_id,
  oauthConfig.installed.client_secret,
  `http://localhost:${PORT}`
);

const doOauthFlow = () =>
  new Promise((resolve, reject) => {
    const authorizationUrl = oauth2Client.generateAuthUrl({
      access_type: "offline",
      scope: ["https://www.googleapis.com/auth/youtube.readonly"],
      include_granted_scopes: true,
    });

    open(authorizationUrl);

    const server = http
      .createServer(async (req, res) => {
        try {
          console.log(`Received request: ${req.url}`);
          const url = new URL(req.url, `http://${req.headers.host}`);
          const code = url.searchParams.get("code");
          if (!code) {
            res.statusCode = 404;
            res.end("No OAuth2 code provided");
            return;
          }

          const { tokens } = await oauth2Client.getToken(
            url.searchParams.get("code")
          );
          await fs.writeFile(
            "./youtube_oauth_tokens.json",
            JSON.stringify(tokens)
          );
          res.statusCode = 200;
          res.end("You can now close this window");
          res.on("close", () => {
            server.close();
            resolve(tokens);
          });
        } catch (err) {
          res.statusCode = 500;
          console.error(err);
          res.end();
          res.on("close", () => {
            server.close();
            reject(err);
          });
        }
      })
      .listen(PORT);
  });

const getTokens = async () => {
  try {
    return JSON.parse(await fs.readFile("./youtube_oauth_tokens.json", "utf8"));
  } catch {
    return doOauthFlow();
  }
};

oauth2Client.setCredentials(await getTokens());

const yt = youtube({
  version: "v3",
  auth: oauth2Client,
});
const { data } = await yt.liveBroadcasts.list({
  part: ["snippet"],
  mine: true,
  type: "all",
  maxResults: 1,
  order: "date",
});
// console.log(data.items);
if (data.items.length === 0) throw new Error("No live streams found");
const item = data.items[0];
if (item.snippet.actualEndTime)
  throw new Error("Most recent live stream has already ended");
const broadcastUrl = `https://youtu.be/${item.id}`;

const res = await fetch(discordWebhookUrl, {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
  },
  body: JSON.stringify({
    content: `Now streaming: ${broadcastUrl}`,
  }),
});
if (!res.ok)
  console.error(
    "Failed to send Discord message:",
    res.status,
    res.statusText,
    await res.body().catch(() => undefined)
  );
