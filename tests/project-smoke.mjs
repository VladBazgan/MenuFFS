import { access, readFile } from "node:fs/promises";
import { constants } from "node:fs";
import path from "node:path";
import process from "node:process";

const root = path.resolve(import.meta.dirname, "..");
const requiredFiles = [
    "MenuFFS.csproj",
    "Program.cs",
    "appsettings.json",
    "Services/OllamaCloudClient.cs",
    "Services/OpenAiClient.cs",
    "Services/MenuTranslationService.cs",
    "wwwroot/index.html",
    "wwwroot/styles.css",
    "wwwroot/app.js",
    "wwwroot/manifest.webmanifest"
];

for (const file of requiredFiles) {
    await access(path.join(root, file), constants.R_OK);
}

const config = JSON.parse(await readFile(path.join(root, "appsettings.json"), "utf8"));
const providers = config.Ai?.Providers || {};

for (const providerId of ["ollama1", "ollama2", "openai"]) {
    if (!providers[providerId]) {
        throw new Error(`Furnizor lipsă: ${providerId}`);
    }
    if (providers[providerId].ApiKey !== "") {
        throw new Error(`appsettings.json nu trebuie să conțină cheia pentru ${providerId}`);
    }
    if (!providers[providerId].Models?.length) {
        throw new Error(`Nu există modele configurate pentru ${providerId}`);
    }
}

const program = await readFile(path.join(root, "Program.cs"), "utf8");
const service = await readFile(path.join(root, "Services/MenuTranslationService.cs"), "utf8");
const frontend = await readFile(path.join(root, "wwwroot/app.js"), "utf8");
const html = await readFile(path.join(root, "wwwroot/index.html"), "utf8");

const expectations = [
    [program, 'MapPost("/api/translate"', "endpointul de traducere"],
    [program, 'RequireRateLimiting("translations")', "rate limiting"],
    [service, "form.ProviderId", "selectarea explicită a furnizorului"],
    [service, "form.Model", "selectarea explicită a modelului"],
    [frontend, 'formData.append("providerId"', "furnizorul trimis de interfață"],
    [frontend, 'formData.append("model"', "modelul trimis de interfață"],
    [frontend, 'fetch("/api/translate"', "apelul backendului"]
];

for (const [source, fragment, description] of expectations) {
    if (!source.includes(fragment)) {
        throw new Error(`Lipsește ${description}.`);
    }
}

const htmlIds = [...html.matchAll(/\sid="([^"]+)"/g)].map(match => match[1]);
const duplicateIds = htmlIds.filter((id, index) => htmlIds.indexOf(id) !== index);
if (duplicateIds.length) {
    throw new Error(`ID-uri HTML duplicate: ${[...new Set(duplicateIds)].join(", ")}`);
}

const queriedIds = [...frontend.matchAll(/querySelector\("#([^"]+)"\)/g)].map(match => match[1]);
const missingIds = queriedIds.filter(id => !htmlIds.includes(id));
if (missingIds.length) {
    throw new Error(`Elemente HTML lipsă pentru app.js: ${missingIds.join(", ")}`);
}

console.log(`MenuFFS smoke check: OK (${requiredFiles.length} fișiere esențiale, ${htmlIds.length} elemente UI, 3 furnizori, fără chei incluse).`);
