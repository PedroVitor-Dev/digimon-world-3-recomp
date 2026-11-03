#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <runtime/input/input.h>
#include <array>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <vector>

static void require(bool condition, const char* message) { if (!condition) throw std::runtime_error(message); }
static std::vector<uint8_t> bytes(const std::filesystem::path& path) {
    std::ifstream input(path, std::ios::binary);
    return {std::istreambuf_iterator<char>(input), {}};
}
static uint8_t writeSector(ps1::input::MemoryCard& card, uint16_t sector, uint8_t value, bool bad = false) {
    std::vector<uint8_t> command{0x81,0x57,0,0,static_cast<uint8_t>(sector >> 8),static_cast<uint8_t>(sector)};
    uint8_t checksum = static_cast<uint8_t>((sector >> 8) ^ sector);
    for (int i=0;i<128;i++) { command.push_back(value); checksum ^= value; }
    command.push_back(bad ? static_cast<uint8_t>(checksum ^ 1) : checksum);
    command.insert(command.end(), 3, 0);
    uint8_t reply = 0;
    for (uint8_t valueIn : command) reply = card.transfer(valueIn);
    return reply;
}
int main(int argc, char** argv) {
    if (argc != 2) return 2;
    const auto root = std::filesystem::path(argv[1]);
    std::filesystem::create_directories(root);
    try {
        const auto cardPath = root / "card.mcd";
        ps1::input::MemoryCard card;
        require(card.attachFile(cardPath.string()), "Cannot create card");
        require(bytes(cardPath).size() == card.CARD_SIZE, "Invalid new card length");
        require(writeSector(card, 64, 0x31) == 0x47, "Valid SIO write rejected");
        ps1::input::MemoryCard reopened;
        require(reopened.attachFile(cardPath.string()), "Cannot reopen card");
        require(reopened.image()[8192] == 0x31, "Accepted sector was not persisted");
        std::cout << "PASS SIO sector write survives a new card instance\n";
        auto previous = bytes(cardPath);
        require(writeSector(card, 64, 0x32, true) == 0x4e, "Bad checksum accepted");
        require(bytes(cardPath) == previous, "Bad checksum modified disk");
        std::cout << "PASS bad SIO checksum leaves disk unchanged\n";
        const uint8_t flag = card.flagByte();
        HANDLE lock = CreateFileW(cardPath.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
        require(lock != INVALID_HANDLE_VALUE, "Cannot lock test card");
        const uint8_t reply = writeSector(card, 64, 0x55);
        CloseHandle(lock);
        require(reply == 0x4e, "Persistence failure acknowledged as success");
        require(card.image()[8192] == 0x31 && card.flagByte() == flag, "Failed commit changed in-memory card");
        require(bytes(cardPath) == previous, "Failed commit changed live disk image");
        for (const auto& entry : std::filesystem::directory_iterator(root)) require(entry.path().extension() != ".tmp", "Temporary image leaked");
        std::cout << "PASS Windows replacement failure preserves disk/RAM and reports failure to guest\n";
        const auto shortPath = root / "truncated.mcd";
        { std::ofstream output(shortPath, std::ios::binary); output << "MC"; }
        const auto imageBefore = reopened.image();
        require(!reopened.loadFromFile(shortPath.string()), "Truncated image accepted");
        require(reopened.image() == imageBefore, "Failed load partially modified RAM");
        ps1::input::MemoryCard invalid;
        require(!invalid.attachFile(shortPath.string()) && !invalid.isPresent(), "Malformed backing image exposed as usable card");
        require(bytes(shortPath).size() == 2, "Malformed image was overwritten");
        std::cout << "PASS truncated image rejected without partial load or reformat\n";
        const auto longPath = root / "oversized.mcd";
        { std::ofstream output(longPath,std::ios::binary); output.write(reinterpret_cast<const char*>(previous.data()),previous.size()); output.put('x'); }
        require(!reopened.loadFromFile(longPath.string()), "Oversized image accepted");
        require(reopened.image() == imageBefore, "Oversized image changed RAM");
        std::cout << "PASS oversized image rejected without modifying RAM\n";
        return 0;
    } catch(const std::exception& error) { std::cerr << "FAIL " << error.what() << '\n'; return 1; }
}
