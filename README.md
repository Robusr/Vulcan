# Vulcan: SolidWorks AI Agent
<p align="center">
  <img src="https://img.shields.io/badge/python-3.8%20%7C%203.9%20%7C%203.10%20%7C%203.11-blue" alt="Python Version">
  <img src="https://img.shields.io/badge/client-Windows-lightgrey" alt="Client Platform">
  <img src="https://img.shields.io/badge/server-cross--platform-brightgreen" alt="Server Platform">
  <img src="https://img.shields.io/badge/SolidWorks-2020%20%7C%202021%20%7C%202022%20%7C%202023%20%7C%202024%20%7C%202025-blue" alt="SolidWorks Version">
  <a href="https://www.gnu.org/licenses/gpl-3.0" target="_blank">
  <img src="https://img.shields.io/badge/License-GPLv3-blue.svg" alt="License: GPL v3">
</a>
</p>

<p align="center">
  <b>AI-powered SolidWorks automation tool that turns natural language into 3D models with one click</b>
</p>

---

## Table of Contents
- [Overview](#overview)
- [Key Features](#key-features)
- [System Architecture](#system-architecture)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
  - [Beta Version (Legacy)](#beta-version-legacy)
  - [Rebuilt Version (Latest)](#rebuilt-version-latest)
- [Configuration](#configuration)
  - [Server Environment Variables](#server-environment-variables)
  - [Client Environment Variables](#client-environment-variables)
- [Quick Start](#quick-start)
  - [Run Beta Version](#run-beta-version)
  - [Run Rebuilt Version](#run-rebuilt-version)
- [Project Structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

---

## Overview
Vulcan is a client-server AI assistant for SolidWorks, designed to bridge natural language commands and SolidWorks' COM API. It enables engineers and designers to generate 3D models directly from text prompts, eliminating repetitive manual operations and accelerating the design workflow.

The project maintains two development tracks:
- **Beta Version**: Original Python-based client/server (stable, legacy)
- **Rebuilt Version**: Refactored architecture with C# client (Vulcan.SolidWorksClient) and optimized Python server (latest, actively developed)

The server hosts LLM logic for code generation, while the lightweight Windows client connects to local SolidWorks instances to execute generated code.

---

## Key Features
- 🤖 **Natural Language Modeling**: Generate complete 3D features with plain text prompts (no coding required)
- 🔌 **Client-Server Decoupling**: Server can be deployed locally or on remote cloud instances (Linux/macOS/Windows)
- 🎨 **Dual Client Support**:
  - Legacy PyQt5 UI (beta) with dark theme & always-on-top mode
  - Refactored C# client (rebuild) with native SolidWorks integration
- 🛠️ **Comprehensive Modeling Toolset**:
  - Sketch: Rectangles, Circles, Lines, Arcs (Front/Top/Right reference planes)
  - Features: Boss Extrude, Cut Extrude
  - Utilities: Fillet, Chamfer (with manual selection assist)
- 🔗 **LLM Compatibility**: Works with OpenAI API and OpenAI-compatible endpoints (DeepSeek, Qwen, Claude, etc.)
- 📝 **Full Transparency**: Real-time execution logs and AI thought process display
- 🔄 **Version Flexibility**: Support for SolidWorks 2020-2025 (tested on 2025)

---

## System Architecture
```mermaid
flowchart LR
    A[User] -->|Text Prompt| B[Client (Python/C#)]
    B -->|API Request| C[FastAPI Server]
    C -->|Prompt Engineering| D[LLM]
    D -->|Generated Code (Python/COM)| C
    C -->|Code Response| B
    B -->|COM API Call| E[Local SolidWorks Instance]
    E -->|3D Model Generation| E
```

---

## Prerequisites
### General
- Python 3.8 ~ 3.11 (optimal compatibility with SolidWorks COM API)
- .NET Framework (for C# client in rebuilt version, compatible with SolidWorks add-in requirements)

### Server Requirements
- Cross-platform support (Windows, Linux, macOS)
- Valid API key for OpenAI (or OpenAI-compatible LLM service)

### Client Requirements
- **Windows OS only** (SolidWorks is Windows-exclusive)
- SolidWorks 2020 ~ 2025 (tested on 2025)
- `pywin32` (for Python client COM interaction)
- Visual Studio (optional, for building C# client)

---

## Installation
### 1. Clone the Repository
```bash
git clone https://github.com/your-username/Vulcan.git
cd Vulcan
```

### Beta Version (Legacy)
Original Python-based implementation (stable, legacy support)

#### Server Setup
```bash
# Navigate to beta server directory
cd beta/server-python-beta

# Create and activate virtual environment
# Windows
python -m venv venv
venv\Scripts\activate

# Linux/macOS
python3 -m venv venv
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt
```

#### Client Setup
```bash
# Open new terminal, navigate to beta client directory
cd beta/client-python-beta

# Create and activate virtual environment
python -m venv venv
venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt
```

### Rebuilt Version (Latest)
Refactored architecture with C# client and optimized Python server (actively developed)

#### Server Setup
```bash
# Navigate to rebuilt server directory
cd rebuild/server

# Create and activate virtual environment
# Windows
python -m venv venv
venv\Scripts\activate

# Linux/macOS
python3 -m venv venv
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt  # Create requirements.txt with updated dependencies
```

#### Client Setup (C#)
1. Open `rebuild/client/Vulcan.SolidWorksClient/Vulcan.SolidWorksClient.csproj` in Visual Studio
2. Restore NuGet packages (Newtonsoft.Json, SolidWorks.Interop)
3. Build the project (Debug/Release configuration for x64 architecture)
4. Register the add-in with SolidWorks (follow SolidWorks add-in installation guidelines)

---

## Configuration
### Server Environment Variables
Create a `.env` file in the server directory (beta: `beta/server-python-beta/`, rebuild: `rebuild/server/`) with the following configuration. **Never commit this file to GitHub**.

```env
# ==============================================================================
# LLM API Configuration (Core)
# ==============================================================================
# OpenAI Official
OPENAI_API_KEY="sk-proj-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
OPENAI_BASE_URL="https://api.openai.com/v1"
MODEL_NAME="gpt-4o"

# Alternative: DeepSeek
# OPENAI_API_KEY="sk-xxxxxxxxxxxxxxxxxxxxxxxx"
# OPENAI_BASE_URL="https://api.deepseek.com/v1"
# MODEL_NAME="deepseek-chat"

# Alternative: Alibaba Qwen
# OPENAI_API_KEY="sk-xxxxxxxxxxxxxxxxxxxxxxxx"
# OPENAI_BASE_URL="https://dashscope.aliyuncs.com/compatible-mode/v1"
# MODEL_NAME="qwen-plus"

# ==============================================================================
# Server Configuration
# ==============================================================================
HOST="0.0.0.0"
PORT="8000"
```

For team collaboration, create a `.env.example` file (safe to commit) with placeholder values:
```env
OPENAI_API_KEY=""
OPENAI_BASE_URL="https://api.openai.com/v1"
MODEL_NAME="gpt-4o"

HOST="0.0.0.0"
PORT="8000"
```

### Client Environment Variables
#### Beta Python Client
Create a `.env` file in `beta/client-python-beta/`:
```env
# Local server (same machine)
AGENT_SERVER_URL="http://127.0.0.1:8000"

# Remote server (cloud deployment)
# AGENT_SERVER_URL="http://<your-server-ip>:8000"
```

#### Rebuilt C# Client
Update the API endpoint in `rebuild/client/Vulcan.SolidWorksClient/Services/ApiClient.cs`:
```csharp
private readonly string _serverUrl = "http://127.0.0.1:8000"; // Modify for remote server
```

---

## Quick Start
### 1. Prepare SolidWorks
- Open SolidWorks on your Windows machine
- Create a new empty **Part** document

### 2. Start the Server
#### Beta Version
```bash
# In beta/server-python-beta directory (venv activated)
python main.py
```

#### Rebuilt Version
```bash
# In rebuild/server directory (venv activated)
python main.py
```

Successful server startup output:
```
INFO:     Uvicorn running on http://0.0.0.0:8000 (Press CTRL+C to quit)
```

### 3. Launch the Client
#### Run Beta Version (Python UI)
```bash
# In beta/client-python-beta directory (venv activated)
python main.py
```

#### Run Rebuilt Version (C# Add-in)
1. Build the C# project in Visual Studio (x64 Release)
2. Load the add-in in SolidWorks:
   - Go to **Tools > Add-ins**
   - Browse and select the built `Vulcan.SolidWorksClient.dll`
   - Enable the add-in

### 4. Generate Your First Model
- Enter a prompt in the input box (beta UI / C# add-in panel), e.g.:
  ```
  Create a 100x100 square on the Front Plane, then extrude it 50mm high
  ```
- Click **🚀 Send & Execute**
- Watch the AI generate and execute code in real time, with the 3D model appearing in SolidWorks

---

## Project Structure
```text
Vulcan/
├── .gitignore                # Ignore .env, venv, cache, build artifacts
├── README.md                 # Project documentation
├── .idea/                    # IDE configuration (JetBrains)
├── .venv/                    # Global virtual environment
├── .vs/                      # Visual Studio configuration
├── beta/                     # Legacy beta implementation
│   ├── client-csharp-beta/   # Early C# client prototype
│   │   └── VulcanAddin/      # C# add-in beta code
│   ├── client-python-beta/   # Python PyQt5 client (legacy)
│   │   ├── remote/           # Server API communication
│   │   ├── sw_agent/         # SolidWorks COM interaction
│   │   └── __pycache__/      # Python compiled cache
│   └── server-python-beta/   # Python FastAPI server (legacy)
│       ├── api/              # API routes (v1)
│       ├── core/             # LLM client & prompt management
│       ├── models/           # Pydantic schemas
│       └── __pycache__/      # Python compiled cache
└── rebuild/                  # Refactored main implementation
    ├── client/               # C# SolidWorks client/add-in
    │   └── Vulcan.SolidWorksClient/
    │       ├── bin/          # Build outputs (Debug/Release/x64)
    │       ├── Core/         # Core client logic
    │       ├── Models/       # Data models
    │       ├── obj/          # Build intermediates
    │       ├── packages/     # NuGet packages
    │       ├── Properties/   # Project properties
    │       ├── ReferenceDLL/ # SolidWorks interop DLLs
    │       ├── Services/     # API & COM services
    │       └── UI/           # Client UI components
    └── server/               # Optimized Python server
        ├── services/         # Core server services
        ├── utils/            # Utility functions
        └── __pycache__/      # Python compiled cache
```

---

## Troubleshooting
### Common Issues & Fixes
1. **SolidWorks Connection Failed**
   - Ensure SolidWorks is running with an open Part document before launching the client
   - Run the client/add-in with Administrator privileges (required for COM API access)
   - Verify Python version is 3.8~3.11 (newer versions have pywin32 compatibility issues)
   - For C# client: Ensure correct SolidWorks Interop DLL versions (matching your SolidWorks release)

2. **"Target computer actively refused the connection"**
   - Confirm the server is running and accessible
   - Validate `AGENT_SERVER_URL` (Python client) or `_serverUrl` (C# client) is correct
   - For remote servers: Ensure port 8000 is open in firewalls/security groups

3. **Sketch Generated but Extrusion Fails**
   - Known compatibility issue with SolidWorks 2025 COM API `FeatureExtrusion2` parameters
   - Manual workaround: Right-click the generated sketch in FeatureManager and extrude manually
   - Fix: Record a SolidWorks macro of extrusion and update parameters in `sw_operations.py` (Python) or COM service (C#)

4. **AI Returns Empty/None Code**
   - Restart the server to apply latest prompt changes
   - Verify API key validity and sufficient balance
   - Use a more powerful model (gpt-4o, deepseek-chat) for better JSON format compliance

5. **C# Add-in Not Loading**
   - Ensure build architecture (x64) matches SolidWorks (64-bit)
   - Check SolidWorks add-in registry entries (run Visual Studio as Administrator)
   - Verify all NuGet packages are restored and referenced correctly

---

## Contributing
Contributions are welcome! Please follow these steps:
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines
- For beta version changes: Target `beta/` directory
- For main development: Target `rebuild/` directory
- Follow PEP8 for Python code, C# Coding Conventions for .NET code
- Include test cases for new features
- Update documentation for any functional changes

---

## License
Distributed under the GPL v3 License. See `LICENSE` file for more information.