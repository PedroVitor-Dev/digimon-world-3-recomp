// Diagnostic harness only: stops at InitHeap or at the game-main boundary.
#include <runtime/cpu_context.h>
#include <runtime/memory.h>
#include <runtime/bios/bios.h>
#include <runtime/cdrom/virtual_fs.h>
#include <algorithm>
#include <cstdint>
#include <cstring>
#include <fstream>
#include <iostream>
#include <iterator>
#include <memory>
#include <stdexcept>
#include <vector>

extern void recomp_init_dispatch_table();
extern void recomp_dispatch(uint8_t*, recomp_context*, uint32_t);

static void require(bool value, const char* message) {
    if (!value) throw std::runtime_error(message);
}

int main(int argc, char** argv) {
    if (argc < 2 || argc > 3 || (argc == 3 && std::strcmp(argv[2], "--through-heap") != 0)) {
        std::cerr << "Usage: startup_probe boot.exe [--through-heap]\n"; return 2;
    }
    const bool throughHeap = argc == 3;
    try {
        std::ifstream input(argv[1], std::ios::binary);
        require(static_cast<bool>(input), "Cannot open boot executable");
        std::vector<uint8_t> bytes{std::istreambuf_iterator<char>(input), {}};
        require(bytes.size() == 923648, "Unexpected executable size");
        require(std::memcmp(bytes.data(), "PS-X EXE", 8) == 0, "Invalid header");
        auto memory = std::make_unique<ps1::Memory>();
        std::memcpy(memory->ramPtr() + 0x10000, bytes.data() + 2048, 921600);
        constexpr uint32_t bssStart = 0x5cce8, bssEnd = 0x82cb0;
        // A nonzero pattern proves the translated startup really clears this range.
        std::fill(memory->ramPtr() + bssStart, memory->ramPtr() + bssEnd, 0xa5);
        memory->ramPtr()[bssStart - 1] = 0x5a;
        memory->ramPtr()[bssEnd] = 0x5a;
        recomp_context context{};
        context.mem = memory.get();
        context.bios = nullptr;
        context.pc = 0x80010e90;
        context.r29 = 0x801ffff0;
        context.r31 = 0x12345678; // Caller RA is stored after the clear.
        ps1::cdrom::VirtualFs filesystem;
        std::unique_ptr<ps1::bios::Bios> bios;
        if (throughHeap) {
            bios = std::make_unique<ps1::bios::Bios>(context, filesystem, *memory);
            context.bios = bios.get();
        }
        recomp_init_dispatch_table();
        bool boundaryReached = false;
        try {
            recomp_dispatch(memory->ramPtr(), &context, context.pc);
        } catch (uint32_t boundary) {
            require(boundary == (throughHeap ? 0x80014524u : 0x8002507cu), "Unexpected diagnostic boundary");
            boundaryReached = true;
        }
        require(boundaryReached, "Expected diagnostic boundary not reached");
        require(memory->read32(0x8005cce8) == 0x12345678, "Caller RA was not preserved");
        require(std::all_of(memory->ramPtr() + bssStart + 4, memory->ramPtr() + bssEnd,
                            [](uint8_t value) { return value == 0; }), "BSS clear incomplete");
        require(memory->ramPtr()[bssStart - 1] == 0x5a && memory->ramPtr()[bssEnd] == 0x5a,
                "BSS clear crossed its boundaries");
        require(context.r29 == 0x80200000, "Unexpected startup stack pointer");
        require(context.r28 == 0x8005cb50, "Unexpected global pointer");
        require(context.r4 == 0x80082cb4, "InitHeap argument/delay slot mismatch");
        require(context.r5 == 0x175350, "Unexpected InitHeap size");
        require(context.r0 == 0, "Zero register changed");
        if (throughHeap) {
            require(memory->read32(0x80082cb4) == 0x175344, "BIOS heap header was not initialized");
            std::cout << "PASS translated InitHeap wrapper and BIOS A0:39 service; reached game-main boundary 0x80014524\n";
        } else {
            std::cout << "PASS translated startup reached InitHeap boundary 0x8002507C\n";
        }
        std::cout
                  << "PASS BSS clear, boundary guards, RA, SP, GP and delayed A0 argument\n"
                  << "DIAGNOSTIC ONLY: game main was not executed; no game boot verified\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL startup probe: " << error.what() << '\n'; return 1;
    }
}
