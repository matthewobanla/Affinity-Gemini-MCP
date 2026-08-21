# Affinity MCP Integration for Google Gemini & Antigravity

[![GitHub Repository](https://img.shields.io/badge/GitHub-Affinity--Gemini--MCP-181717?logo=github)](https://github.com/matthewobanla/Affinity-Gemini-MCP)
[![MCP Server](https://img.shields.io/badge/MCP-Protocol%202024--11--05-blue.svg)](https://modelcontextprotocol.io/)
[![Affinity Support](https://img.shields.io/badge/Serif%20Affinity-Designer%20%7C%20Photo%20%7C%20Publisher-orange.svg)](https://affinity.serif.com/)
[![Powered by Gemini](https://img.shields.io/badge/Powered%20by-Google%20Gemini-8E75B2.svg)](https://deepmind.google/technologies/gemini/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Control, automate, and pair-program vector designs, artboards, and layouts in **Affinity by Canva** (Affinity Designer, Photo, and Publisher v2/v3) using **Google Gemini** and **Antigravity IDE**.

---

## 🌟 Features

- **Dedicated MCP Resources**: Instant, non-destructive document probing via standard URIs (`affinity://document/info`, `affinity://spread/artboards`, `affinity://selection`).
- **Standard MCP Prompts**: Built-in prompt recipes for creating isometric art, generating icon sets, and preparing production exports.
- **Direct Vector Generation**: Create complex vector shapes, parametric curves, Bézier paths, squircle app icons, and polygons.
- **Lighting & Shading**: Generate multi-stop linear and radial gradient fills with custom transform matrices, bevels, and specular highlights.
- **Artboard & Spread Control**: Query artboard geometries, target specific artboards, and manage document layers.
- **Real-Time Visual Feedback**: Built-in `render_spread` and `render_selection` tools allow Gemini to visually inspect the canvas in real time.
- **Atomic Undo**: Every generated design or layout is batched into an atomic transaction for single-step undo.

---

## 📁 Repository Structure

```text
affinity-gemini-mcp/
├── README.md                   # Setup guide and feature documentation
├── instructions.md             # In-depth LLM agent guidelines & SDK handbook
├── mcp_config.json             # Sample MCP server configuration
├── .gitignore                  # Git ignore rules for build artifacts
├── bridge/
│   ├── AffinityMcpBridge.cs    # Zero-dependency C# stdio-to-SSE bridge source
│   ├── build.bat               # 1-click build script (csc / dotnet)
│   └── affinity-mcp-bridge.exe # Compiled standalone bridge executable
└── skills/
    └── affinity-designer/
        └── SKILL.md            # Agent skill with JavaScript SDK rules & patterns
```

---

## 📦 Dedicated MCP Capabilities

### 1. MCP Resources (`resources/list`, `resources/read`)

| URI | Name | Description |
| :--- | :--- | :--- |
| `affinity://document/info` | Document Information | Returns canvas dimensions, units, color format, color space, session UUID, spread and artboard count. |
| `affinity://spread/artboards` | Spread Artboards | Returns list of all artboards in the active spread with indices, names, and bounding boxes. |
| `affinity://selection` | Layer Selection | Returns list of currently selected layers with node IDs, types, names, and bounding boxes. |

### 2. Standard MCP Prompts (`prompts/list`, `prompts/get`)

- **`create-isometric-artwork`**: Generates high-detail 2.5D isometric geometry with directional lighting and 30-degree projection planes.
- **`generate-icon-set`**: Generates a cohesive set of grid-aligned vector app icons on designated artboards.
- **`export-production-assets`**: Audits document geometry, resolution, and artboard alignment for asset export.

---

## 🚀 Quick Start Guide

### Step 1: Clone the Repository

```cmd
git clone https://github.com/matthewobanla/Affinity-Gemini-MCP.git
cd Affinity-Gemini-MCP
```

---

### Step 2: Enable MCP in Affinity

1. Open **Affinity Designer**, **Photo**, or **Publisher**.
2. Go to **Edit** > **Settings** (or **Preferences**).
3. Select **Model Context Protocol**.
4. **Enable** the Model Context Protocol server (default port `6767`).

---

### Step 3: Build the Bridge

The bridge is a lightweight C# application with zero external dependencies that connects Antigravity's `stdio` JSON-RPC to Affinity's embedded SSE server.

Run the build script in the `bridge/` folder:

```cmd
cd bridge
build.bat
```

*Alternatively, compile manually with `csc`:*

```cmd
csc /nologo /r:System.Net.Http.dll /target:exe /out:affinity-mcp-bridge.exe bridge\AffinityMcpBridge.cs
```

This will produce `affinity-mcp-bridge.exe`.

---

### Step 4: Configure Gemini / Antigravity

Add the server entry to your global MCP configuration (`~/.gemini/config/mcp_config.json` on Windows at `C:\Users\<YourUsername>\.gemini\config\mcp_config.json`):

```json
{
  "mcpServers": {
    "affinity": {
      "command": "C:\\path\\to\\affinity-gemini-mcp\\bridge\\affinity-mcp-bridge.exe"
    }
  }
}
```

---

### Step 5: Install the Agent Skill

Copy the `skills/affinity-designer` folder into your Antigravity skills directory:

- **Global Setup** (Available across all projects):

  ```text
  C:\Users\<YourUsername>\.gemini\config\skills\affinity-designer\
  ```

- **Project Workspace Setup** (Committed into a project):

  ```text
  <YourProjectRoot>\.agents\skills\affinity-designer\
  ```

---

## 🧪 Example Prompts to Try

Once configured, simply ask Gemini in Antigravity:

- *"Add an isometric cube with specular highlights in the center of the canvas in Affinity"*
- *"Create 10 different fintech app icons on the second artboard"*
- *"Generate an 8-pointed star badge with a radial gold gradient and white rim stroke"*
- *"Inspect the active spread and render a visual preview"*

---

## 🛠️ Architecture & Protocol Flow

```text
┌──────────────────────────────────────────────┐
│  Google Gemini / Antigravity IDE             │
└──────────────────────┬───────────────────────┘
                       │ stdio (JSON-RPC 2.0)
┌──────────────────────▼───────────────────────┐
│  affinity-mcp-bridge.exe (C# Bridge)         │
│  - MCP 2024-11-05 Tools, Resources & Prompts │
│  - Auto-negotiates protocol 2025-11-25       │
│  - Handles mandatory SDK preamble            │
└──────────────────────┬───────────────────────┘
                       │ HTTP / SSE Stream (:6767)
┌──────────────────────▼───────────────────────┐
│  Affinity Embedded Engine (C++ / V8)         │
│  - Executes native JavaScript SDK calls      │
│  - Renders vector artboards & spreads        │
└──────────────────────────────────────────────┘
```

---

## 📄 License

MIT License. Free for personal and commercial use.
