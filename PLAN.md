# PLAN.md — Local AI Product Recognition for an Electrical Store

## 1. Project Goal

Build a local-first web application that helps a cashier identify electrical-store products using a regular webcam and retrieve the correct product and price from a local catalog.

The application must:
- Run locally without requiring internet access during normal use.
- Target Windows, Linux, and macOS.
- Work on CPU-only, modest hardware; do not require a dedicated GPU.
- Support a catalog of 1,000+ products.
- Scan one physical item at a time using a webcam.
- Show likely product candidates and let the cashier confirm the correct SKU.
- Never let AI invent or estimate a selling price. Prices must come from the local database.
- Favor correctness and safe confirmation over automatic selection when similar products exist.

This is an initial prototype, not a complete production POS. Prove product recognition first; avoid building payment, accounting, or advanced inventory features unless needed by the prototype.

## 2. Product Context and Recognition Challenges

The store sells electrical goods such as:
- LED bulbs and other lamps, often with individual packaging.
- Wall sockets and switches, sometimes sold unpackaged from a larger carton.
- MCBs and other electrical components, where visually similar items may have different ratings.
- Electrical cable sold by meter or roll.
- Electrical tape and similar consumables.

Packaging and sales units vary. A shipping carton may contain many saleable units. The application must distinguish packaging/purchasing units from the product's sale unit, but recognition should identify one item per scan.

Important limitation: visual appearance alone may not reliably distinguish products with different wattage, current rating, size, model, or other technical specifications. When these cannot be read confidently from an image, show candidates and require cashier confirmation. Never silently choose a risky near-match.

## 3. Proposed Technology

Use supported, mutually compatible stable releases at implementation time. Do not upgrade to preview packages.

- Language: C#
- Runtime: .NET 10 LTS, if supported by selected dependencies and target OS; otherwise document the compatible supported LTS alternative.
- Web UI: Blazor Web App hosted by ASP.NET Core.
- Database: SQLite with Entity Framework Core.
- Camera: browser `getUserMedia` / MediaDevices API through a small JavaScript interop module.
- Local image inference: ONNX Runtime with a lightweight pretrained image embedding model that can run on CPU.
- Image preprocessing: use a maintained cross-platform library only where needed; avoid native dependencies unless their cross-OS packaging is verified.
- OCR: optional later phase. Keep an interface seam for OCR, but do not make OCR a blocker for the first working prototype.
- Tests: xUnit (or the repository's established test framework if one exists).

Do not require Docker. Do not use cloud AI, hosted inference APIs, telemetry requiring internet, or external APIs in the normal scan flow. All models and assets must be installed locally.

Before selecting a model/library, verify licensing, CPU performance, model size, input tensor format, preprocessing requirements, and Windows/Linux/macOS support. Document model source, license, and installation path. Do not download a model automatically at runtime.

## 4. Architecture and Project Structure

Prefer a simple Clean Architecture structure without unnecessary abstractions:

```text
src/
  ElectricalStoreAI.Domain/
    Products/
  ElectricalStoreAI.Application/
    Products/
    Recognition/
  ElectricalStoreAI.Infrastructure/
    Persistence/
    ImageStorage/
    Recognition/
  ElectricalStoreAI.Web/
    Components/
    Pages/
    wwwroot/js/
tests/
  ElectricalStoreAI.UnitTests/
  ElectricalStoreAI.IntegrationTests/
```

Responsibilities:
- **Domain:** product/catalog concepts and basic invariants.
- **Application:** use cases for creating/updating products, adding reference images, and finding matching candidates.
- **Infrastructure:** EF Core/SQLite, image file storage, image embedding inference, similarity calculation.
- **Web:** Blazor pages, camera integration, product management and scanner UI.

Use dependency injection. Keep AI inference behind an interface such as `IImageEmbeddingService`; keep matching behind `IProductRecognitionService`. Do not make Domain depend on EF Core, ONNX Runtime, browser APIs, or image libraries.

If an existing repository already has established conventions, inspect and follow them rather than blindly creating this structure.

## 5. Minimum Domain/Data Model

Implement only what the prototype needs.

### Product
- `Id` (GUID or the project's established ID convention)
- `Sku` (internal unique SKU; do not assume the product has a barcode)
- `Name`
- `Brand` (optional)
- `Category` (optional; may be a simple field for MVP)
- `Description` (optional)
- `SalePrice` (decimal; non-negative)
- `SaleUnit` (e.g. `Pcs`, `Meter`, `Roll`, `Pack`)
- `IsActive`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### ProductReferenceImage
- `Id`
- `ProductId`
- `RelativePath` or a safe storage key
- `ContentType`
- `CreatedAtUtc`
- Optional metadata such as image angle/notes

Store image files under the app's local data directory, not as large blobs by default. Store only a safe relative path/storage key in the database. Validate file type and size; generate server-side filenames; prevent path traversal. Do not trust client-provided paths.

### ProductImageEmbedding
- `Id`
- `ProductReferenceImageId`
- `ModelKey` / model version
- Embedding vector data in a documented, versioned representation
- `CreatedAtUtc`

An embedding must be reproducible from its source image and model version. When the model changes, do not compare vectors from incompatible models. Provide a way to rebuild embeddings.

### Optional later entities
Inventory, stock movements, sales, and sale items are out of scope for the initial recognition prototype unless needed to test a minimal cart. If a cart is added, snapshot the unit price on each sale line.

### Data constraints
- SKU unique, with database-enforced uniqueness.
- `SalePrice >= 0`.
- Product must have a name and sale unit.
- Inactive products must not be returned as selectable scan results.
- Product reference images and embeddings must belong to existing products.
- Handle case-insensitive product search consistently across SQLite and other future providers, but SQLite is the MVP provider.

## 6. Core User Stories

### A. Product Catalog
1. User can list/search active products.
2. User can create and edit a product.
3. User can set SKU, name, brand, category, sale price, and sale unit.
4. User can upload or capture multiple reference images for a product.
5. User can delete a reference image and its related embedding safely.
6. User can deactivate a product without deleting historical references.
7. Validation errors are understandable and no raw exception details are shown to the user.

### B. Camera Scanner
1. User opens the scanner page on `localhost`.
2. Browser requests camera permission and displays the live webcam preview.
3. User captures one item at a time.
4. App validates the captured image and sends it to the local backend.
5. App generates an embedding using the local model.
6. App ranks product candidates using cosine similarity or another metric appropriate for the selected model.
7. App displays up to five candidates with product name, SKU, price, sale unit, and a similarity score clearly labelled as a ranking score, not a calibrated probability.
8. User confirms the correct product before adding it to a temporary selection/cart.
9. If no candidate is sufficiently distinct, show “Product not confidently identified” and allow manual catalog search.
10. Do not automatically add a product to a sale based only on an uncalibrated similarity score.

### C. Local-first operation
1. After setup, catalog browsing and scanning work without internet.
2. If model files are missing or invalid, show a useful setup error; never silently call a cloud service.
3. Persist database and images in a platform-appropriate local application-data directory, not in a transient build folder.
4. Document how to back up and restore the local database and image directory.

## 7. Recognition Approach

Use pretrained image embeddings and catalog matching for the first prototype; do not train a custom model initially.

### Registering a product
1. User adds product metadata.
2. User uploads/captures several clear reference photos where practical: front, side, packaging, and readable label.
3. Validate and store images.
4. Generate embeddings locally and store them with model version.
5. If embedding generation fails, report the failure and do not mark the image as indexed.

### Scanning
1. Capture a still image from the webcam.
2. Apply the exact preprocessing expected by the selected model.
3. Generate one query embedding locally.
4. Compare against compatible product reference embeddings.
5. Aggregate image-level scores into product-level candidate scores; document the chosen method (for example, maximum or top-k mean).
6. Return up to five distinct active products ordered by score.
7. Allow the user to confirm or manually search.

### Important accuracy safeguards
- Do not claim a similarity score is a probability.
- Do not automatically select a product until a validation dataset demonstrates that a threshold is safe.
- If two similar products differ in wattage, rating, size, or other critical specification, favor user confirmation.
- Record basic local diagnostic timings (preprocessing, inference, matching) without uploading telemetry.
- Keep a manual product search fallback available at all times.
- Support re-indexing reference images when model version changes.

### Model selection checklist
Choose a small CPU-friendly embedding model that can distinguish fine-grained retail objects as well as possible. Evaluate at least one suitable candidate before committing. A generic classification model may not distinguish two nearly identical MCBs or lamps with different ratings. If the chosen model performs poorly, consider OCR and/or a fine-grained model in a later phase rather than making unsupported accuracy claims.

## 8. Camera and Browser Requirements

- Use browser `getUserMedia` through JavaScript interop.
- `localhost` is expected to be the main development/runtime access path, because browser camera access requires a secure context (localhost is generally treated as secure).
- Handle denied permission, no camera, camera already in use, unsupported browser, and device changes gracefully.
- Include start/stop camera controls and a capture button.
- Do not continuously run inference on every video frame for the MVP; infer only after the user captures an image to keep CPU usage low.
- Release camera tracks when navigating away or stopping the camera.
- Avoid retaining captured scan images unless explicitly needed for debugging and the user has opted in.

## 9. UI Requirements

Keep the UI simple and responsive:
- Navigation: `Products`, `Scanner`, `Settings/About`.
- Product list with search and create/edit actions.
- Product form with image upload/capture and image gallery.
- Scanner page with live preview, capture button, candidate list, confidence/ranking label, and manual search fallback.
- Show clear empty states, loading state, inference duration, and recoverable error messages.
- Display price formatted as Indonesian Rupiah (`id-ID`) while storing monetary values as decimal.
- Never use a candidate's price until the user selects that candidate.
- Do not display internal stack traces or arbitrary exception messages to the user.

## 10. Non-Functional Requirements

- Offline-first: no network dependency for recognition after initial installation.
- CPU-only operation is mandatory for MVP.
- Cross-platform intent: Windows, Linux, macOS.
- Reasonable memory use; do not load multiple copies of the model.
- Reuse one initialized inference session for the lifetime of the application where safe.
- Avoid processing a full-resolution camera image if a smaller input is sufficient.
- Use asynchronous I/O for database/file operations where appropriate.
- Keep uploaded images bounded in size and validate file signatures/content types.
- Use safe file storage and avoid exposing arbitrary local filesystem paths.
- Use structured logging without logging sensitive image data by default.
- Add cancellation support for long-running recognition operations.
- Avoid overengineering and premature microservices.

## 11. Testing and Acceptance Criteria

### Automated tests
- Product validation and unique SKU.
- Product price cannot be negative.
- Product deactivation excludes it from scanner candidates.
- Embeddings with incompatible model versions are not compared.
- Similarity ranking returns expected ordering for controlled test vectors.
- Product-level aggregation produces distinct products, not duplicate image rows.
- Invalid uploads and oversized images are rejected.
- Deleting a reference image removes/invalidates its embedding safely.
- API/service returns safe user-facing errors.

### Manual prototype test
Prepare 50–100 real store products with multiple reference images for representative categories:
- Boxed LED lamps.
- Unboxed sockets/switches.
- MCBs with differing ratings.
- Electrical tape.
- Cable products.

Capture test photos that are different from the reference photos, including varied angles and normal shop lighting. Do not evaluate the system only on the exact images used for indexing.

Measure:
- Top-1 accuracy.
- Top-5 accuracy.
- False confident matches between similar variants.
- Median and p95 scan latency on CPU.
- Model memory use and application startup time.

Initial engineering targets (to validate, not guarantee):
- A scan completes in about 1 second on a reasonable CPU-only development machine.
- Top-5 retrieval is useful on the test catalog.
- No cloud requests are made during scan.
- If the result is ambiguous, the UI offers candidates/manual search rather than silently selecting a product.

Do not claim production-ready accuracy based on a tiny sample. Expand the test catalog toward 1,000+ products after the initial evaluation.

## 12. Deployment and Setup

- Provide a clear README with prerequisites and commands to build/run locally.
- Keep the model file out of source control if licensing/size requires it; document where to place it and verify its checksum/version.
- Do not download models automatically during application startup.
- Store the SQLite database, reference images, and configuration in a documented application-data directory.
- Provide platform-specific publish/run instructions for Windows, Linux, and macOS.
- Test camera access and native AI dependencies on each target OS; explicitly document anything not yet verified.
- Include database migration/initialization instructions.
- Ensure a fresh install can run without internet once all required files are supplied.

## 13. Implementation Order

Implement in small, working increments:

1. Inspect the repository and confirm SDK/runtime availability.
2. Scaffold the solution and projects.
3. Implement Product entity, SQLite DbContext, initialization/migrations, and tests.
4. Implement product CRUD and local image storage.
5. Implement webcam capture using browser JS interop.
6. Evaluate and integrate one lightweight ONNX image embedding model; document provenance/license/preprocessing.
7. Implement embedding persistence and candidate ranking.
8. Build scanner UI with five candidates and manual confirmation/search.
9. Add diagnostics and run the acceptance tests.
10. Document limitations and cross-platform status.

After each step, build and run tests. Do not generate the entire application in one unverified pass.

## 14. Out of Scope for MVP

- Payment processing and fiscal/tax integrations.
- Full inventory/accounting/reporting system.
- Multi-user synchronization or shared network database.
- Cloud AI or cloud database.
- Automatic counting of many items in a single frame.
- Barcode/QR workflow as a required feature.
- Training a custom vision model from scratch.
- Guaranteed recognition of visually indistinguishable variants.
- Fully automatic checkout without cashier confirmation.

## 15. Definition of Done

The MVP is complete when:
- The app starts locally and opens in a browser.
- Product CRUD works with SQLite.
- A product can have multiple local reference images.
- Webcam capture works with permission/error handling.
- The local embedding model produces and persists compatible embeddings.
- Scanning returns up to five distinct product candidates ranked by similarity.
- The cashier can manually confirm or search when recognition is uncertain.
- Prices always come from the database.
- Core automated tests pass.
- Setup, model installation, data backup, and known limitations are documented.
- Offline scanning is verified on the development environment; Windows/Linux/macOS support is marked verified only after actual testing on each OS.
