# Digimon World 3 Recompiled

An in-development project to bring Digimon World 3 (PlayStation) to Windows through static recompilation.

**The game is not playable yet.** The current milestone is a working Windows dump preparation launcher and command-line disc analyzer. No game runtime, MIPS recompiler, Vulkan renderer or in-game F1 menu is implemented yet.

## Run the current tools

On Windows 10/11 x64 with .NET Framework 4.8, double-click `Launch.cmd`. It builds the launcher using the framework C# compiler, without downloading packages. Select an **extracted** single-track CUE/BIN or ISO, then choose **Analisar e preparar**. Extract `.7z` archives manually first. The GUI performs analysis in a background task.

From PowerShell in the project directory:

```powershell
.\scripts\build.ps1
.\build\Dw3.Launcher.exe prepare 'path\to\disc.cue' '.\local\prepared'
.\scripts\test.ps1
```

The CLI exits with 0 on success, 1 on analysis failure and 2 for invalid arguments. Its output is the destination directory. Launching with no arguments opens the GUI; the console executable also retains an attached terminal for diagnostics.

Each preparation creates `local/prepared/<image-sha256>/manifest.json` and `boot.exe`. The manifest records the actual disc-image hash (not the CUE or archive hash), executable hash, boot path, sector layout, volume label, entry point, load address and ISO file inventory. This is analysis, **not compilation**; `boot.exe` is the original MIPS binary and cannot run directly on Windows. No supported game revision is claimed yet. Patched copies must be analyzed separately.

Supported layouts: ISO9660 with 2048-byte sectors; raw Mode1/2352 and Mode2 Form1/2352; single-file, single-track CUE with INDEX 01 at zero and no pregap. Multi-track discs, nonzero index offsets, Mode2 Form2 executable sectors, multi-extent files and non-ISO filesystems are rejected. The tool reads directory metadata and the boot executable; large asset containers are inventoried without loading their contents. It does not verify every asset sector, EDC/ECC, or identify all overlays.

## Product target

- Native Windows 10/11 x64 runtime, with Vulkan 1.3 on a compatible GPU.
- User-provided extracted game copy; no bundled disc assets.
- Launcher for dump selection, validation, local recompilation and launch.
- In-game settings opened with F1.
- Configurable shortcuts, fast-forward, save/load states, session and accumulated playtime.
- Persistent memory cards, versioned backups and restoration.
- 4K output and high-refresh presentation; actual 160 FPS requires game-specific timing research and hardware validation.

Vulkan 1.3 is a requirement for the **planned game renderer**, not this preparation utility. GPU compatibility is not checked yet. See [the technical roadmap](docs/ROADMAP.md) for the remaining work.

## Architecture and initial research copy

The [architecture](docs/ARCHITECTURE.md) defines the launcher/runtime boundary, data contracts, clocks and persistence strategy. The [initial research profile](docs/INITIAL_PROFILE.md) records the supplied copy without claiming native compatibility. The [build and boot plan](docs/BUILD_AND_BOOT.md) defines the next engineering gates.

After preparing the supplied copy, verify its actual image and executable hashes:

```powershell
.\scripts\verify-profile.ps1
```

The default profile is `profiles/dw3-sles04936-local-translation.json`. It is an identity record, not a function map or a ready-to-use recompiler configuration. An unknown or modified image needs a separate reviewed profile. No backend has been selected or pinned yet.

## Local data and contributions

`roms/`, `local/` and `build/` are ignored by Git. Keep original and generated game data there. Do not commit extracted executables or disc images. Preparation artifacts contain local paths and game-derived data and should remain local.

Use Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `build:`). Keep relevant changes in separate commits. Push is performed manually by the repository owner.

GPL-3.0; see LICENSE. No third-party recompiler code is bundled.
