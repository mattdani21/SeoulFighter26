# Cheonggyecheon night stage + clean match frame — evidence

Branch: `cursor/cheonggyecheon-night-bd63` (from `main`).
All stage geometry and textures are generated in code at startup; the branch adds no binary art assets.

## What changed

**Stage (`game/SeoulNightStage.cs`, `game/StudioArenaNode.cs`, `game/ProceduralTextures.cs`)**

- Match play uses a Cheonggyecheon night diorama: wet granite plaza (no metre grid), still canal
  band behind the fighters, stone coping / far bank, side pedestrian bridge, cool grey building
  masses with dim window emission, N Seoul Tower needle, and three geometric neon sign boxes
  above head height.
- Warm key light is kept so the oatmeal henley / skin still separate from a cooler indigo ambient
  and fill. Super Art cinema darkens the sky and dims neon.
- F9 turnaround keeps the taupe studio cyclorama and studio lighting.

**Match frame (`game/HudAndFrameStudio.cs`, `game/MainController.cs`)**

- Default HUD is health / structure / meter + fighter names only.
- Collision boxes default off (`F1` still toggles).
- Frame meter, hotkey legend, and telemetry sit behind `F6` training overlay.
- Window title: `FIRST FIGHTER — Cheonggyecheon Night`.

## Reproduction

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build FirstFighter.sln
dotnet test -m:1 FirstFighter.sln

godot --path . --write-movie captures/seoul_night.avi --fixed-fps 60 --quit-after 90
ffmpeg -y -i captures/seoul_night.avi -vf "select=eq(n\,60)" -frames:v 1 -update 1 \
  docs/evidence/seoul_night_f060.png

godot --path . --write-movie captures/seoul_motion.avi --fixed-fps 60 --quit-after 300
ffmpeg -y -i captures/seoul_motion.avi -c:v libx264 -pix_fmt yuv420p -crf 20 \
  -movflags +faststart docs/evidence/seoul_night_motion_1280x720.mp4
```

Movie Maker auto-enqueues the 11-hit golden combo after a short settle so motion frames show the
night stage under a real sequence.

## Files

- `seoul_night_f060.png` — match still (clean HUD, neon, canal band)
- `seoul_night_motion_1280x720.mp4` — ~5s motion capture
