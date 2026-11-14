import { defineConfig } from "@playwright/test";

// BASE_URL is the web app's address (printed by `aspire run`, or a preview's URL).
export default defineConfig({
  testDir: "./e2e",
  use: { baseURL: process.env.BASE_URL, screenshot: "only-on-failure" },
  reporter: [["list"]],
});
