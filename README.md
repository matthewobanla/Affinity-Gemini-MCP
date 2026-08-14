# Affinity MCP Integration for Google Gemini & Antigravity

Control, automate, and pair-program vector designs, artboards, and layouts in **Affinity by Canva** (Affinity Designer, Photo, and Publisher v2/v3) using **Google Gemini** and **Antigravity IDE**.

---

## 🌟 Features

* **Direct Vector Generation**: Create complex vector shapes, parametric curves, Bézier paths, squircle app icons, and polygons.
* **Lighting & Shading**: Generate multi-stop linear and radial gradient fills with custom transform matrices, bevels, and specular highlights.
* **Artboard & Spread Control**: Query artboard geometries, target specific artboards, and manage document layers.
* **Real-Time Visual Feedback**: Built-in `render_spread` and `render_selection` tools allow Gemini to visually inspect the canvas in real time.
* **Atomic Undo**: Every generated design or layout is batched into an atomic transaction for single-step undo.

---

## 📁 Repository Structure

```text
affinity-gemini-mcp/
├── README.md                   # Setup guide and usage documentation
├── mcp_config.json             # Sample MCP server configuration
├── .gitignore                  # Git ignore rules for build artifacts
├── bridge/
│   ├── AffinityMcpBridge.cs    # Zero-dependency C# stdio-to-SSE bridge source
│   └── build.bat               # 1-click build script (csc / dotnet)
└── skills/
    └── affinity-designer/
        └── SKILL.md            # Agent skill with JavaScript SDK rules & patterns
```

---

## 🚀 Quick Start Guide

### Step 1: Enable MCP in Affinity
1. Open **Affinity Designer**, **Photo**, or **Publisher**.
2. Go to **Edit** > **Settings** (or **Preferences**).
3. Select **Model Context Protocol**.
4. **Enable** the Model Context Protocol server (default port `6767`).

---

### Step 2: Build the Bridge
The bridge is a lightweight C# application with zero external dependencies that connects Antigravity's `stdio` JSON-RPC to Affinity's embedded SSE server.

Run the build script in the `bridge/` folder:
```cmd
cd bridge
build.bat
```

*Alternatively, compile manually with `csc` or `dotnet`:*
```cmd
csc /target:exe /out:affinity-mcp-bridge.exe bridge\AffinityMcpBridge.cs
```

This will produce `affinity-mcp-bridge.exe`.

---

### Step 3: Configure Gemini / Antigravity
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

### Step 4: Install the Agent Skill
Copy the `skills/affinity-designer` folder into your Antigravity skills directory:

* **Global Setup** (Available across all projects):
  ```text
  C:\Users\<YourUsername>\.gemini\config\skills\affinity-designer\
  ```
* **Project Workspace Setup** (Committed into a project):
  ```text
  <YourProjectRoot>\.agents\skills\affinity-designer\
  ```

---

## 🧪 Example Prompts to Try

Once configured, simply ask Gemini in Antigravity:

* *"Add an isometric cube with specular highlights in the center of the canvas in Affinity"*
* *"Create 10 different fintech app icons on the second artboard"*
* *"Generate an 8-pointed star badge with a radial gold gradient and white rim stroke"*
* *"Inspect the active spread and render a visual preview"*

---

## 🛠️ Architecture & Protocol Flow

```text
┌──────────────────────────────────────────────┐
│  Google Gemini / Antigravity IDE             │
└──────────────────────┬───────────────────────┘
                       │ stdio (JSON-RPC 2.0)
┌──────────────────────▼───────────────────────┐
│  affinity-mcp-bridge.exe (C# Bridge)         │
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
