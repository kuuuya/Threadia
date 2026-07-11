/// <reference types="vitest/config" />
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

// /api と /hubs は開発時にバックエンド(launchSettings.json の 5100 番)へプロキシする。
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/api": "http://localhost:5100",
      "/hubs": { target: "http://localhost:5100", ws: true },
    },
  },
  test: {
    environment: "jsdom",
  },
});
