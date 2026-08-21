---
name: affinity-designer
description: Direct control, vector design automation, and canvas inspection in Affinity Designer, Photo, and Publisher via the built-in Affinity MCP Server and JavaScript SDK. Use when creating shapes, layouts, grids, colors, typography, artboards, layer transformations, reading document info/artboards/selection via MCP resources, or rendering canvas previews in Affinity.
---

# Affinity Design & Automation Skill

This skill guides direct vector design, layout generation, and canvas manipulation in **Affinity by Canva** (v2/v3) using the native JavaScript SDK, dedicated MCP Resources, and the Affinity MCP Server.

## Architecture

* **MCP Bridge**: Connects Antigravity / Gemini via `stdio` JSON-RPC (MCP 2024-11-05) to Affinity's embedded SSE MCP Server (`http://[::1]:6767/sse`).
* **MCP Resources**: Read-only instant URIs (`affinity://document/info`, `affinity://spread/artboards`, `affinity://selection`) to inspect canvas geometry without running scripts.
* **Execution**: JavaScript runs natively inside Affinity's embedded engine with direct C++ memory handles.

---

## Fast Canvas Probing via MCP Resources

Before generating or modifying graphics, probe the document state non-destructively:

* **`affinity://document/info`**: Dimensions, units, color space, session UUID, spread and artboard count.
* **`affinity://spread/artboards`**: List of all artboards with their bounding boxes (`x, y, width, height`).
* **`affinity://selection`**: Active selected layers and their node properties.

---

## Core SDK Modules & Imports

All SDK modules must be required with a leading slash:

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

## Key Design Patterns & SDK Rules

### 1. Colors & RGBA8 Helper

`RGBA8(r, g, b, alpha)` from `/colours` is a helper function that returns a `Colour` object directly.

```javascript
// Correct:
const fillColour = RGBA8(56, 189, 248, 255);
const solidFill = FillDescriptor.createSolid(fillColour, BlendMode.Normal);

// Plain object with Colour.createRGBA8:
const altColour = Colour.createRGBA8({ r: 56, g: 189, b: 248, alpha: 255 });
```

### 2. Linear and Radial Gradients with Transforms

Gradient coordinates in Affinity are positioned via matrix transforms:

```javascript
// Linear Gradient Transform Helper:
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

// Radial Gradient Transform Helper:
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

// Example Gradient:
const grad = Gradient.create([
    { position: 0.0, colour: RGBA8(255, 255, 255, 255) },
    { position: 1.0, colour: RGBA8(30, 58, 138, 255) }
]);
const fillDesc = FillDescriptor.create(
    GradientFill.create(grad, GradientFillType.Linear),
    false,
    createLinearTransform(0, 0, 400, 400),
    BlendMode.Normal,
    false
);
```

### 3. Adding Native Shapes Atomically

Always batch nodes into `AddChildNodesCommandBuilder` so the entire layout is created in a single undo step:

```javascript
const doc = Document.current;
const acnBuilder = AddChildNodesCommandBuilder.create();

// If targeting a specific Artboard:
// const abNode = doc.spreads.first.artboards[targetIndex].node;
// acnBuilder.setInsertionTarget(abNode);

const star = ShapeStar.create();
star.points = 8;
star.innerRadius = 0.45;

const rect = new Rectangle(100, 100, 200, 200);
const brushFill = FillDescriptor.createSolid(RGBA8(99, 102, 241, 255), BlendMode.Normal);
const lineFill = FillDescriptor.createSolid(RGBA8(255, 255, 255, 255), BlendMode.Normal);
const lineStyle = LineStyleDescriptor.createDefault(3);

const shapeDef = ShapeNodeDefinition.create(star, rect, brushFill, lineFill, lineStyle, null);
acnBuilder.addNode(shapeDef);

const cmd = acnBuilder.createCommand(true, NodeChildType.Main);
doc.executeCommand(cmd);
```

### 4. Custom Polygons, Curves & Paths

Use `CurveBuilder` and `PolyCurve` for custom vector geometry:

```javascript
const cBuilder = CurveBuilder.create();
cBuilder.beginXY(points[0].x, points[0].y);
for (let i = 1; i < points.length; i++) {
    cBuilder.lineToXY(points[i].x, points[i].y);
}
cBuilder.close();

const polyCurve = new PolyCurve();
polyCurve.addCurve(cBuilder.createCurve());

const pcnDef = PolyCurveNodeDefinition.create(
    polyCurve,
    brushFill,
    lineStyle,
    lineFill,
    FillDescriptor.createNone()
);
acnBuilder.addPolyCurveNode(pcnDef);
```

### 5. Visual Verification (`render_spread`)

Call `render_spread` with the document's session UUID to inspect the rendered canvas:

```javascript
const uuid = Document.current.sessionUuid;
// Call tool: render_spread { document_session_uuid: uuid, spread_index: 0 }
```
