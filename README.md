# MenuFFS

MenuFFS este o aplicație web responsive pentru traducerea meniurilor de restaurant. Primește fie o fotografie, fie text introdus manual și returnează un meniu structurat în Markdown.

Caracteristici importante:

- fotografia este trimisă direct modelului multimodal, fără motor OCR și fără etapă OCR separată;
- utilizatorul alege explicit furnizorul și modelul înaintea fiecărei traduceri;
- sunt configurate separat `Ollama Cloud 1`, `Ollama Cloud 2` și `OpenAI API`;
- nu există fallback automat: dacă modelul selectat eșuează, MenuFFS afișează eroarea acelui model;
- cheile API există doar în backend;
- imaginile sunt ținute doar în memorie pe durata cererii și nu sunt scrise pe disc;
- rezultatul poate fi previzualizat, copiat sau descărcat ca `.md`;
- interfața poate fi instalată ca PWA.

## Tehnologii

- ASP.NET Core 8;
- HTML, CSS și JavaScript fără framework și fără dependențe frontend externe;
- Ollama native Chat API pentru cele două conturi Cloud;
- OpenAI Responses API pentru OpenAI;
- rate limiting și headere de securitate incluse în ASP.NET Core.

## Pornire rapidă

Ai nevoie de .NET 8 SDK.

```powershell
cd MenuFFS
dotnet restore
dotnet run
```

Aplicația pornește implicit la:

```text
http://localhost:5088
```

## Configurarea cheilor API

Nu pune cheile în `appsettings.json`. Pentru dezvoltare, varianta recomandată este .NET User Secrets:

```powershell
dotnet user-secrets set "Ai:Providers:ollama1:ApiKey" "CHEIA_OLLAMA_CLOUD_1"
dotnet user-secrets set "Ai:Providers:ollama2:ApiKey" "CHEIA_OLLAMA_CLOUD_2"
dotnet user-secrets set "Ai:Providers:openai:ApiKey" "CHEIA_OPENAI"
```

Alternativ, poți folosi variabile de mediu în PowerShell:

```powershell
$env:Ai__Providers__ollama1__ApiKey = "CHEIA_OLLAMA_CLOUD_1"
$env:Ai__Providers__ollama2__ApiKey = "CHEIA_OLLAMA_CLOUD_2"
$env:Ai__Providers__openai__ApiKey = "CHEIA_OPENAI"
dotnet run
```

Pe Linux/macOS:

```bash
export Ai__Providers__ollama1__ApiKey="CHEIA_OLLAMA_CLOUD_1"
export Ai__Providers__ollama2__ApiKey="CHEIA_OLLAMA_CLOUD_2"
export Ai__Providers__openai__ApiKey="CHEIA_OPENAI"
dotnet run
```

Cele două configurații Ollama folosesc același endpoint oficial, dar chei independente. Astfel poți alege în interfață contul Cloud 1 sau Cloud 2.

## Configurarea modelelor

Modelele afișate în interfață provin exclusiv din `Ai:Providers` din `appsettings.json`.

Exemplu:

```json
{
  "Id": "gemma4:31b",
  "DisplayName": "Gemma 4 31B",
  "SupportsVision": true
}
```

Pentru fiecare model:

- `Id` trebuie să fie identificatorul exact acceptat de API;
- `DisplayName` este numele afișat în MenuFFS;
- `SupportsVision` trebuie să fie `true` doar dacă modelul acceptă imagini.

Poți adăuga oricâte modele. După modificarea configurației, repornește backendul. Modelele care nu acceptă imagini pot fi utilizate în modul „Text”, dar aplicația le va bloca în modul „Imagine”.

## Fluxul unei traduceri

```text
Frontend MenuFFS
    → furnizorul selectat
    → modelul selectat
    → backend ASP.NET Core
    → exact API-ul selectat
    → Markdown sau eroarea furnizorului
```

Nu există cod care încearcă automat al doilea furnizor sau alt model.

### Imagine

```text
JPEG/PNG/WebP → model multimodal → traducere Markdown
```

Backendul validează semnătura reală a imaginii, o codifică Base64 în memorie și o trimite direct modelului. Nu există Tesseract, OCR API, bibliotecă OCR sau text intermediar extras separat.

### Text manual

```text
Text introdus/lipit → model selectat → traducere Markdown
```

## Endpointuri MenuFFS

| Metodă | Endpoint | Rol |
|---|---|---|
| `GET` | `/api/health` | Verifică dacă backendul rulează |
| `GET` | `/api/config` | Returnează limbile, furnizorii și modelele publice; niciodată cheile |
| `POST` | `/api/translate` | Trimite textul sau imaginea modelului selectat |

`POST /api/translate` folosește `multipart/form-data`:

- `providerId`: `ollama1`, `ollama2` sau `openai`;
- `model`: identificatorul exact al modelului;
- `sourceLanguage`: codul limbii sau `auto`;
- `targetLanguage`: codul limbii țintă;
- `menuText`: necesar în modul text;
- `image`: necesară în modul imagine.

## Limite implicite

- imagine: 15 MB;
- text: 50.000 de caractere;
- rezultat AI: maximum 8.000 de tokenuri;
- timeout: 180 de secunde;
- traduceri: 20 de cereri/minut/IP.

Valorile pot fi schimbate în secțiunea `Ai` din `appsettings.json`.

## Docker

```powershell
docker build -t menuffs .
docker run --rm -p 8080:8080 `
  -e Ai__Providers__ollama1__ApiKey="CHEIA_1" `
  -e Ai__Providers__ollama2__ApiKey="CHEIA_2" `
  -e Ai__Providers__openai__ApiKey="CHEIA_OPENAI" `
  menuffs
```

Apoi deschide `http://localhost:8080`.

## Verificare

```powershell
dotnet build
node --check wwwroot/app.js
node tests/project-smoke.mjs
```

## Securitate la publicare

Rate limiting-ul inclus reduce abuzul accidental, dar dacă publici aplicația pe internet trebuie să o protejezi și cu autentificare sau prin autentificarea existentă din FamilieFaraStres. Altfel, orice vizitator ar putea consuma creditele API prin backend.

MenuFFS nu salvează imaginile sau traducerile. Furnizorul AI selectat primește însă conținutul necesar procesării, conform politicilor acelui furnizor.

## Documentație API folosită

- [Ollama Cloud API](https://docs.ollama.com/cloud)
- [Ollama Vision](https://docs.ollama.com/capabilities/vision)
- [OpenAI Images and Vision](https://developers.openai.com/api/docs/guides/images-vision)
