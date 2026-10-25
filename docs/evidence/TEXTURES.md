# Procedural surface textures — evidence

Branch: `feat/textures` (from `main`).
All textures are generated in code at startup (`game/ProceduralTextures.cs`); the branch adds
no binary image assets.

## What changed

**Stage (`game/StudioArenaNode.cs`)**

- Floor is now a textured `StandardMaterial3D`: concrete albedo, aggregate normal map and
  roughness variation.
- The 1m reference grid is baked into the floor albedo (plus a stronger line every 4m) instead
  of being drawn as separate `ImmediateMesh` lines. That removes the thin-line geometry that
  shimmered and z-fought against the floor plane, and lets the grid filter and mip like real
  floor marking.
- Backdrop wall is a plaster-textured unshaded material with a baked grounding fade that
  darkens toward the floor line, breaking the hard plane-to-plane horizon.

**Characters (`game/FirstFighterModelBuilder.cs`)**

- `AddFlatTriangle` now emits box-projected UVs measured in metres, so texture density is
  uniform across parts of very different size.
- One tangent per triangle is authored analytically (flat shading means one face tangent is
  correct). `SurfaceTool.GenerateTangents()` is *not* used: it requires an indexed surface and
  threw at runtime on these deliberately unindexed, flat-shaded meshes.
- `CreateFlatFacetMaterial` takes a `SurfaceKind` (fabric / trouser / skin / hair / leather /
  rubber) and applies a detail albedo plus normal map per body part.

**Colour fidelity**

Detail textures are albedo *multipliers* centred on a known mean, and each material divides its
`AlbedoColor` by that same mean. This keeps the authored palette exact — the earlier approach
(clip-to-white after mean normalisation) silently flattened the floor and darkened every garment.

## Measurements

Same fixed camera, frame 60 of a 90-frame Movie Maker run at 1920x1080. `HF` is the local
high-pass standard deviation (surface detail); `mean` is the region's mean luminance.

| Region      | HF before | HF after | mean before | mean after |
| ----------- | --------- | -------- | ----------- | ---------- |
| floor       | 28.53     | 28.48    | 107.7       | 106.2 (−1.4%) |
| wall        | 18.86     | 19.29    | 124.0       | 125.9 (+1.6%) |
| P1 shirt    | 26.12     | 27.08    | 145.7       | 146.5 (+0.5%) |
| P1 trousers | 17.42     | 18.03    | 94.4        | 96.2 (+1.9%) |
| P2 shirt    | 22.85     | 22.72    | 105.6       | 105.4 (−0.1%) |

High-pass is a poor summary for the stage: the floor's change is a large-scale tonal field
(scanline standard deviation went from 0.56 to 7.07 in an empty floor band), not high-frequency
detail. Palette values stay within ~2% of the untextured build.

## Honest limits

- Character texture is deliberately subtle. The reference sheet is a flat, faceted, *untextured*
  render, and every attempt to push cloth albedo harder read as sandpaper/foil at gameplay
  distance. Cloth now carries its read in a coherent diagonal twill normal map at low strength.
  The visible polish is on the stage; the character change is a surface-quality change, not a
  costume change.
- Headless tests cover the simulation only (all 8 pass, including the sim-purity guard). Nothing
  here is verified by those tests; the verification is the captures below.
- Captures show `F1 3D Boxes: ON` because that is the project's default debug state. The boxes are
  unchanged by this branch, so before/after comparison is under identical conditions.

## Reproduction

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build FirstFighter.sln
dotnet test -m:1 FirstFighter.sln

# still frame (frame 60 of 90)
~/Applications/Godot_mono.app/Contents/MacOS/Godot --path . --resolution 1920x1080 \
  --write-movie captures/textured.avi --fixed-fps 60 --quit-after 90
ffmpeg -y -i captures/textured.avi -vf "select=eq(n\,60)" -vframes 1 docs/evidence/textured_f060.png

# motion
~/Applications/Godot_mono.app/Contents/MacOS/Godot --path . --resolution 1280x720 \
  --write-movie captures/motion.avi --fixed-fps 60 --quit-after 300
ffmpeg -y -i captures/motion.avi -c:v libx264 -pix_fmt yuv420p -crf 20 \
  -movflags +faststart -c:a aac -b:a 128k docs/evidence/textures_motion_1280x720.mp4
```

## Files

- `baseline_f060.png` — pre-change render, same camera and frame
- `textured_f060.png` — post-change render
- `textures_before_after.png` — both stacked
- `textures_motion_1280x720.mp4` — 5s motion capture
