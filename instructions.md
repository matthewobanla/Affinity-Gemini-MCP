# Affinity MCP Server & JavaScript SDK Instructions

Comprehensive guide for AI agents (Google Gemini / Antigravity) interacting with **Affinity Designer**, **Affinity Photo**, and **Affinity Publisher** (v2/v3) via the Model Context Protocol (MCP).

---

## 1. System Architecture

```text
┌─────────────────────────┐       stdio (JSON-RPC)       ┌───────────────────────────┐
│  Antigravity IDE Agent  │ ───────────────────────────> │  Affinity MCP Bridge      │
│  (Google Gemini Model)  │ <─────────────────────────── │  (affinity-mcp-bridge.exe)│
└─────────────────────────┘                              └─────────────┬─────────────┘
                                                                       │ SSE / HTTP
                                                                       ▼ (:6767)
                                                         ┌───────────────────────────┐
                                                         │ Affinity by Canva App     │
                                                         │ (Embedded V8 JS Engine)   │
                                                         └───────────────────────────┘
```

- **Transport**: The bridge exposes standard `stdio` JSON-RPC (MCP 2024-11-05) to the agent and relays commands to Affinity's native SSE endpoint (`http://[::1]:6767/sse`).
- **Execution Model**: JavaScript scripts execute synchronously inside Affinity's embedded V8 runtime with direct C++ pointer access to the document model.
- **Safety**: Single-step atomic undo via `AddChildNodesCommandBuilder`.

---

## 2. Dedicated MCP Resources

Agents should probe document state non-destructively using MCP Resources before executing scripts:

| Resource URI | Description | Use Case |
| :--- | :--- | :--- |
| `affinity://document/info` | Canvas size (w/h), units, color format, color space, session UUID, spread count, artboard count. | Check if a document is open, check page dimensions and color space. |
| `affinity://spread/artboards` | List of all artboards on the active spread with indices, names, and bounding boxes (`{x, y, width, height}`). | Target specific artboards for icons, illustrations, or responsive layouts. |
| `affinity://selection` | List of currently selected layers, their node IDs, types, names, and bounds. | Contextual edits on what the user currently has highlighted on canvas. |

---

## 3. Mandatory SDK Imports & Module Conventions

All native Affinity modules **must** be imported using a leading slash (`/`) except `affinity:common`:

```javascript
const { Document } = require('/document');
const { 
    ShapeRectangle, ShapeStar, ShapeCog, ShapeHeart, ShapeCloud, 
    ShapeCrescent, ShapeDoubleStar, ShapeTear, ShapePolygon, ShapeDiamond, 
    ShapeSpiral, ShapePie, ShapeSquareStar, ShapeTrapezoid, ShapeTriangle, 
    ShapeArrow, ShapeCat, ShapeQRCode 
} = require('/shapes');
const { AddChildNodesCommandBuilder, InsertionMode } = require('/commands');
const { ShapeNodeDefinition, PolyCurveNodeDefinition } = require('/nodes');
const { Rectangle, Point, PolyCurve, CurveBuilder, Transform } = require('/geometry');
const { FillDescriptor, GradientFill, GradientFillType, BitmapFill } = require('/fills');
const { LineStyleDescriptor, LineCap, LineJoin, StrokeAlignment } = require('/linestyle');
const { Colour, RGBA8, RGB8, CMYK8, Gradient, SVG11 } = require('/colours');
const { BlendMode, NodeChildType } = require('affinity:common');
```

---

## 4. Coordinate System & Geometry

1. **Origin**: `(0, 0)` is the top-left of the active spread or artboard. Positive X moves right; positive Y moves down.
2. **Units**: Point coordinates are in document units (typically pixels).
3. **Artboards**: When inserting into an artboard, coordinates are relative to the artboard's origin if targeting the artboard node directly:

   ```javascript
   const doc = Document.current;
   const targetArtboard = doc.spreads.first.artboards[0].node;
   acnBuilder.setInsertionTarget(targetArtboard);
   ```

---

## 5. Color, Fill & Gradient Math

### Solid Colors

- `RGBA8(r, g, b, alpha)` takes values `0–255` and directly returns a `Colour` object.
- `FillDescriptor.createSolid(color, BlendMode.Normal)` creates a solid fill.
- `FillDescriptor.createNone()` creates a transparent/empty fill.

```javascript
const primaryColor = RGBA8(59, 130, 246, 255); // #3B82F6
const solidFill = FillDescriptor.createSolid(primaryColor, BlendMode.Normal);
```

### Linear & Radial Gradients (Transform Matrices)

Affinity gradient coordinates are transformed using affine matrices (`Transform.data` array of 6 floats):

```javascript
// Linear Gradient Matrix Transform Helper:
function createLinearTransform(x1, y1, x2, y2) {
    const xf = new Transform();
    const dX = x2 - x1;
    const dY = y2 - y1;
    xf.data[0] = dX;
    xf.data[3] = dY;
    xf.data[1] = -dY;
    xf.data[4] = dX;
    xf.data[2] = x1;
    xf.data[5] = y1;
    return xf;
}

// Radial Gradient Matrix Transform Helper:
function createRadialTransform(cx, cy, radius) {
    const xf = new Transform();
    xf.data[0] = radius;
    xf.data[3] = 0;
    xf.data[1] = 0;
    xf.data[4] = radius;
    xf.data[2] = cx;
    xf.data[5] = cy;
    return xf;
}

// Creating a Multi-Stop Gradient Fill:
const grad = Gradient.create([
    { position: 0.0, colour: RGBA8(99, 102, 241, 255) },  // Indigo
    { position: 1.0, colour: RGBA8(236, 72, 153, 255) }   // Pink
]);
const gradientFill = FillDescriptor.create(
    GradientFill.create(grad, GradientFillType.Linear),
    false,
    createLinearTransform(0, 0, 400, 400),
    BlendMode.Normal,
    false
);
```

---

## 6. Shape & Curve Construction

### A. Parametric Built-in Shapes

Use `Shape<Type>.create()` and configure parameters:

```javascript
const star = ShapeStar.create();
star.points = 6;
star.innerRadius = 0.5;

const rect = new Rectangle(50, 50, 200, 200);
const brushFill = FillDescriptor.createSolid(RGBA8(250, 204, 21, 255), BlendMode.Normal);
const lineFill = FillDescriptor.createSolid(RGBA8(255, 255, 255, 255), BlendMode.Normal);
const lineStyle = LineStyleDescriptor.createDefault(2);

const shapeDef = ShapeNodeDefinition.create(star, rect, brushFill, lineFill, lineStyle, null);
acnBuilder.addNode(shapeDef);
```

### B. Custom Polygons, Curves & Bézier Paths

Use `CurveBuilder` and `PolyCurve` for arbitrary vector graphics:

```javascript
const cb = CurveBuilder.create();
cb.beginXY(100, 50);
cb.cubicToXY(150, 20, 200, 80, 250, 50);
cb.lineToXY(250, 200);
cb.lineToXY(100, 200);
cb.close();

const polyCurve = new PolyCurve();
polyCurve.addCurve(cb.createCurve());

const pcnDef = PolyCurveNodeDefinition.create(
    polyCurve,
    brushFill,
    lineStyle,
    lineFill,
    FillDescriptor.createNone()
);
acnBuilder.addPolyCurveNode(pcnDef);
```

---

## 7. Atomic Execution & Undo Transactions

Never add nodes one by one outside a command builder. Always batch nodes into a single transaction:

```javascript
const doc = Document.current;
if (!doc) throw new Error("No active document.");

const acnBuilder = AddChildNodesCommandBuilder.create();

// 1. Add all shape nodes and polycurves
acnBuilder.addNode(backgroundNode);
acnBuilder.addNode(heroShapeNode);
acnBuilder.addPolyCurveNode(detailCurveNode);

// 2. Commit transaction atomically
const cmd = acnBuilder.createCommand(true, NodeChildType.Main);
doc.executeCommand(cmd);
```

---

## 8. Perception & Verification Loop

Gemini is a multimodal model. Always close the feedback loop after generating artwork:

1. Obtain the document session UUID:

   ```javascript
   const uuid = Document.current.sessionUuid;
   ```

2. Call the MCP tool `render_spread`:

   ```json
   {
     "document_session_uuid": "<uuid>",
     "spread_index": 0
   }
   ```

3. Inspect the resulting preview image to verify color contrast, spatial alignment, and visual hierarchy.
