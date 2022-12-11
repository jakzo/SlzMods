export interface OAuth2Config {
  installed: {
    client_id: string;
    client_secret: string;
  };
}

export const DEFAULT_OAUTH2_CONFIG: OAuth2Config = {
  installed: {
    client_id:
      "315745154162-o12or87v77h7pm1n3p3qq28vct5bdvj5.apps.googleusercontent.com",
    client_secret: "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",
    // project_id: "bionic-kiln-371111",
    // auth_uri: "https://accounts.google.com/o/oauth2/auth",
    // token_uri: "https://oauth2.googleapis.com/token",
    // auth_provider_x509_cert_url: "https://www.googleapis.com/oauth2/v1/certs",
    // redirect_uris: ["http://localhost"],
  },
};
