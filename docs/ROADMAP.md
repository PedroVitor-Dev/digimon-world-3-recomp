# Engineering roadmap

## Foundation already implemented

Windows launcher and CLI, ISO9660 inventory, SYSTEM.CNF boot discovery, PS-X EXE bounds validation and local SHA-256 manifest. Synthetic tests cover cooked and raw sectors plus malformed inputs. Preparation does not establish game compatibility.

## 1. Architecture and initial research profile — completed

- [Architecture and contracts](ARCHITECTURE.md): component boundaries, clocks, configuration, state compatibility, saves and backups.
- [Initial research copy](INITIAL_PROFILE.md): observed metadata, exact hashes and verification procedure.
- [Build and first-boot plan](BUILD_AND_BOOT.md): existing toolchain, native build direction and dependency acceptance gates.
- Machine-readable identity in `profiles/dw3-sles04936-local-translation.json` and `scripts/verify-profile.ps1`.

This completes the architectural definition and copy identification. It does not complete function discovery, select a recompiler or demonstrate a native boot.

## 2. First native boot — in progress

The C++20 toolchain and Windows diagnostic build are validated. The restricted startup probe reaches the main-entry boundary after InitHeap; a real game screen remains unverified. See [the evidence and commands](NATIVE_BRINGUP.md). The candidate is pinned for evaluation, not adopted as the final runtime.

Continue reviewing a candidate recompiler's source, license, Windows build and instruction/overlay semantics; retain the pinned revision and reviewed portability patch. PS1Recomp remains a candidate, not an adopted game runtime.

Map the entry point, reachable functions, code/data, BIOS calls, indirect branches and dynamically loaded modules. Generate and compile a controlled function set. Integrate required PS1 services and compare boot traces with a reference execution.

Exit: reproducible native executable reaches a real game screen with useful diagnostics, and limitations are documented.

## 3. Correct gameplay and persistent progress

Validate menus, traversal, battles, transitions, FMV, sound, input and memory cards at the original simulation timing. Establish representative test routes and repeat save/reload cycles. Optimize measured bottlenecks after correctness.

Exit: a documented playable session with progress safely saved and resumed.

## 4. Vulkan 1.3 and 4K

Implement PS1 GPU semantics and a Vulkan presentation/rendering backend. Query loader and physical-device capabilities and required features. Separate internal resolution from output resolution; preserve aspect ratio by default.

Exit: correct 3840×2160 output on documented test hardware, with CPU/GPU measurements and remaining image differences recorded.

## 5. High refresh and 160 FPS research

Measure timing dependencies. Separate original simulation from rendering/presentation where possible. Test animation, input, battles, audio and timers after every timing change. Distinguish interpolated frames, presentation refresh, rendered FPS and actual simulation updates.

Exit: validated high-refresh mode or a precise blocker report. 160 FPS remains a target dependent on game behavior and hardware.

## 6. Product launcher and pre-game configuration

Connect selection, identity verification, analysis, generation, compilation and launch. Add progress, cancellation, failure recovery, build caching and shared versioned configuration. Never infer a successful later stage from preparation alone.

Exit: a user can prepare and start a supported copy through the UI, with actionable errors.

## 7. In-game F1 overlay

Add display/audio/input options, configurable shortcuts with conflict handling, hold/toggle fast-forward, slots for states, session and accumulated real playtime, and access to save/backup history. Coordinate pause and input capture. Reuse configuration contracts from the launcher.

Exit: keyboard/controller operation and persistence verified inside the actual runtime. State actions are enabled only after milestone 8's state implementation passes validation.

## 8. Save states and backup management

Build on milestone 3's memory cards. Snapshot complete machine state at safe points, validate compatibility before loading, coordinate persistent-card rollback and back up before destructive restoration. Add checksums, temporary writes, bounded retention and recovery from interrupted writes.

Exit: states and backups restore correctly, reject incompatible/corrupt inputs and preserve the last valid normal save through failures. State format design and serialization hooks start with the runtime; this milestone completes the user-facing feature.

## 9. Distribution and maintenance

Package redistributable project components, keeping user-derived outputs local. Validate Windows 10/11, selected GPU families, controllers and accented paths. Preserve player data across upgrades and cache cleanup. Document exact supported copies, dependency versions and known issues.

Exit: tested public release with a reproducible preparation/build procedure and no bundled disc assets.
