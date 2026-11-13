import { defineConfig } from "vite";
import solid from "vite-plugin-solid";

// The API address comes from the environment (set by Aspire; by the proxy in a preview).
const api = process.env.API_URL ?? "http://localhost:5080";

export default defineConfig({
  plugins: [solid()],
  server: {
    host: true,
    port: Number(process.env.PORT ?? 5173),
    proxy: { "/api": { target: api, changeOrigin: true } },
  },
});
