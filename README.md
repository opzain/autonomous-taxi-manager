# Autonomous Taxi Manager — Pinnacle 1, Project 15

**Team:** Tirth, Sarthak, Zain, Sahil.
**Co-ordinator:** Dr. Rishika Aggrawal. **Supervisor:** Prof. Dr. Rabinder Henry.
**Course:** B.Tech AI & ML, Semester I.

This is a deterministic city-transport and fleet-dispatch simulation game. It is set in a stylised 3D **Bandra Kurla Complex (BKC), Mumbai** and controlled from an **ESP32 physical dispatch console**.

> **Final report and full documentation (v0.12):** `docs/report/Autonomous_Taxi_Manager_Final_Report.docx` (and `.pdf`), and the deliverable index in the report's Appendix A. **v0.11 — the dispatch game is playable:** the simulation drives the six AT taxis and RapidCab in the 3D city, passenger pins and route ribbons, a dispatch HUD, and the ESP32 console over USB (TFT pages, buzzer cues, auto COM-port search, keyboard fallback). How to play: `docs/16-dispatch-game.md`. v0.10 — realism pass: parked cars, tangled overhead cables, painted kerbs, potholes, window ACs, awnings, glass-tower fins, sun-glow haze, filmic grading and grass. v0.9 — the dispatch simulation core (43 tests), menus, minimap (K), festivals (Y) and monsoon flooding. v0.8 — sound, cinematic mode (C) and pedestrian crossings. v0.7 — street life: traffic signals, irregular car-following traffic with scooters and trucks, walking pedestrians, roadside stalls, BEST bus shelters, billboards, more realistic buildings (L/U/twin/stepped shapes, balconies, Sintex tanks, roof clutter) and the **AT Fleet Hub** depot that the autonomous taxis launch from. Earlier: v0.6 filled streets and realistic trees; v0.5 realistic day + neon night (V), real textures, street camera (G); v0.4 Neon Mumbai 2077 graphics. Full plan and progress log: `docs/00-HANDOFF-PLAN.md`.

---

## 1. Open the 3D world in Unity (about 10 minutes)

> **New to Unity on Windows?** Follow the full step-by-step guide in `docs/12-windows-setup-guide.md`. It covers installing Unity Hub and the editor, setting up the licence, and creating the project.

**You need:** Unity Hub and **Unity 6 LTS** (6000.0 or newer) on Windows or macOS. This step uses no hardware.

1. **Create a project.** In Unity Hub, choose **New project**, then **Unity 6 LTS**, then the **"Universal 3D"** template. Name it `TaxiManager` and click Create.
2. **Copy in our folder.** Copy `unity/TaxiManager/Assets/_Project/` from this repository into the new project's `Assets/` folder. You can drag it into the Project window or use File Explorer / Finder.
3. **Let it compile.** Wait for Unity to finish compiling. The Console should show no red errors.
4. **Run the setup.** From the top menu, choose **Taxi Manager → Setup BKC 3D World (Ultra)**. It does the following:
   - switches to the Linear colour space and the highest quality level
   - tunes URP for ultra quality: HDR, 4K soft shadows with 4 cascades, MSAA 4x, the depth texture the water needs, and per-pixel lights
   - adds SSAO to the renderer if it is missing
   - creates and opens `Assets/_Project/Scenes/BKC_World.unity`
5. **Press Play.** The whole city is generated in about 1–3 seconds. Press **I** to show the controls.

### Controls (keyboard and mouse fallback for the ESP32 console)

| Key | Action |
|---|---|
| W A S D / arrow keys | Pan the camera (x / z) |
| Mouse wheel, Z / X | Zoom / height (y) |
| Right-drag, Q / E, R / F | Orbit and tilt |
| Middle-drag | Drag the map |
| Tab / Shift+Tab | Fly to the next / previous landmark |
| Space, Esc | Follow a fleet taxi, stop following |
| C, J, H | Cinematic mode (auto director, letterbox), next shot, home view |
| N | Sound on / off |
| K · Y · Esc | Minimap (off / small / large) · festival (Ganpati / Diwali) · pause menu and settings |
| T, [ ] | Toggle time-lapse, change the hour |
| 1 2 3 4 | Dawn, Noon, Sunset, Night |
| M | Weather: Clear → Haze → Monsoon |
| P, Shift+P, Shift+O | Random fastest route, close a road on it (reroute demo), reopen all roads |
| O | Traffic heat overlay (green / amber / red) |
| V | Style: **Auto** (realistic day, neon at night; default) / Neon Mumbai 2077 / Realistic |
| G | Street-level chase camera behind a fleet taxi (G again to exit) |
| B | Tilt-shift "miniature city" depth of field |
| F1 … F5 | Graphics: Low, Medium, High, Ultra, Cinematic (1.5× render scale) |
| L, U, F9 | Labels, hide the UI, 2× screenshot (saved to `persistentDataPath/Screenshots`) |

### Troubleshooting

- **Pink objects.** A shader failed to compile on your GPU. Select the shader in `Assets/_Project/Shaders` and read its error in the Inspector. The game automatically falls back to URP/Lit, so it stays playable meanwhile.
- **Errors mentioning `UnityEngine.Input` or the Input System.** Go to Edit → Project Settings → Player → **Active Input Handling** and choose **Both**.
- **Low FPS on a laptop** (for example a MacBook Air or an office laptop). Press **F3** (High) or **F2** (Medium). The chosen tier is remembered.
- **Water looks flat or has no shoreline foam.** Make sure **Depth Texture** is ticked on the URP asset. The setup menu does this for you.

---

## 1b. Make it photoreal (optional, recommended)

Download a few free CC0 textures and an HDRI sky, then drop them into `Assets/_Project/Resources/TaxiSimTextures/`. Step-by-step instructions are in **`docs/13-textures-guide.md`**.

## 2. Run the simulation-core tests (no Unity needed)

```bash
dotnet test sim-tests
```

These tests use the same C# files Unity uses (`Scripts/Core`). They cover the map, routing (spec T07 / T08), deterministic random numbers and terrain. They need the .NET 8 SDK.

---

## 3. Repository layout

```
unity/TaxiManager/Assets/_Project/
  Scripts/Core/        Pure C# simulation core (no UnityEngine): BKC map, terrain, Dijkstra, PCG32 random numbers
  Scripts/World/       3D world generation: terrain, roads/bridges/flyovers, buildings, landmarks,
                       transit, traffic, time of day, weather, post-FX, camera, overlay
  Scripts/Editor/      One-click setup menu
  Shaders/             9 custom URP shaders (towers with interior mapping, roads, water, sky, ...)
sim-tests/             xUnit tests for the core
docs/                  01 concept · 02 simulation · 03 hardware · 04 software · 05 BOM · 06 balancing · 07 tests · 08 manual · 09 bugs · 10 enclosure · 11–16 world/setup/textures/sound/inventory/game · report/ (final report .md/.docx/.pdf)
firmware/              ESP32 dispatch console firmware v0.4 (game console + self-test), Wokwi + PlatformIO
```

## 4. Verification status (v0.9, checked on 2026-11-05)

Run `tools/verify/run_checks.sh` (Linux/WSL; needs the .NET 8 SDK, python3, curl and glslang-tools) to repeat every check below.

| Check | Result |
|---|---|
| Simulation core unit tests (`dotnet test sim-tests`) | ✅ **43/43 pass** (map, routing, and the whole dispatch simulation incl. save/replay and 10 full shifts) |
| All Unity C# scripts type-checked with the real C# compiler against real UnityEngine reference assemblies, in both input-system modes | ✅ **0 errors, 0 warnings** |
| Editor setup script type-checked against the real UnityEditor assembly | ✅ **0 errors, 0 warnings** |
| All 15 shaders: every pass (ForwardLit / ShadowCaster / DepthOnly / DepthNormals), vertex + fragment, basic + full-feature variants, compiled against Unity 6's real URP shader library | ✅ **148/148 compile** |
| BKC map layout: connectivity, no at-grade road crossings, river bridges | ✅ checked (prototype + unit tests) |
| All 22 procedural city sounds rendered outside Unity: no NaN, silence or clipping | ✅ **22/22** |

**What this can't prove.** URP and Input System classes were checked against hand-written stand-ins (`tools/verify/csharp/PackageStubs.cs`), because Unity's package servers aren't reachable from the build machine. The shaders were compiled with glslang, not Unity's own DXC/FXC compiler. The final word is still the first open in Unity 6, plus a visual check in Play mode.
