---
name: affinity-vector-style-recreator
description: Analyzes reference images and generates structured vector reproduction instructions or direct script/layer executions for Affinity Designer via MCP.
version: 1.0.0
---

# Affinity Vector Style Recreator Skill

## Objective
Deconstruct a reference visual style into exact vector parameters (geometry, gradients, lighting, layer hierarchy, and color palettes) and execute or output the Affinity Designer construction plan.

## 1. Style DNA Extraction Framework
When analyzing reference artwork, break it down across these 5 dimensions:

1. **Color Strategy & Palette (Hex values):**
   - Background (Solid bold backdrop / Accent contrast).
   - Base primary/secondary fills.
   - Highlights, midtones, and ambient shadow tones.
   - Metallic / Specular stops (e.g., Gold gradient ranges: `#FFE57F`, `#D4AF37`, `#8C6D1F`, `#3B2805`).

2. **Form & Linework:**
   - Silhouette treatment (clean bezier curves, stylized proportions, organic vs. geometric).
   - Stroke styling (borderless shapes, uniform strokes, variable pressure strokes).

3. **Shading & Volume Rendering:**
   - Shading type: Clean planar vector cutouts, soft linear/radial gradients, or inner glow/gaussian blur passes.
   - Light source direction and falloff angle.
   - Specular reflections (e.g., metallic rim lighting, glossy lens reflections).

4. **Layer Hierarchy & Clipping Masks:**
   - Bottom-to-top Z-index structure.
   - Parent shapes acting as clipping masks for internal shadow/highlight vectors.

---

## 2. Affinity Designer Construction Protocol

When creating or orchestrating layers in Affinity Designer via MCP: