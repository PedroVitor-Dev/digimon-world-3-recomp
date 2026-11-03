# Engineering roadmap

## 1. Dump preparation (implemented)

Windows launcher and CLI, ISO9660 inventory, SYSTEM.CNF boot discovery, PS-X EXE bounds validation and local SHA-256 manifest. Synthetic tests cover cooked and raw sectors plus malformed inputs. A successful preparation does not establish game compatibility.

## 2. Establish the game-specific recompilation profile

Identify the supplied revision by content. Map the executable, entry point, code/data boundaries, indirect jumps, BIOS calls and dynamically loaded overlays. Document the translated copy separately from an unmodified reference; do not infer revision identity from the archive name or serial alone. Record reproducible hashes locally before producing any supported-revision profile.

Evaluate and pin a recompiler only after reviewing its source, tests, Windows build and game-specific behavior. PS1Recomp is a candidate, not a dependency or a confirmed solution. Its current documented renderer uses OpenGL, so adopting it does not meet the Vulkan target automatically. Record upstream revision and license if adopted.

## 3. Recompiler and runtime vertical slice

Translate a verified reachable MIPS function set to C++ and build it with a Windows C++ toolchain. Validate delay slots, load delays, exceptions and indirect control flow. Integrate the game boot path, RAM, BIOS services, CD-ROM, interrupts, DMA, timers, GTE, SPU and MDEC incrementally. Account for overlays and generated code explicitly. Compare boot traces against a reference implementation before claiming a playable port.

Exit criterion: a reproducible native build reaches a verified game screen with correct rendering and input, with all remaining fallbacks and missing behavior documented.

## 4. Vulkan 1.3 renderer and settings

Implement accurate PS1 GPU command semantics and VRAM behavior, then present using Vulkan 1.3. Query loader and physical-device capabilities; provide a clear unsupported-GPU error. Integrate an F1 settings overlay in the actual runtime, with persisted display, audio and controller configuration. A launcher dialog does not satisfy the in-game requirement.

## 5. Product launcher and validation

Connect analysis, pinned code generation, native compilation and launch with logs and failure recovery. Keep long operations cancellable. Isolate outputs per source hash and toolchain revision. Test save/load, memory cards, battles, world traversal, FMV, sound and translated text; document Windows 10/11 and GPU coverage. Publish only after real gameplay verification.

## References

- [PS-X EXE and SYSTEM.CNF specification](https://psx-spx.consoledev.net/cdromfileformats/)
- [PS1Recomp source and current limitations](https://github.com/PS1Recomp/ps1-recomp)

No C++ compiler/CMake/Ninja was found on PATH during the initial environment inspection; the .NET runtime was present without an SDK. The preparation tools deliberately use the installed .NET Framework compiler. Native runtime development needs its own C++/Vulkan toolchain.
