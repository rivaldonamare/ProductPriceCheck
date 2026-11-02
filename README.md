# ElectricalStoreAI — product price check

Local Blazor Server app for an electrical store: take a photo of a product with the camera, find the closest matching products from the catalog by image similarity, and confirm the price. Everything runs locally on CPU — no cloud services.

## How it works

- **Catalog**: products (SKU, name, brand, category, price, sale unit) stored in SQLite.
- **Reference photos**: each product gets one or more photos. On upload, each photo is turned into a 512-dimension embedding by a CLIP ViT-B/32 ONNX model.
- **Scanning**: the camera frame is embedded the same way and compared with all indexed photos (cosine similarity). The top 5 products are shown as candidates; the cashier confirms the right one. Nothing is selected automatically.
- **Prices** are always read from the database at confirmation time, never from the scan result.

Architecture (Clean Architecture):

| Project | Responsibility |
|---|---|
| `src/ElectricalStoreAI.Domain` | Entities, validation, similarity math. No external dependencies. |
| `src/ElectricalStoreAI.Application` | Use cases, DTOs, interfaces (`IImageEmbeddingService`, `IProductRecognitionService`, `IImageStorageService`). |
| `src/ElectricalStoreAI.Infrastructure` | EF Core + SQLite, ONNX Runtime inference, ImageSharp preprocessing, file storage. |
| `src/ElectricalStoreAI.Web` | Blazor Server UI (InteractiveServer), camera JS interop. |
| `tests/*` | xUnit unit and integration tests. |

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (`dotnet --version` ≥ 10.0)
- A browser with camera access (Chrome / Edge). Camera access requires `localhost` or HTTPS.
- Python 3.10+ — only needed once, to export the model (see below).

## 1. Get the model

The model file (~335 MB) is **not** in the repository because GitHub rejects files over 100 MB. Export it once:

```powershell
python -m venv .venv
.venv\Scripts\activate          # Linux/macOS: source .venv/bin/activate
pip install torch transformers onnx
python tools/export_clip_onnx.py
```

This writes `Models/clip-vit-b32.onnx` (input `pixel_values` `[batch,3,224,224]`, output `image_embeds` `[batch,512]`).

Notes:

- The app finds `Models/clip-vit-b32.onnx` automatically from the repo root. To use another location, set `Recognition:ModelPath` to an absolute path in `appsettings.json` or an environment variable (`Recognition__ModelPath`).
- A freshly exported file may differ byte-for-byte from another export (different torch versions), which changes the embeddings slightly. After replacing the model, open **Settings / About → Rebuild embeddings**.
- Model source: [`openai/clip-vit-base-patch32`](https://huggingface.co/openai/clip-vit-base-patch32). Check the model's license terms before commercial use.

## 2. Build, test, run

From the repository root:

```powershell
dotnet build ElectricalStoreAI.slnx
dotnet test ElectricalStoreAI.slnx
dotnet run --project src/ElectricalStoreAI.Web --launch-profile http
```

Open http://localhost:5196 (or `--launch-profile https` → https://localhost:7290).

The database is created and migrated automatically on startup. The model loads in the background at startup (a few seconds); the Scanner page shows its status.

## 3. Use it

1. **Products → Add product**: fill in the details and save. The form stays open in edit mode.
2. **Add photos**: select one or more photos (max 10 per batch, 10 MB each). Each photo is indexed immediately; thumbnails show *Indexed* / *Not indexed*. Use several angles per product for better matching.
3. **Scanner → Start camera → Capture & scan**: pick the right candidate, set the quantity, confirm. If the match is ambiguous, the page warns *Product not confidently identified* — use manual search instead.

## Data location and backup

| Data | Default location |
|---|---|
| SQLite database | `%APPDATA%\ElectricalStoreAI\ElectricalStoreAI.db` |
| Reference photos | `%APPDATA%\ElectricalStoreAI\Images\` |

The database stores photo metadata and embeddings; the photo files themselves live in the `Images` folder. **Back up the whole `%APPDATA%\ElectricalStoreAI` folder**, not only the `.db` file — otherwise thumbnails break. Stop the app before copying.

Override locations with configuration:

| Setting | Purpose |
|---|---|
| `DataDirectory` | Root data folder |
| `ConnectionStrings:DefaultConnection` | SQLite connection string |
| `ImageStorage:RootDirectory` | Photo folder (relative paths resolve under `DataDirectory`) |
| `Recognition:ModelPath` | ONNX model file |
| `Recognition:MinTopScore` / `MinMarginToSecond` | Thresholds for the "not confident" warning (default 0.80 / 0.02, not yet calibrated) |

## Known limitations

- Confidence thresholds are initial guesses; tune them with real store photos.
- Matching scans all embeddings in memory — fine for thousands of photos, not tuned for very large catalogs.
- No authentication; intended for a single trusted local machine.
- SixLabors.ImageSharp 4.x requires a commercial license for some uses (the build shows a license warning). Review before production use.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Scanner says model missing | Export the model (step 1) or set `Recognition:ModelPath`. |
| Photos show *Not indexed* | Model was unavailable at upload. Go to **Settings / About → Rebuild embeddings**. |
| Camera permission denied / not found | Allow camera in browser site settings; close other apps using the camera; use `localhost` or HTTPS. |
| Build fails copying files | The app is still running — stop it first. |
