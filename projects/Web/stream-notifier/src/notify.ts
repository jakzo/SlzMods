import fs from "fs/promises";
import http from "http";
import { google } from "googleapis";
import { youtube } from "@googleapis/youtube";
import open from "open";
import getPort from "get-port";

import { DEFAULT_OAUTH2_CONFIG, OAuth2Config } from "./oauth2-config.js";

export interface NotifyOpts {
  discordWebhookUrl: string;
  port?: number;
  oauth2Config?: OAuth2Config;
}

export const OAUTH2_TOKENS_FILENAME = "youtube_oauth_tokens.json";

export const notify = async (opts: NotifyOpts) => {
  const {
    discordWebhookUrl,
    port = await getPort(),
    oauth2Config = DEFAULT_OAUTH2_CONFIG,
  } = opts;

  const oauth2Client = new google.auth.OAuth2(
    oauth2Config.installed.client_id,
    oauth2Config.installed.client_secret,
    `http://localhost:${port}`
  );

  let hasSetCredentials = false;

  const setCredentialsFromFile = async () => {
    oauth2Client.setCredentials(
      JSON.parse(await fs.readFile(OAUTH2_TOKENS_FILENAME, "utf8"))
    );
    hasSetCredentials = true;
  };

  const setCredentialsFromOauthFlow = () =>
    new Promise<void>((resolve, reject) => {
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
            const url = new URL(req.url!, `http://${req.headers.host}`);
            const code = url.searchParams.get("code");
            if (!code) {
              res.statusCode = 404;
              res.end("No OAuth2 code provided");
              return;
            }

            const { tokens } = await oauth2Client.getToken(code);
            await fs.writeFile(OAUTH2_TOKENS_FILENAME, JSON.stringify(tokens));
            res.statusCode = 200;
            res.end(`
              <html>
                <head>
                  <script>window.close();</script>
                </head>
                <body>You can now close this window.</body>
              </html>
            `);
            res.on("close", () => {
              server.close();
              oauth2Client.setCredentials(tokens);
              hasSetCredentials = true;
              resolve();
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
        .listen(port);
    });

  const requestYoutube = async <T>(
    makeRequest: () => Promise<T>
  ): Promise<T> => {
    if (!hasSetCredentials)
      await setCredentialsFromFile().catch(setCredentialsFromOauthFlow);
    try {
      return await makeRequest();
    } catch (err) {
      // TODO: Also rethrow if err is not "invalid_grant" error
      if (!err) throw err;
      await setCredentialsFromOauthFlow();
      return await makeRequest();
    }
  };

  const yt = youtube({
    version: "v3",
    auth: oauth2Client,
  });

  const { data } = await requestYoutube(() =>
    yt.liveBroadcasts.list({
      part: ["snippet"],
      mine: true,
      // type: "all",
      maxResults: 1,
      // order: "date",
    })
  );
  // console.log(data.items);
  const item = data.items?.[0];
  if (!item) throw new Error("No live streams found");
  if (item.snippet?.actualEndTime)
    throw new Error("Most recent live stream has already ended");
  const broadcastUrl = `https://youtu.be/${item.id}`;
  const streamAnalyticsUrl = `https://studio.youtube.com/video/${item.id}/livestreaming`;
  console.log({ broadcastUrl, streamAnalyticsUrl });

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
      res.statusText
    );

  open(streamAnalyticsUrl);
};
