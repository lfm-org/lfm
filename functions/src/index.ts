import { runStartupMigrations } from "./lib/startup-migrations.js";
await runStartupMigrations();

// Barrel file — imports all function registrations so the v4 runtime discovers them
import "./functions/cors-preflight.js";
import "./functions/health.js";
import "./functions/me.js";
import "./functions/me-delete.js";
import "./functions/me-update.js";
import "./functions/raider-cleanup.js";
import "./functions/battlenet-login.js";
import "./functions/battlenet-callback.js";
import "./functions/battlenet-logout.js";
import "./functions/runs-list.js";
import "./functions/runs-detail.js";
import "./functions/runs-create.js";
import "./functions/runs-update.js";
import "./functions/runs-delete.js";
import "./functions/runs-signup.js";
import "./functions/runs-cancel-signup.js";
import "./functions/raider-character.js";
import "./functions/battlenet-characters.js";
import "./functions/battlenet-character-portraits.js";
import "./functions/battlenet-characters-refresh.js";
import "./functions/wow-update.js";
import "./functions/instances-list.js";
import "./functions/specializations-list.js";
import "./functions/guild.js";
import "./functions/privacy-contact.js";
