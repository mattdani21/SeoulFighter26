# FIRST FIGHTER (SeoulFighter26)

A standalone **2.5D Martial-Arts Fighting Game** built from scratch in Godot 4 Mono (.NET 9 C#).

Inspired by Sloclap's *Sifu*, Andrea "Jens" Demetrio's *I wanna make a fighting game!*, and Typhoon's key-pose animation principles.

---

## Architecture Overview

- **`sim/` (FirstFighter.Sim)**:
  - 100% deterministic, 60 Hz simulation.
  - Zero Godot dependencies, zero single/double precision floats.
  - `Fix.cs`: `Q16.16` fixed-point math (`1000 mu = 1.00m`).
  - Strict `FighterStateKind` finite state machine.
  - 3-box collision system (Pushbox, Hurtboxes with crouching duck & extended limb whiff vulnerability, Hitbox).
  - 16-tick virtual button ring buffer with priority motion parsing (`Super [P5] > DP [P4] > QCF/QCB [P3] > Command Normal [P2] > Normal [P1]`) and 2-tick kara-cancel leniency.
  - Damage scaling, combo pushback scaling (anti-infinite), hitstun/gravity decay, and once-per-combo ground bounce.
  - Guard matrix & Sifu forearm deflect parry (`4S` / `214S`).
- **`game/` (Presentation Layer)**:
  - `FirstFighterModelBuilder.cs`: Procedural flat-shaded faceted 3D mesh with unindexed per-triangle normals on a 19-bone `Skeleton3D` (1.85m tall, 7.8 heads) matching `reference_first_fighter.png`.
  - `FighterPoseEngine.cs`: Non-linear keyframe snapping (`Windup -> Active -> Recovery`), 1.22x axial striking limb stretch, smear arcs, and branched Block/Whiff recovery.
  - `StudioArenaNode.cs`: Sloclap warm taupe-grey studio lighting (`#867970`) with contact shadows and dynamic camera.
  - `HudAndFrameStudio.cs`: Sifu-styled Structure/Guard & Meter HUD, 60-tick color-coded Frame Meter (`Cyan` Startup, `Red` Active, `Blue` Recovery, `Yellow` Stun, `Gold` Parry/Invuln), and live Frame Advantage (`+3 ON HIT`, `-4 ON BLOCK`).
- **`tests/` (Verification Suite)**:
  - 8 headless deterministic verification tests verifying netcode determinism, high whiff on crouch, guard matrix, priority parser, anti-infinite pushback scaling, 11-hit golden combo, sim purity, and model bone anatomy.

---

## Complete 12-Move Martial-Arts Kit

| Move ID | Input | Move Name | Stance / Height | Startup / Active / Rec | Key Characteristics |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **5L** | `L` (`J`) | Snap Lead Jab | Stand / High | 4 / 2 / 7 | Chin height (whiffs cleanly over crouch) |
| **2L** | `2+L` (`S+J`) | Low Lead Hook-Check | Crouch / Low | 5 / 2 / 8 | Low oblique shin check |
| **5M** | `M` (`K`) | Side-Thrust Snap Kick | Stand / Mid | 6 / 3 / 12 | Horizontal side-thrust kick |
| **2M** | `2+M` (`S+K`) | Tailored Low Sweep | Crouch / Low | 7 / 3 / 14 | Beats standing guard |
| **5H** | `H` (`L`) | Step-In Rear Cross | Stand / Mid | 8 / 3 / 16 | Advancing power cross |
| **6H** | `6+H` (`D+L`) | Hopping Downward Elbow | Stand / Overhead | 16 / 3 / 14 | Crushes crouching guard |
| **5S** | `S` (`U`) | Rising Knee & Sky Uppercut | Stand / Mid | 7 / 3 / 17 | Launcher, Jump-Cancelable on hit |
| **j.L** | Air `L` | Air Snap Backfist | Air / Overhead | 4 / 3 / 9 | Mid-air chain starter |
| **j.M** | Air `M` | Air Roundhouse Kick | Air / Overhead | 5 / 3 / 11 | Mid-air horizontal roundhouse |
| **j.H** | Air `H` | Air Spinning Hook Kick | Air / Overhead | 6 / 3 / 13 | Aerial hook kick |
| **j.S** | Air `S` | Axe-Heel Meteor Drop | Air / Overhead | 6 / 4 / 16 | Slams down, triggers Ground Bounce |
| **236P** | `236+P` | Pak Mei Chain Flurry | Stand / Mid | 7 / 9 / 16 | 3-hit advancing rekka burst |
| **623H** | `623+H` | Dragon Rising Uppercut | Stand / Mid | 6 / 5 / 22 | Invulnerable startup (t=1..7), launcher |
| **214S** | `214+S` / `4+S` | Forearm Deflect Parry | Stand / Parry | 2 / 8 / 18 | Parries High/Mid/Overhead, auto-launches |
| **236H+S**| `236+H+S` (`O`) | Super: Five-Point Break | Stand / Mid | 6 / 36 / 20 | 100 Meter, 6-hit cinematic finisher |
| **L+M** | `L+M` (`I`) | Clinch Takedown Throw | Stand / Throw | 5 / 2 / 15 | Unblockable command throw |

---

## Controls & Studio Hotkeys

- **Movement**: `W` / `A` / `S` / `D` or Arrow Keys
- **Attacks**:
  - `J`: Light (`L`)
  - `K`: Medium (`M`)
  - `L`: Heavy (`H`)
  - `U`: Special (`S`)
  - `I`: Throw (`L+M`)
  - `O`: Super Art (`236H+S`)
- **Studio Tools**:
  - `F1`: Toggle 3D Collision boxes (Pushbox: Green, Hurtbox: Blue, Hitbox: Red)
  - `F2`: Pause / Step 1 tick forward (`Space` resumes)
  - `F3`: Cycle dummy mode (`Stand`, `Crouch`, `AutoBlock`, `CounterJab`, `AiSparring`)
  - `F4`: Play the **11-Hit Golden Combo** showcase live
  - `F9`: **Turnaround & 12-Move Inspector Mode** (`Up`/`Down` selects move, `Left`/`Right` scrubs ticks)
  - `R`: Reset match state & meter

---

## Running Headless Tests

```bash
dotnet test -m:1 FirstFighter.sln
```
