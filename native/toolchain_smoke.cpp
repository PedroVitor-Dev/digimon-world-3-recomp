#include <array>
#include <bit>
#include <cstdint>
#include <iostream>
#include <span>

int main() {
    constexpr std::array<std::uint32_t, 3> words{0x80010e90u, 0x80010000u, 0x000e1000u};
    const std::span<const std::uint32_t> view(words);
    static_assert(sizeof(void*) == 8, "A 64-bit compiler is required");
    static_assert(std::endian::native == std::endian::little, "Little-endian host required");
    if (view.size() != 3 || view.front() != 0x80010e90u) return 1;
    std::cout << "DW3 C++20 x64 toolchain smoke test passed\n";
}
