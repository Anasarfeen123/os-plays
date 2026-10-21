# OS Plays (project GRIDLOCK) — OS concepts in VR: Deadlock, Banker's Algorithm, CPU Scheduling, Memory Management
## Master Implementation Specification

> Build strictly against this spec. New mechanics get added here first, not improvised in Unity.

Source requirement (ARVR Project Report, Module 4, owner: Anas Arfeen): a walkable 3D
resource-allocation graph, circular-wait detection with highlighting, and a Banker's Algorithm
puzzle mode with immediate safe-sequence feedback. The parking-lot setting is **our chosen
implementation concept** for those requirements. The report does not mention it.

---

## 1. Concept mapping

| Parking simulation | Operating Systems |
|---|---|
| Car | Process |
| Parking space / lane | Resource |
| Car occupying a space | Allocation |
| Car requesting an occupied space | Request |
| Car unable to proceed | Waiting process |
| Holding one space while waiting for another | Hold-and-wait |
| Circular dependency between cars | Circular wait |
| Cycle in wait-for graph | Deadlock |
| Parking controller | Allocation / avoidance decision maker |
| Safe / unsafe allocation | Safe / unsafe state |

## 2. Modes

**Mode A — Deadlock Detection.** Cars acquire and request spaces. When a cycle forms in the
wait-for graph, show `DEADLOCK DETECTED`, turn the involved cars and resources red, and offer **Analyze**.

**Mode B — Banker's Algorithm.** The player is the Parking Resource Manager. A request arrives
(`Car B requests P3`) and the player chooses GRANT or DENY. The game runs the **real**
request and safety algorithms (it never looks up a pre-labelled answer), then shows the safe sequence or explains why the state is unsafe.

## 3. Environment

Small and stylized. It has parking spaces (P1..P6), a few lanes, an entry, an exit, 4+ cars and resource
indicators in Analyze mode. No photorealism.

## 4. Scale

The main demo uses 4 cars, but **nothing is hard-coded to 4**. It must support 2 (A↔B), 3 (A→B→C→A),
4 (A→B→C→D→A) and 5+.

## 5. Resource model (v1)

- Resources are parking spaces (lanes, loading zones and exit access come later).
- A resource has `Owner = Car | None`. Detection mode uses single-instance resources.
- A car has `Held resources` and an optional `Requested resource`.
- Banker mode uses resource **types** with counts (e.g. "Zone A spaces: 3").

## 6. Deadlock detection

Maintain Allocation, Request, Ownership and Waiting. Wait-for edge `X → Y` means X requests a resource
held by Y. A cycle means deadlock. The detector works for N cars / M resources.

## 7. Analyze mode

Overlay on the lot. Each car gets a label (`CAR A / Owns: P1 / Wants: P2`), wait-for arrows (`A ──► B`)
and highlighted resources, plus a compact info panel. No graph editor.

## 8. Deadlock alert

Involved cars and resources turn red. The UI shows `DEADLOCK DETECTED` and
`Circular wait detected between N processes`, followed by an **ANALYZE** button.

## 9–10. Banker's Algorithm

Matrices: `Available`, `Max`, `Allocation`, `Need = Max − Allocation`.

Request algorithm for process i with request vector R:
1. If R > Need[i], it is an error (the car exceeded its declared maximum).
2. If R > Available, the car must wait.
3. Tentatively allocate, then run the safety algorithm. If the state is safe, grant. Otherwise roll back and deny.

Feedback:
- Correct GRANT: "Request granted. System remains safe. Safe sequence: B → C → A → D"
- Wrong GRANT: "Unsafe allocation. Safe sequence no longer exists."
- Correct DENY / wrong DENY are explained in the same way.

## 11. Levels

1. **Resources** — allocation basics, no deadlock.
2. **Waiting** — requesting an occupied space.
3. **Circular Wait** — 3 cars, the player identifies the cycle.
4. **Deadlock Detection** — 4 cars, automatic detection, then Analyze.
5. **Banker Challenge** — GRANT/DENY against the safe state.
6. **Challenge Mode** — randomly generated states and requests, scored.

## 12. Scoring

Deadlocks detected, correct analyses, correct and incorrect Banker decisions, final score.
Time, accuracy and deadlocks prevented are optional.

## 13. VR interaction (XRI / OpenXR)

Point to inspect a car or resource, grab/select to view state, an Analyze button, drag a request onto a resource
(Banker), teleport around the lot.

## 13a. Meta Quest 1 (as built)

Target: Quest 1 standalone (Android 10 / API 29, Snapdragon 835, 72 Hz) through OpenXR. The same APK runs on Quest 2/3.

**Rig.** XRI *XR Origin Hands* (Hands Interaction Demo sample). It switches between Touch controllers and tracked
hands automatically and has a floor-level tracking origin. Jump is removed so A is free.

**Touch controllers**

| Input | Action |
|---|---|
| Trigger or grip on a car/bay | select (trigger was added to XRI *Select*) |
| Trigger on a panel | press the button |
| Right stick forward / left-right | teleport arc / 45° snap turn |
| Left stick | walk |
| A / B / X | Deadlock: ANALYZE / TOW CAR / RESTART (hold 0.8 s). Banker: GRANT / DENY / NEXT. CPU lab: RUN-PAUSE / STEP / SIMULATOR |
| Y or ≡ | toggle the wrist menu above the left controller |

**Hand tracking** (enable *Hand Tracking* in the Quest settings)

| Gesture | Action |
|---|---|
| Point and pinch (index + thumb) at a car/bay/panel | select / press (Meta hand-tracking aim) |
| Pinch on open ground, release | teleport (landing ring while held; `HandTeleport`) |
| Left palm towards the face | wrist menu; poke its buttons with the right index (`WristMenu`) |
| Wrist menu ← TURN / TURN → | 45° snap turn |

A **VR CONTROLS** lectern left of the start point lists these. Both scenes get them from
`GridlockSceneBuilder.BuildPresentation`.

**Performance.** On Android, `QuestPerformance` turns off post-processing, SMAA and HDR on the XR camera and enables
fixed foveation. The Mobile URP asset (Android quality) uses 2× MSAA, no HDR, one 35 m hard-shadow cascade and per-pixel
additional lights without shadows (only the CPU lab and the memory warehouse have any). Rendering is single-pass multiview on Vulkan.

**Build.** **GRIDLOCK ▸ Build Quest APK** (or batchmode `-buildTarget Android -executeMethod
Gridlock.Editor.GridlockQuestSetup.ConfigureAndBuild`). It configures the player (IL2CPP, ARM64, API 29, Vulkan,
Linear, ASTC), XR Management and the OpenXR features (Meta Quest Support, Oculus Touch, Hand Tracking, Meta Hand
Tracking Aim, Foveated Rendering), then writes `Builds/GRIDLOCK-Quest.apk`. `GridlockQuestManifest` adds Meta's
hand-tracking permission and feature to the manifest. Install it with `adb install -r Builds/GRIDLOCK-Quest.apk`
(the headset must be in developer mode). The app appears under *Unknown Sources*.

## 13b. Android phone (as built)

- Same scenes, no VR: **GRIDLOCK ▸ Build Phone APK** (batchmode `-buildTarget Android -executeMethod Gridlock.Editor.GridlockPhoneSetup.ConfigureAndBuild`) writes `Builds/GRIDLOCK-Phone.apk` (`com.anasarfeen.gridlock.mobile`, Android 10+, landscape auto-rotate, Vulkan with GLES3 fallback). The OpenXR loader is removed for that build only; the Quest configuration is restored afterwards.
- Runtime: `QuestPerformance.IsStandaloneHeadset` is true only when an XR loader is configured, so the phone build falls back to the desktop presentation, and `PhonePresentation` swaps in the touch-aware `InputSystemUIInputModule`, runs at 60 fps and keeps the screen awake.
- Touch (`TouchGestures`): in the lot, drag orbits, pinch zooms, two fingers pan and a tap inspects and selects a car or bay. In the hub, a big on-screen keypad (◄ 1 2 3 ► START) presses the desk keys, the desk keys can also be tapped, and a drag looks around. Android back returns to the hub, or quits from it. The help bars switch to touch wording (`TouchVariant`).
- Tests: `PhoneModeTests` (PlayMode) drive it with simulated touches. `GridlockScreenshots.Phone` renders 2400×1080 phone views.

## 14. AR mode

The **same simulation** placed on a detected surface (tabletop) with touch input. Only presentation and
input differ.

## 15. Architecture

```
Scripts/
├── Core      (pure C#)  ParkingState, Car/Resource ids, SimulationManager glue
├── Deadlock  (pure C#)  WaitForGraph, DeadlockDetector   + DeadlockVisualizer (MonoBehaviour)
├── Banker    (pure C#)  BankerState, BankerAlgorithm      + BankerChallenge (MonoBehaviour)
├── UI                   HUD, DeadlockAlert, AnalysisPanel, BankerPanel
└── XR                   XR Origin, grab/interact, teleport, AR placement
```

Rule: **simulation logic has no UnityEngine dependency**. MonoBehaviours only present it and feed input to it.
The logic is covered by EditMode tests.

## 16. Build order (do not skip ahead)

```
SIMULATION → DEADLOCK → BANKER → VR → POLISH
```

Not yet: city assets, realistic cars, fancy graph UI, complex animation, multiplayer, AR, polished VR, elaborate UI.

> **Decision 2026-10-05:** the project owner brought **VR rig and visual/audio polish forward** ahead of the Banker
> game mode. The Banker game (§9–11) was then built the same day as its own scene.

## 16c. Banker mode as built (GridlockBanker.unity)

- **Resource types:** Zone N (P1–P3) and Zone S (P4–P6), with 3 bays each. **Processes:** fleets A–D.
- **The matrices are physical:** parked cars = Allocation, cars on the street = remaining Need, cars at the gate = the
  Request, empty bays = Available.
- **Deciding:** GRANT or DENY on the board, the desktop panel, the VR podium, or the card floating over the gate cars.
  The real request and safety algorithms judge the answer. GRANT is correct only when the outcome is Safe; DENY is
  correct for Unsafe, Must wait and Exceeds claim.
- **Feedback:** a verdict, a plain-language explanation, and the safety check step by step. On the lot, fleets light
  up in safe-sequence order and stuck fleets turn red. A correct safe grant drives the gate cars into their bays.
- **Levels:** five guided levels that tell one story (safe, over claim, must wait, unsafe, safe with the last bay),
  then an endless random Challenge mode. Random lots always start safe, and the outcomes are mixed so guessing
  does not pay.
- **Scoring:** +100 for a correct answer, +50 for a correct challenge answer within 10 s, −50 for a wrong one, plus
  a streak counter.
- **Switching:** the Deadlock and Banker scenes switch with a MODE button on every panel.

## 16a. Learner interaction (Deadlock mode)

The report requires learners to *create* allocation and request edges, so the learner does not just watch:

- **Select a car, then a bay**: the car requests that bay. If the bay is free, the car drives over. If it is taken,
  the car waits and honks.
- **Cars move bay to bay**: a car keeps its current bay until it holds the next one, then frees the old one
  (hold-and-wait made physical).
- **TOW CAR** preempts the selected car: it loses its bays and goes back to the street. Waiting cars then move up one
  by one, so the learner sees *deadlock recovery by preemption*.
- Hover or point at a car or bay to inspect it. Analyze toggles the wait-for overlay. Restart replays the demo.
- Taking control of a car stops the scripted demo.

Input: XR uses ray hover and **trigger/grip** (or a hand pinch) to select, plus teleport (see §13a). Desktop uses hover, left-click, right-drag to orbit
and the wheel to zoom.

## 16b. Audio

City ambience, a speed-driven engine per car, a horn when a car is blocked, a chime when a bay is granted, an alarm
when a deadlock forms, and UI clicks. Procedural sounds are generated by `Tools/synth_audio.py`. Credits are in
`Docs/CREDITS.md`.

## 16d. Hub room (GridlockHub.unity, the start scene)

A large sci-fi control room (16.8 × 14.4 m, 4.8 m ceiling) built from the Kenney Space Station kit. The player sits
in a chair at a central desk with a keyboard, a holo projector and three monitors. A 7 m panoramic window looks out
on a ringed gas giant, its moon and passing ships. Around the desk are a **research lab** (left: computer banks,
a planet display with a hologram planet, and a holo table spinning the four-car wait-for cycle), an **engineering bay**
(right: stacked containers, an ore skip, pipes, structure frames and the live wait-for-graph wall screen) and a raised
**gallery** at the back (stairs, rails, a lounge and the server racks). The rig is **seated**: snap turn only, no
walking or teleporting. The desktop gets a fixed chair view with a slight mouse look.

- **Selecting happens on the desk keyboard, not the screen.** The keyboard's right-hand pad has working keys:
  **1 2 3**, **◄ ►** and **ENTER** (`HubKey`). Press them with a fingertip (hand-tracking poke), a controller tip,
  a controller ray + trigger, a mouse click, or the real keyboard on desktop (← → / A D, 1 2 3, Enter / Space;
  `HubDesktopInput`). Keys sink, glow and click. The focused tile lifts and glows; ENTER launches it.
- **Main monitor.** Three scenario tiles: **01 DEADLOCK** (→ GridlockParkingLot) and two locked placeholders,
  **SCENARIO 02 / 03**, which shake and buzz with "ACCESS DENIED". To add a scenario, give its `Tile(...)` in
  `GridlockSceneBuilder.Hub.cs` a scene name and `locked: false`.
- **Boot sequence** (`HubScreen`): CRT power-on, a typed title, a boot log with the keyboard typing along, then the
  tiles slide in one by one. While idle it shows a live clock, telemetry lines and a blinking prompt.
- **Tiles** (`HubTile`): the focused tile lifts and glows, with ► and [ENTER] on its action, and a blip on focus.
- **Launch** (`HubLauncher`): confirm chime and a screen glitch (jitter, tearing, flicker). Then the warp starts:
  streaks and tunnel rings rush out of the screen, the room's neon powers down, the view accelerates through the glass
  and whites out, and the scenario fades in from white with an arrival swell (`ScreenFader`, in every scene).
- **Ambient motion:** breathing neon (`HubPulse`), ceiling halo and floor ring, holo rings and core, blinking server
  LEDs, live resource bars, a CRT scanline, a hovering drone, a spinning planet, moon and starfield, ships flying
  past (`HubFlyby`), and keys lighting up as they "type" (`HubKeyboard`).
- **Audio** (generated by `Tools/synth_audio.py` into `Assets/Audio/Hub/`): reactor hum ambience, boot, hover,
  select, denied, key clicks, glitch, warp and arrive.
- **Back to the hub:** a **◄ ROOM** button on the wrist menu, the VR CONTROLS sign and the desktop HUD.
- Quest budget: no realtime shadows or extra lights in the room, unlit neon, about 600 warp particles at most.
  Review renders come from **GRIDLOCK ▸ Screenshots ▸ Hub Room** (`Docs/Screenshots/hub_*.png`).

### 16d-2. Roaming the control room (as of 2026-10-12)

The hub is no longer seated-only. You start at the desk and can walk the whole room.
- **Desktop:** **R** or the **STAND UP** button. Walk with WASD or the arrow keys, run with Shift, right-drag to look,
  click the floor to walk there. Click the chair, press R, or press SIT DOWN to glide back into the chair, where the
  desk keys work again. Desk keys can also be clicked while standing.
- **Phone:** STAND UP, tap the floor to walk, drag to look, tap the chair to sit.
- **Headset:** walk with the left stick and teleport or turn with the right stick (hand pinch-teleport too). The
  stairs lead to the gallery. Point at the chair, or press **SIT AT DESK** on the wrist menu, to return to the desk.
- **Collision** (`BuildHubCollision`): invisible blockers for the walls, the gallery rails and the kit furniture.
  The floor, the stairs ramp and the gallery deck are walkable and teleportable (upward-facing hits only).
- **Exhibit labels** (`HubPlaque`): only the nearest one shows, fading in as you approach. They cover the deadlock
  hologram, orbital display, courier shuttle, process table, server racks, research lab, gallery lounge and
  observation window.

## 16e. City around the lot (both lot scenes)

`BuildStreetLife` adds traffic on the east street (`AmbientTraffic`, eight Kenney cars that the simulation never uses),
street lamps, traffic lights at the gates, a highway sign and a café terrace. `BuildGreenery` adds Kenney Nature Kit
planting: a pocket park behind the VR start point (trees, a stone path, a flower plaza and a hedge), a hedge-and-flower
bed under the scoreboard, and street trees. Static decor is static-batched for Quest.

## 16f. Guided tours (narrated, about 2 minutes each)

For anyone, including people who have never heard of a deadlock. Each scenario (Deadlock, Banker, CPU lab, memory warehouse) has a tour that plays by itself on the
first visit of a session (Settings → Guided tours AUTO/OFF). The **► GUIDED TOUR** pill or Menu → GUIDED TOUR
replays it. The control room has no narration.

- **Deadlock tour (`GuideTour`, ~100 s).** It drives the real simulation in step with the voice:
  1. cars = processes, bays = resources;
  2. allocation;
  3. hold and wait;
  4. circular wait into deadlock (alarm);
  5. detection (wait-for arrows);
  6. **your turn:** select a red car and press TOW CAR (after ~15 s the guide tows D);
  7. the jam unwinds (recovery by preemption);
  8. pointer to Banker mode.
- **Banker tour (`BankerTour`, ~95 s).** Told on Level 1's lot:
  1. fleets = processes, zones N/S = resource types;
  2. parked cars = Allocation, street cars = Need, gate cars = Request, empty bays = Available;
  3. how the safety check works;
  4. **your turn:** GRANT or DENY (hint after ~18 s, then the guide grants);
  5. the narrator explains why the answer was right or wrong while the fleets light up in safe order;
  6. what the next levels teach.
- **CPU lab tour (`CpuTour`, ~90 s plus the run).**
  1. processes and burst times, the ready queue, the CPU core;
  2. the algorithm desks (the tour picks FCFS);
  3. **your turn:** press RUN (hint after ~15 s, then the guide runs it);
  4. the Gantt chart fills in at triple speed;
  5. waiting time, then: try another desk and compare, and the challenge wall.
- **Memory tour (`MemoryTour`, ~85 s plus the moves).** Replaces the scenario's own welcome voice-over:
  1. pallets = frames, boxes = pages, the reference string on the screen;
  2. the replacement policy (the tour picks FIFO);
  3. **your turn:** load the first page (hint, then the guide loads it);
  4. page faults while the frames fill (the guide makes each move after a pause if nobody does);
  5. the first page in is the first out, and what LRU and Optimal would do instead.
- The scenario tours live with the scenario scripts (Assembly-CSharp) and the builder adds them by type name.
- **Shared machinery (`NarratedTour`):**
  - the floating **guide screen** shows the step counter, title, subtitles that light up word by word, a progress
    bar, NEXT / SKIP, up to two hands-on buttons, and REPLAY / mode switch at the end;
  - a bouncing marker points at what is being explained, and the desktop camera glides to it;
  - in a headset the screen floats beside the view and lazily follows the head;
  - the learner's input is locked until their turn, and music and city sound duck under the voice.
- **Narration source:** `Assets/Audio/Narration/narration.json`, used for the subtitles. `Tools/make_narration.py`
  speaks it with Piper TTS (LJ Speech voice, public domain). An optional `say` field changes only the spoken form.

## 16g. System menu, music and settings (every scene)

- **Opening it:** Esc (and Android back), the **≡ MENU** button on the desktop/phone HUD, the left controller's
  **≡** button, the wrist menu's **≡ MENU** button, or Quest's hand-tracking menu gesture (look at your left palm and
  pinch).
- **While open:** the game pauses (time and sound); the music and the menu's own clicks continue.
- **Entries:** RESUME, GUIDED TOUR, RESTART, ◄ CONTROL ROOM (home), SETTINGS, HOW TO PLAY (per scene) and QUIT.
  Entries a scene does not use are hidden.
- **On desktop:** a dimmed full-screen overlay.
- **In a headset:** an 84 × 50 cm panel 0.75 m away, so it can be poked with a fingertip or pointed at with a ray or
  pinch. It draws on top of the scene, so the hub's monitor cannot hide it.
- **Settings** (saved in PlayerPrefs, applied immediately; `GameSettings`, `SettingsApplier`), on four tabs with RESET ALL:
  - **AUDIO:** master, music, effects and narrator volume (the mixer's Music / Effects / Voice groups; master is the
    listener volume).
  - **DISPLAY:** graphics PERFORMANCE/QUALITY, post effects on/off, sun shadows on/off, subtitles on/off.
  - **CONTROLS:** look speed (0.4×–1.6×), invert look, VR turning SNAP/SMOOTH, snap angle 30/45/90°, and VR moving
    TELEPORT/WALK (a comfort option).
  - **GAMEPLAY:** guided tours AUTO/OFF, car speed SLOW/NORMAL/FAST, SHOW TOURS AGAIN.
- **Music:** three seamless loops composed by `Tools/synth_music.py`, crossfading between scenes (`MusicPlayer`):
  "Orbital" (control room), "Rush Hour" lo-fi (deadlock lot) and "Ledger" (banker). On Quest they stream from disk.
- **Verification:** `XRModeTests` runs each scene with the XR rig through the XR Interaction Simulator. It checks
  that the XR rig replaces the desktop camera, that world canvases take rays and pokes, that the hand and controller
  interactors can press UI, that ≡ is wired, and that the menu opens within arm's reach.

## 16h. Phone AR (every scene, as of 2026-10-21)

Android phones run every scene in AR (ARCore through AR Foundation 6.6): the control room, the Deadlock and Banker lots,
the CPU scheduling lab and the memory warehouse. Each one is a tabletop model. Quest stays full VR.

- **Finding a surface:** at start the phone shows the camera feed and the AR pill ("Slowly move your phone to find the floor or a table"). Detected horizontal surfaces get a translucent cyan grid, and a placement ring follows the centre of the screen on them. Until the model is placed, the scene is parked 20 km away, beyond the camera's far plane.
- **Placing:** a tap places the model facing the player, on a base (the lot's grass slab, or a dark plinth with a glowing edge in the scene's colour for the rooms), with a soft contact shadow on the real surface and a name plate on its front.

  | Scene | Model | Default scale | Range |
  |---|---|---|---|
  | Control room | 17 × 14 m room | 1 : 26 | 1 : 8 to 1 : 60 |
  | Deadlock / Banker lot | 66 × 58 m city block | 1 : 60 | 1 : 12 to 1 : 140 |
  | CPU lab | 20 × 20 m lab | 1 : 30 | 1 : 8 to 1 : 70 |
  | Memory warehouse | 16 × 11 m warehouse | 1 : 24 | 1 : 6 to 1 : 60 |

- **Dollhouse rooms:** the rooms have no ceiling in AR, and the walls on the sides facing the phone hide as it circles the model, so the player always looks into the room.
- **Gestures:** pinch resizes, a two-finger twist rotates, and a one-finger drag slides the model across the surface. MOVE picks it up to place again, and SHOW/HIDE GRID toggles the plane grid.
- **Hold to zoom:** holding a spot zooms the model 2.5× around it (that spot moves onto the anchor); holding again shows
  the whole model.
- **Feel:** a light haptic tick when a tap lands on something, a firmer one on placing and zooming (Android haptic
  feedback, so the phone's touch-vibration setting applies), and a ring flashes under what was tapped.
- **Lighting and occlusion:** the model's lights follow the real room's brightness and colour (ARCore light
  estimation, `ARLightEstimation`); the lots' sun also turns to the room's main light and takes half the room's ambient
  light. Real things in front of the model hide it on phones with ARCore depth (`AROcclusionManager`; AR Foundation
  scales the real depth by the rig's scale).
- **Playing:** a tap on the model does what a click (or [E]) does on a monitor:
  - the lot: cars and bays;
  - the control room: its monitor rises above the room as a 4× holo screen; tap a scenario card to focus it, tap it again to enter;
  - the CPU lab: desks, the console and the blocks, plus RUN / PAUSE, STEP and SIMULATOR buttons on the HUD;
  - the warehouse: tap a page box (it floats up above its spot), then a frame's pallet.

  World screens with buttons take finger taps directly. Walls cut away for the view never block a tap.
- **One placement for the whole visit:** the AR session outlives the scene (`ARStage.PrepareRig` drops a later scene's own session before it wakes). A scenario entered from the hub appears on the same spot, facing the same way, without scanning again.
- **Placement gate:** the scripted demo and the narrated tours wait for placement (`ScenePlacement.Ready`).
- **How it works:** the scene never moves. The AR session origin is placed and scaled around it (`ARPlacementMath.OriginFor`), so every simulation position stays valid. Per-scene settings are in `ARDioramaSettings` (set by the scene builder).
- **No ARCore:** ARCore is marked *optional*. A phone without it falls back to the touch view (`XRModeController.FallBackFromAR`).
- **Build:**
  - `GridlockPhoneSetup` assigns only the ARCore loader (GLES3 first, min SDK 24).
  - `GridlockQuestSetup` assigns only OpenXR.
  - The builder adds AR Foundation's background renderer feature to the URP renderers.
- **Tests:** ARPlacementMathTests (EditMode) and ARModeTests (PlayMode, `XRModeController.ForceARInEditor`, which stands in for the plane hit). ARModeTests covers placement in every scene, the tap paths, and the hub-to-scenario hand-over on the same spot.

## 16i. Scenarios 02 and 03 (imported, as of 2026-10-14)

Two OS simulations made as separate Unity projects were imported (`Tools/import_scenario.py`, GUIDs kept) into
`Assets/Scenarios/CPUScheduling` and `Assets/Scenarios/MemoryManagement`. Their original scenes stay there untouched;
`GridlockSceneBuilder.Scenarios.cs` opens each one and saves a GRIDLOCK version in `Assets/Scenes`:

| | CPU scheduling lab (02) | Memory warehouse (03) |
|---|---|---|
| Idea | processes queue for one CPU core: FCFS, SRTF, Priority, Round Robin; Gantt board, quiz wall | pallets are frames, boxes are pages: FIFO, LRU, Optimal page replacement |
| Monitor | its own first-person player: WASD, mouse look, E/click on what the dot points at, U simulator window | the same player (built by the generator): E/click a box to carry it, then a pallet to place it; screens are pressed with the dot |
| Phone | drag to look, two fingers to walk, tap to use | same; tap a box, then a pallet |
| Headset | trigger/pinch on any "[E]" object (`ScenarioXRInteraction` adds XR simple interactables, prompt label on hover); A run/pause, B step, X simulator window (a floating panel, `XRWorldCanvas`) | grab boxes with the grip and drop them on a pallet (the scenario's own VR design) |

Both get the GRIDLOCK rig, hint cards, wrist and system menus (RESTART reloads the scene), teleporting on every floor,
the fade, mixer and settings, and a ≡ MENU corner button. The memory scene's own rig, simulator, interaction manager
and event system are removed and references to its camera are remapped to the GRIDLOCK rig.

**Hands everywhere.** The memory scenario's animated hands replace the plastic controller models in every scene: the
fingers curl with the grip and the index finger with the trigger (`ControllerHandAnimator`). Tracked hands are unchanged.

The scenario scripts are in Assembly-CSharp (no asmdef); the glue scripts sit next to them
(`Assets/Scenarios/Shared`, `…/Scripts/Gridlock`) and the generator adds them by type name.

## 17. Scenes

```
Scenes/
├── GridlockHub.unity             (start: sci-fi room, scenario select)
├── GridlockParkingLot.unity      (Deadlock Detection mode)
└── GridlockBanker.unity          (Banker's Algorithm mode)
All are generated by GRIDLOCK ▸ Build Scenes (Assets/Editor/GridlockSceneBuilder*.cs).

The old prototype's scripts were lost (only its scene survives in ~/Deadlock), so there is no Deadlock_TestScene.
Its logic is covered by the EditMode tests instead.
```

## 18. Final demo flow

Start → learn basics → cars acquire spaces → requests on occupied spaces → waiting → circular wait →
**DEADLOCK DETECTED** → Analyze (wait-for arrows) → Banker mode → request appears → GRANT/DENY →
safety algorithm → safe sequence / unsafe explanation → next challenge.
