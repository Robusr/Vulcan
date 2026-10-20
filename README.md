<p align="center">
  <img src="docs/assets/logo.svg" alt="Vulcan logo" width="96" height="96">
</p>

<h1 align="center">Vulcan</h1>

<p align="center">
  <strong>Natural language in. Parametric SolidWorks geometry out.</strong><br>
  An AI add-in that turns a plain engineering request into editable SolidWorks features.
</p>

<p align="center">
  <a href="https://github.com/Robusr/Vulcan/stargazers"><img src="https://img.shields.io/github/stars/Robusr/Vulcan?style=flat-square&amp;color=f97316" alt="GitHub stars"></a>
  <a href="https://github.com/Robusr/Vulcan/network/members"><img src="https://img.shields.io/github/forks/Robusr/Vulcan?style=flat-square&amp;color=2563eb" alt="GitHub forks"></a>
  <img src="https://img.shields.io/badge/SolidWorks-2025-e63946?style=flat-square" alt="SolidWorks 2025">
  <img src="https://img.shields.io/badge/.NET_Framework-4.8-512BD4?style=flat-square&amp;logo=dotnet&amp;logoColor=white" alt=".NET Framework 4.8">
  <img src="https://img.shields.io/badge/Python-3.9+-3776AB?style=flat-square&amp;logo=python&amp;logoColor=white" alt="Python 3.9+">
  <img src="https://img.shields.io/badge/Flask-3.0-000000?style=flat-square&amp;logo=flask&amp;logoColor=white" alt="Flask 3.0">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPLv3-blue?style=flat-square" alt="GPLv3"></a>
</p>

<p align="center">
  <a href="README.md"><strong>English</strong></a> ·
  <a href="README.zh-CN.md">简体中文</a> ·
  <a href="README.fr.md">Français</a>
</p>

<p align="center">
  <a href="#why-vulcan">Why Vulcan</a> ·
  <a href="#what-it-does">What it does</a> ·
  <a href="#capabilities">Capabilities</a> ·
  <a href="#system-architecture">Architecture</a> ·
  <a href="#the-json-contract">JSON contract</a> ·
  <a href="#quick-start">Quick start</a> ·
  <a href="#roadmap">Roadmap</a>
</p>

<p align="center">
  <img src="docs/assets/banner.svg" alt="Vulcan: natural language in, parametric geometry out" width="100%">
</p>

## Why Vulcan

SolidWorks is precise, powerful, and famously click-heavy. A mounting plate with four holes can take a dozen operations: choose a plane, sketch a rectangle, add dimensions, exit the sketch, extrude, select a face, sketch circles, pattern them, and cut through all.

Vulcan compresses that sequence into one sentence. The name comes from the Roman god of fire and metalworking — the forge where raw material meets deliberate intent. Here the raw material is language, and the intent is a parametric feature tree.

The repository contains two generations of the idea:

- **`beta/`** — the original proof of concept. The LLM generated Python code and the client executed it with `exec()`.
- **`release/`** — the maintained architecture. The LLM returns a typed JSON feature tree and a C# add-in executes it deterministically.

This README focuses on `release/`. The Beta track is kept for reference and for the design story.

## What it does

<p align="center">
  <img src="docs/assets/demo.svg" alt="A Chinese prompt becomes a 200 by 100 by 20 millimetre plate with four through holes" width="100%">
</p>

<p align="center">
  <sub>Illustrative response. The same contract accepts a base extrude followed by an ordered list of cuts.</sub>
</p>

1. **Describe the part.** Write a request in the add-in panel. Multilingual models handle Chinese, English, and French input; the system prompt itself is written in Chinese.
2. **The model fills a schema.** The server prepends a versioned SolidWorks domain prompt and calls any OpenAI-compatible endpoint. The model returns JSON, not code.
3. **The add-in builds it.** The C# client deserializes the tree, draws sketches, and calls the SolidWorks COM API. The result is a native feature tree you can edit.

## Capabilities

| Capability | What it means |
|---|---|
| Natural-language modeling | One request becomes an ordered feature tree with a base and subsequent features. |
| Native output | Editable SolidWorks features, not a mesh, a macro, or a generated script. |
| Structured contract | A JSON envelope with `extrude` and `cut` features; millimetres in, metres at the COM boundary. |
| Shape library | Circle, rectangle, polygon, ellipse, and slot profiles. |
| Hole placement | Explicit `center_x` and `center_y`, `through_all`, and a four-corner heuristic. |
| Provider freedom | OpenAI, DeepSeek, Qwen, Qiniu Cloud, vLLM, Ollama, or any compatible endpoint. |
| Portable server | Runs locally, in Docker, or behind a tunnel; only the client needs Windows. |
| Debuggable pipeline | No `exec()` in the release track; each stage is logged and typed. |
| API-first execution | COM feature calls first, with a keyboard-driven UI fallback when the COM call fails. |

## System architecture

<p align="center">
  <img src="docs/assets/architecture.svg" alt="Vulcan system architecture: Windows client, Python server, LLM provider" width="100%">
</p>

| Layer | Stack | Responsibility |
|---|---|---|
| Client | C# / WPF / .NET Framework 4.8 | `Vulcan AI` ribbon tab, prompt panel, JSON envelope handling, COM execution |
| Transport | HTTP REST / JSON | Single endpoint: `POST /api/v1/generate` |
| Server | Python 3.9+ / Flask 3 | Prompt assembly, upstream LLM call, envelope check, error mapping |
| Model | OpenAI-compatible API | JSON feature tree generation |
| Deployment | Docker, reverse proxy, or bare metal | Server anywhere; client beside SolidWorks |

The add-in registers a `Vulcan AI` ribbon tab for parts, assemblies, and drawings, plus a toolbar and menu command. The button opens a modal WPF panel; it is not a separate CAD application.

<p align="center">
  <img src="docs/assets/panel.svg" alt="Layout mockup of the Vulcan add-in prompt panel" width="76%">
</p>

<p align="center">
  <sub>Layout mockup of the 500 × 300 prompt panel. The shipped UI is Chinese; the panel accepts natural-language requests.</sub>
</p>

## Request lifecycle

<p align="center">
  <img src="docs/assets/pipeline.svg" alt="Six-stage request lifecycle with latency and failure handling" width="100%">
</p>

1. **Request.** The add-in sends `{ "prompt": "..." }` to the Flask server.
2. **Prompt assembly.** The server prepends the versioned SolidWorks domain prompt to the user text.
3. **Model call.** Any OpenAI-compatible endpoint returns a JSON feature tree. The default request uses `temperature 0.05`, `top_p 0.95`, `max_tokens 2048`, and a 30-second timeout.
4. **Recovery.** The server strips markdown code fences, then extracts the first `{` through the last `}` before parsing.
5. **Validation.** Flask checks that the response has a single-feature or multi-feature envelope. The C# client deserializes the envelope into typed model classes, then reads feature parameters from a dictionary and applies modeler defaults.
6. **Execution.** `SwModeler` draws the sketch, selects it, and calls `FeatureExtrusion3` or `FeatureCut4`. If the COM call fails, a keyboard-driven fallback opens the native SolidWorks feature panel.

The model call dominates latency. Everything else is local and usually measured in milliseconds.

## The JSON contract

<p align="center">
  <img src="docs/assets/contract.svg" alt="Annotated JSON feature tree with envelope, feature types, shape parameters, units, and placement" width="100%">
</p>

### Envelope

A response is either a single feature:

```json
{
  "feature_type": "extrude",
  "name": "Cylinder",
  "params": {
    "plane": "前视基准面",
    "shape": "circle",
    "diameter": 50,
    "depth": 100
  }
}
```

or an ordered multi-feature model:

```json
{
  "model_name": "Mounting plate",
  "features": [
    {
      "feature_type": "extrude",
      "name": "Base",
      "params": {
        "plane": "前视基准面",
        "shape": "rectangle",
        "length": 200,
        "width": 100,
        "depth": 20
      }
    },
    {
      "feature_type": "cut",
      "name": "Hole 1",
      "params": {
        "plane": "前视基准面",
        "shape": "circle",
        "diameter": 10,
        "depth": 20,
        "through_all": true,
        "center_x": 90,
        "center_y": 40
      }
    }
  ]
}
```

One of the four holes is shown. The full response repeats the `cut` feature with the other corner coordinates.

### Feature schema

| Field | Type | Required | Notes |
|---|---|---|---|
| `feature_type` | string | yes | `extrude` or `cut` |
| `name` | string | no | Human-readable label used in logs |
| `params.plane` | string | yes | Requested plane; see the implementation note below |
| `params.shape` | string | yes | `circle`, `rectangle`, `polygon`, `ellipse`, or `slot` |
| `params.depth` | number | yes | Blind depth in millimetres; ignored when `through_all` is true |
| `params.through_all` | boolean | no | Cuts only; defaults to `false`; cuts in both directions when true |
| `params.center_x` | number | cuts in multi-hole models | Sketch centre on the first sketch axis, in millimetres |
| `params.center_y` | number | cuts in multi-hole models | Sketch centre on the second sketch axis, in millimetres |

### Shape parameters

| Shape | Required fields | Notes |
|---|---|---|
| `circle` | `diameter` | Millimetres |
| `rectangle` | `length`, `width` | Centred on `center_x`, `center_y` |
| `polygon` | `diameter`, `sides` | `sides` is clamped to 3–100 |
| `ellipse` | `major_axis`, `minor_axis` | Millimetres |
| `slot` | `length`, `width` | Straight slot; width is the end diameter |

### Coordinates and units

- **Front plane:** sketch axes are X and Y; `center_x` maps to X and `center_y` to Y.
- **Top plane:** sketch axes are X and Z; `center_y` maps to Z and must fall inside the base thickness.
- **Right plane:** sketch axes are Y and Z; `center_x` maps to Y and `center_y` to Z.
- **Multi-feature models:** read the base `length`, `width`, and `depth` first, then compute later coordinates. Never place several identical features at `(0, 0)`.
- **Units:** every dimension in the JSON is millimetres. The modeler converts to metres at the COM boundary.

### Implementation note

The release modeler currently normalizes sketches to the front plane. The `plane` field is part of the contract and is used by the prompt, but general plane routing is on the roadmap. For a four-corner layout, the client has a heuristic that places cuts at `±(length / 2 − 10)` and `±(width / 2 − 10)` when the request explicitly asks for corner holes in Chinese.

## Design decisions

<p align="center">
  <img src="docs/assets/approach.svg" alt="Beta code-generation track versus the Rebuild JSON-contract track" width="100%">
</p>

### JSON parameters instead of generated code

The Beta track let the LLM emit Python code that the client ran with `exec()`. It was flexible and impressive in a demo, but fragile in practice: a hallucinated API call could crash SolidWorks, error recovery was nearly impossible, and debugging meant reading code nobody wrote.

The Rebuild track treats the LLM as a **schema-driven parameterizer**. The model fills a small, typed template. The C# client owns execution. If the JSON is valid, the feature calls are well-formed. If it is not, the envelope check rejects it before SolidWorks is touched. There is no `exec()` and no code-injection surface in the release client.

### A thin client and a portable server

1. **Keys stay server-side.** Embedding an LLM API key in a client-side macro is not acceptable.
2. **Prompts iterate quickly.** Updating a server prompt takes seconds; shipping a new add-in requires a rebuild and reinstall.
3. **The server can run headless.** That keeps the door open for batch processing, CI checks, and a future web front end.

### A domain prompt, not a generic one

General models fail at CAD not because they lack intelligence, but because they lack context. They do not know the difference between a front plane and a top plane, or that a cut's Z coordinate must fall inside the base thickness.

Vulcan supplies that context through [`system_prompt.py`](release/server/prompts/system_prompt.py): plane-to-axis mappings, feature schemas, shape-specific fields, output discipline, and an anti-overlap rule for multi-feature models.

### API first, UI fallback second

SolidWorks interop is version-sensitive. The modeler calls `FeatureExtrusion3` and `FeatureCut4` directly. If that call fails, the fallback activates `RunCommand` and fills the native feature panel with simulated keystrokes.

The fallback is pragmatic, not elegant. Keep SolidWorks focused while a model is generated, and treat a fallback that runs often as a signal to update the interop parameters.

## Prompt engineering

The system prompt lives in [`release/server/prompts/system_prompt.py`](release/server/prompts/system_prompt.py) and is versioned with `PROMPT_VERSION = "2.0.0"` (about 140 lines). It encodes five things:

1. **Output discipline.** JSON only — no markdown, no prose, no code fences.
2. **Coordinate rules.** Per-plane axis mappings, with a worked example for a 200 × 100 × 20 base and four holes.
3. **Feature schema.** Required and optional fields for `extrude` and `cut`, plus shape-specific parameters.
4. **Anti-overlap constraint.** Multiple identical features must use distinct coordinates; never stack them at the origin.
5. **Plane vocabulary.** The model must emit Chinese plane names such as `前视基准面`, `上视基准面`, and `右视基准面`.

The prompt is deliberately separate from business logic so it can be iterated without touching the server. The model call uses a low temperature because the task rewards consistency, not creativity.

## Quick start

### Prerequisites

| Component | Requirement |
|---|---|
| Server | Python 3.9–3.11 and an OpenAI-compatible API key |
| Client | Windows 10/11, SolidWorks 2025 (tested), .NET Framework 4.8 |
| Build tools | Visual Studio 2022 with the .NET desktop workload |
| Network | The client must reach the server; the server must reach the LLM provider |

### 1. Clone the repository

```bash
git clone https://github.com/Robusr/Vulcan.git
cd Vulcan
```

### 2. Start the server

```bash
cd release/server

python -m venv venv
source venv/bin/activate   # Windows: venv\Scripts\activate

pip install -r requirements.txt
cp .env.example .env
# Edit .env: provider key, base URL, and model name

python app.py
# → http://0.0.0.0:5000
```

The current `app.py` binds port 5000 and enables Flask debug mode explicitly. Change both before exposing the server outside a trusted network.

### 3. Or run it with Docker

```bash
cd release/server
docker build -t vulcan-server .
docker run --rm -p 5000:5000 --env-file .env vulcan-server
```

### 4. Verify the API

```bash
curl -s http://127.0.0.1:5000/api/v1/generate \
  -H "Content-Type: application/json" \
  -d '{"prompt":"前视基准面拉伸200x100x20底座，四角各打一个直径10mm通孔"}'
```

The response is the feature tree itself, not a wrapper. The first request may take a few seconds while the model runs.

### 5. Install or build the client

**Option A — prebuilt installer.** Run `release/client/Vulcan.SolidWorksClient/Vulcan Setup/Output/Vulcan.exe` on Windows. The installer registers the add-in with `RegAsm`. The compiled client uses the endpoint hard-coded in `Services/ApiClient.cs`, so build from source if you want it to use your own server.

**Option B — build from source.**

1. Open `release/client/Vulcan.SolidWorksClient/Vulcan.SolidWorksClient.sln` in Visual Studio 2022.
2. Restore NuGet packages (the client uses `Newtonsoft.Json`).
3. Point the SolidWorks interop references at your installation. The project contains absolute `HintPath` entries under `D:\Program Files\SOLIDWORKS Corp\...`; update them, or use matching assemblies from `ReferenceDLL/`.
4. Set `_baseUrl` in [`ApiClient.cs`](release/client/Vulcan.SolidWorksClient/Services/ApiClient.cs) to your server.
5. Build `Release | x64`. SolidWorks is 64-bit; do not ship an x86 build.
6. Register the add-in from an elevated command prompt in the output folder with `install.bat`, or run `RegAsm.exe Vulcan.SolidWorksClient.dll /codebase`.
7. Restart SolidWorks and open a Part document. The `Vulcan AI` ribbon tab should appear.

### 6. Generate your first model

Open the Vulcan panel and try one of these:

```text
前视基准面拉伸200x100x20底座，四角各打一个直径10mm通孔
```

```text
Create a 200 x 100 x 20 mm base on the front plane, with a 10 mm through hole at each corner.
```

```text
Créez une base de 200 x 100 x 20 mm sur le plan de face, avec un trou traversant de 10 mm à chaque coin.
```

Click **开始生成** (Generate). The add-in builds the features, zooms to fit, and leaves the model as an editable feature tree.

## Configuration

### Server environment

```env
QINIU_API_KEY="sk-xxxxxxxxxxxxxxxx"
QINIU_BASE_URL="https://api.openai.com/v1"
QINIU_MODEL="gpt-4o-mini"

FLASK_ENV="development"
FLASK_RUN_PORT="5000"
```

The `QINIU_*` names are historical: the first deployment used Qiniu Cloud. The client is OpenAI-compatible, so the names only matter to `config.py`.

| Variable | Default | Purpose |
|---|---|---|
| `QINIU_API_KEY` | none | Provider API key |
| `QINIU_BASE_URL` | none | OpenAI-compatible base URL |
| `QINIU_MODEL` | `gpt-oss-120b` | Model name |
| `FLASK_ENV` | none | Read by `Config.DEBUG`; the entry point still sets `debug=True` explicitly |
| `FLASK_RUN_PORT` | `5000` | Read by `config.py`; the current `app.py` binds 5000 directly |

### Provider examples

| Provider | `QINIU_BASE_URL` | Example model |
|---|---|---|
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| DeepSeek | `https://api.deepseek.com/v1` | `deepseek-chat` |
| Qwen (DashScope compatible mode) | `https://dashscope.aliyuncs.com/compatible-mode/v1` | `qwen-plus` |
| Ollama | `http://localhost:11434/v1` | a model served locally |
| vLLM | `http://localhost:8000/v1` | your served model |

Use a model with strong instruction-following and reliable JSON output. Weak models produce the classic failure modes: markdown wrappers, prose before the JSON, or coordinates that overlap.

### Client endpoint

```csharp
private readonly string _baseUrl = "http://127.0.0.1:5000";
```

The shipped source points at a hard-coded host. Change it before building for your own deployment.

### Tuning

- `temperature 0.05`, `top_p 0.95`, `max_tokens 2048` in [`llm_client.py`](release/server/services/llm_client.py) control the model call.
- `LLM_TIMEOUT = 30` in [`config.py`](release/server/config.py) controls the server-side request timeout.
- `ApiClient` uses a 30-second client timeout. If you raise one timeout, raise the other as well.

## API reference

### `POST /api/v1/generate`

Request:

```json
{
  "prompt": "前视基准面拉伸200x100x20底座，四角各打一个直径10mm通孔"
}
```

Success — `200 OK` with the feature tree:

```json
{
  "model_name": "Mounting plate",
  "features": [
    {
      "feature_type": "extrude",
      "name": "Base",
      "params": {
        "plane": "前视基准面",
        "shape": "rectangle",
        "length": 200,
        "width": 100,
        "depth": 20
      }
    }
  ]
}
```

Errors use a small envelope:

```json
{
  "message": "缺少必要字段: prompt",
  "status": "error"
}
```

| Status | Condition |
|---|---|
| `400` | Missing or empty `prompt`, or no valid feature envelope |
| `503` | The LLM provider is unreachable or the request timed out |
| `500` | The LLM response could not be parsed, or an unknown server error occurred |

**Security.** The server has no authentication, authorization, rate limiting, or CORS policy. Do not expose it directly to the public internet. Put it behind a reverse proxy, add authentication and quotas, and disable Flask debug mode first.

## Project structure

```text
Vulcan/
├── README.md                     # English (default)
├── README.zh-CN.md               # 简体中文
├── README.fr.md                  # Français
├── docs/
│   └── assets/                   # Vector figures used by all three READMEs
│
├── beta/                         # Legacy code-generation track
│   ├── client-csharp-beta/       #   Early command-group / ribbon prototype
│   ├── client-python-beta/       #   PyQt5 client with exec() execution
│   └── server-python-beta/       #   FastAPI + AsyncOpenAI server
│
└── release/                      # Active JSON-contract track
    ├── client/
    │   └── Vulcan.SolidWorksClient/
    │       ├── Core/             #   SwAddIn — COM registration and lifecycle
    │       ├── Models/           #   ModelData — request/response types
    │       ├── Services/         #   ApiClient, SwModeler, Logger
    │       ├── UI/               #   MainWindow (WPF, 500 × 300)
    │       ├── ReferenceDLL/     #   SolidWorks interop assemblies
    │       └── Vulcan Setup/     #   Inno Setup installer
    └── server/
        ├── app.py                #   Flask entry point
        ├── config.py             #   Environment configuration
        ├── .env.example          #   Provider and Flask template
        ├── Dockerfile            #   python:3.9-slim image
        ├── prompts/              #   Versioned system prompt
        ├── services/             #   LLM client
        └── utils/                #   Logging and custom exceptions
```

**Repository notes.** The project file compiles `Core/SwAddIn.cs`; the root `VulcanApp.cs` is an older duplicate and is not part of the build. The `services/param_parser.py` module is a legacy validator and is not wired into the release `app.py`; field-level defaults live in `SwModeler`.

## Compatibility and current scope

| Area | Status |
|---|---|
| SolidWorks | Built against the 2025 interop surface (`FeatureExtrusion3`, `FeatureCut4`). Older releases may need parameter updates or the UI fallback; they are not part of the tested matrix. |
| Client platform | Windows only, x64, .NET Framework 4.8. |
| Server platform | Python 3.9–3.11; local, Docker, or headless. |
| Feature types | `extrude` and `cut` only. Revolve, sweep, loft, fillet, and chamfer are not implemented in the release client. |
| Planes | The modeler currently normalizes sketches to the front plane; plane routing is on the roadmap. |
| Validation | The server checks the response envelope; envelope deserialization, parameter conversion, and field defaults live in the C# client. |
| Authentication | None built in. Deploy behind a trusted network or add a proxy with auth and rate limits. |
| LLM quality | Latency and correctness depend on the provider. Use a strong instruction-following model. |

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `Vulcan AI` tab does not appear | The add-in is not registered, or the build is x86 | Build the x64 Release configuration, run `install.bat` or `RegAsm` as administrator, restart SolidWorks |
| "Target machine actively refused connection" | Server is not running, or `_baseUrl` is wrong | Start `python app.py`; check the host and port; open the firewall |
| No active document | SolidWorks has no Part open | Create or open a Part document, then generate again |
| Sketch appears, extrusion fails | Interop version mismatch or a stale SolidWorks state | Check `Desktop/Vulcan_Logs`; update `FeatureExtrusion3` parameters; keep SolidWorks focused |
| LLM returns an empty or malformed response | The model ignored the JSON contract | Switch to a stronger model, lower `temperature`, inspect `vulcan_server.log` |
| Request times out | The model is slower than 30 seconds | Raise `LLM_TIMEOUT` and the `ApiClient` timeout together |
| Chinese or French request is misunderstood | The model lacks multilingual engineering vocabulary | Use a multilingual model; the system prompt is Chinese |
| Build fails on SolidWorks references | `HintPath` points at another installation | Update the references or use the matching DLLs in `ReferenceDLL/` |
| Installer registration fails | SolidWorks is running, or the shell is not elevated | Close SolidWorks and rerun the installer as administrator |
| Model appears but holes are in the wrong place | The plane or coordinate convention does not match the request | Inspect the returned JSON; remember the modeler's front-plane normalization |

Server logs are written to `vulcan_server.log` in the working directory. Client logs are written to `Vulcan_Logs/log_YYYYMMDD.txt` on the desktop.

## Roadmap

- [ ] General plane routing instead of front-plane normalization.
- [ ] Revolve, sweep, loft, fillet, and chamfer features.
- [ ] Native hole patterns, mirroring, and linear/circular arrays.
- [ ] Native English and French system prompts, not only multilingual model behavior.
- [ ] Provider adapters and streaming responses.
- [ ] Authentication, rate limiting, and per-user quotas.
- [ ] Prompt regression tests and a mocked COM layer for CI.
- [ ] A task-pane UI instead of a modal panel.
- [ ] Assembly-level operations and drawing annotations.

## Contributing

Pull requests are welcome. For significant changes, open an issue first so the approach can be discussed.

Good places to help:

- **Features:** add new feature types and shape handlers in `SwModeler`.
- **Providers:** abstract the LLM client further and add provider-specific adapters.
- **Testing:** build prompt regression tests and a COM abstraction that can run in CI.
- **Documentation:** improve the three READMEs or add localized screenshots and examples.

Before opening a pull request, run the server, exercise at least one prompt end to end, and keep `.env`, logs, and build artifacts out of the commit. The project is GPLv3; contributions are accepted under the same license.

## License

Distributed under the **GNU General Public License v3.0**. Derivative works must also be open-sourced under GPLv3. See [LICENSE](LICENSE) for the full text.

---

<p align="center">
  <sub>Built by <a href="https://github.com/Robusr">Robusr</a> — because learning SolidWorks shouldn't require a 200-page manual.</sub>
</p>
