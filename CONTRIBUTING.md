# Contributing to OS Plays

## One-time setup

1. Install **Unity 6000.3.25f1** with Android Build Support (see the README).
2. Clone the repository and open it from Unity Hub.
3. Let Git merge Unity scenes and prefabs properly with Unity's **Smart Merge** tool. Add this to your Git config, with
   the path to *your* Unity install:

   ```bash
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver '"<Unity editor folder>/Data/Tools/UnityYAMLMerge" merge -p %O %B %A %A'
   git config merge.unityyamlmerge.recursive binary
   ```

   On Windows the editor folder is usually `C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor`; on Linux
   `~/Unity/Hub/Editor/6000.3.25f1/Editor`; on macOS
   `/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents`.
4. Leave **Edit ▸ Project Settings ▸ Editor ▸ Asset Serialization** on *Force Text* (already set): scenes and prefabs
   must stay text so Git can diff and merge them.

## Workflow

- `main` is always working: it builds and the tests pass.
- Do your work on a branch named after it, e.g. `feature/cpu-quiz-timer`, `fix/memory-box-grab`, `docs/readme`.
- Open a **pull request** into `main`. Describe what changed and how you tested it (desktop, headset, phone). Add a
  screenshot for anything visual. Someone else reviews and merges.
- Pull `main` often (`git pull --rebase`) so branches stay small and conflicts stay rare.
- Write commit messages in the imperative, one topic per commit: `Add timer to the CPU quiz`, `Fix box grab layer`.

## Rules that keep the project working

**Generated scenes.** `Assets/Scenes/Gridlock*.unity` are built by `Assets/Editor/GridlockSceneBuilder*.cs`. Change
the builder, run **GRIDLOCK ▸ Build Scenes**, and commit the builder change *and* the regenerated scenes together.
If two branches both change a generated scene, don't hand-merge it: take either version, then rebuild the scenes.

**Imported scenarios.** `Assets/Scenarios/CPUScheduling` and `Assets/Scenarios/MemoryManagement` hold their teams'
original scenes and scripts. Improve them in place with small, targeted changes, and mark them with a
`// GRIDLOCK:` comment so they're easy to find. Glue that adapts them to OS Plays goes in `Assets/Scenarios/Shared`.

**Always commit `.meta` files** with their asset (and delete them with it). A missing `.meta` breaks references for
everyone else. Move and rename assets inside Unity, not in the file manager.

**One person per scene/prefab at a time.** Unity scene files merge badly. Say in the team chat (or the issue) when
you're working on one that isn't generated.

**Big files.** Keep single files under 50 MB (GitHub refuses 100 MB). Compress audio to Ogg, textures to
reasonable sizes. Don't commit `Library/`, `Builds/`, APKs or anything `.gitignore` excludes.

**No secrets.** Never commit signing keystores, passwords or API keys.

## Before you open a pull request

- Run **GRIDLOCK ▸ Build Scenes** if you touched the generator, and play the scenes you changed.
- Run the tests (Test Runner, or the batchmode commands in the README). They must pass.
- If you changed how something works, update `Docs/GRIDLOCK_SPEC.md`; if you added an asset from outside, add it to
  `Docs/CREDITS.md` with its license.

## Code style

- C#: 4-space indentation, one public class per file (named like the file), `PascalCase` for types and methods,
  `camelCase` for fields. Runtime code lives in the `Gridlock.*` namespaces under `Assets/Scripts`.
- Keep comments for the *why*. `.editorconfig` sets indentation and line endings for most editors.

## Adding a new scenario

1. Put its assets under `Assets/Scenarios/<Name>/` (an existing Unity project can be copied in with
   `python3 Tools/import_scenario.py <project folder> <Name> [folders to skip]`, which keeps its references intact).
2. Add a builder (see `GridlockSceneBuilder.Scenarios.cs`) that gives it the shared rig, menus and audio, add it to
   the build settings list, and add its tile to the hub monitor (`GridlockSceneBuilder.Hub.cs`).
3. Add PlayMode tests for desktop and headset like `ScenarioSceneTests`.
