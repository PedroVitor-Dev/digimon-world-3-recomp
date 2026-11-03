using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace Dw3 {
    public sealed class DiscEntry {
        public string Path;
        public uint Sector;
        public uint Size;
        public bool Directory;
    }
    public sealed class Disc : IDisposable {
        readonly FileStream stream;
        public readonly string Image;
        public readonly int Stride;
        public readonly int Offset;
        public readonly List<DiscEntry> Entries = new List<DiscEntry>();
        public readonly string Volume;
        public Disc(string input) {
            input = System.IO.Path.GetFullPath(input);
            int expected = 0;
            if (System.IO.Path.GetExtension(input).Equals(".cue", StringComparison.OrdinalIgnoreCase)) {
                string cue = File.ReadAllText(input);
                var files = Regex.Matches(cue, "^\\s*FILE\\s+\"([^\"]+)\"\\s+BINARY\\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var tracks = Regex.Matches(cue, @"^\s*TRACK\s+\d+\s+(MODE1/2048|MODE1/2352|MODE2/2352)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (files.Count != 1 || tracks.Count != 1 || Regex.Matches(cue, @"\bTRACK\b", RegexOptions.IgnoreCase).Count != 1 ||
                    !Regex.IsMatch(cue, @"^\s*INDEX\s+01\s+00:00:00\s*$", RegexOptions.Multiline) ||
                    Regex.IsMatch(cue, @"\b(PREGAP|POSTGAP)\b|INDEX\s+00", RegexOptions.IgnoreCase))
                    throw new InvalidDataException("Only single-track CUE with INDEX 01 00:00:00 is supported.");
                expected = tracks[0].Groups[1].Value.EndsWith("2048") ? 2048 : 2352;
                input = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(input), files[0].Groups[1].Value));
            }
            Image = input;
            stream = File.Open(input, FileMode.Open, FileAccess.Read, FileShare.Read);
            try {
                foreach (int stride in new int[] {2048, 2352}) {
                    if (expected != 0 && expected != stride) continue;
                    foreach (int offset in stride == 2048 ? new int[] {0} : new int[] {16, 24}) {
                        stream.Position = 16L * stride + offset;
                        byte[] pvd = new byte[2048];
                        if (stream.Read(pvd, 0, pvd.Length) != pvd.Length) continue;
                        if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001" || pvd[6] != 1) continue;
                        if (BitConverter.ToUInt16(pvd, 128) != 2048) throw new InvalidDataException("Unsupported logical block size.");
                        Stride = stride; Offset = offset;
                        Volume = Encoding.ASCII.GetString(pvd, 40, 32).Trim();
                        Walk(BitConverter.ToUInt32(pvd, 158), BitConverter.ToUInt32(pvd, 166), "", new HashSet<uint>(), 0);
                        return;
                    }
                }
                throw new InvalidDataException("No ISO9660 primary volume found. Extract the archive and select its CUE/BIN/ISO.");
            } catch { stream.Dispose(); throw; }
        }
        public byte[] Read(uint sector, uint size) {
            if (size > 32 * 1024 * 1024) throw new InvalidDataException("Metadata/executable exceeds 32 MiB limit.");
            long count = ((long)size + 2047) / 2048;
            if (((long)sector + count) * Stride > stream.Length) throw new InvalidDataException("Extent outside image.");
            byte[] result = new byte[(int)size];
            int done = 0;
            while (done < result.Length) {
                long lba = (long)sector + done / 2048;
                if (Stride == 2352) {
                    stream.Position = lba * Stride;
                    byte[] header = new byte[24];
                    if (stream.Read(header, 0, 24) != 24 || header[0] != 0 || header[11] != 0)
                        throw new InvalidDataException("Invalid raw sector header.");
                    for (int i = 1; i < 11; i++) if (header[i] != 255) throw new InvalidDataException("Invalid sector sync.");
                    if ((Offset == 16 && header[15] != 1) || (Offset == 24 && (header[15] != 2 || (header[18] & 32) != 0)))
                        throw new InvalidDataException("Expected Mode1 or Mode2 Form1 data sector.");
                }
                stream.Position = lba * Stride + Offset;
                int chunk = Math.Min(2048, result.Length - done);
                int total = 0;
                while (total < chunk) {
                    int n = stream.Read(result, done + total, chunk - total);
                    if (n == 0) throw new EndOfStreamException();
                    total += n;
                }
                done += chunk;
            }
            return result;
        }
        void Walk(uint sector, uint size, string parent, HashSet<uint> seen, int depth) {
            if (depth > 16 || !seen.Add(sector)) throw new InvalidDataException("Cyclic or deeply nested ISO directory.");
            byte[] data = Read(sector, size);
            for (int p = 0; p < data.Length;) {
                int len = data[p];
                if (len == 0) { p = ((p / 2048) + 1) * 2048; continue; }
                if (len < 34 || p + len > data.Length || p % 2048 + len > 2048 || 33 + data[p + 32] > len)
                    throw new InvalidDataException("Malformed ISO directory record.");
                if ((data[p + 25] & 128) != 0) throw new InvalidDataException("Multi-extent files are unsupported.");
                string name = Encoding.ASCII.GetString(data, p + 33, data[p + 32]);
                if (name != "\0" && name != "\u0001") {
                    var entry = new DiscEntry {Path = parent + name.Split(';')[0], Sector = BitConverter.ToUInt32(data, p + 2), Size = BitConverter.ToUInt32(data, p + 10), Directory = (data[p + 25] & 2) != 0};
                    Entries.Add(entry);
                    if (Entries.Count > 100000) throw new InvalidDataException("Too many directory entries.");
                    if (entry.Directory) Walk(entry.Sector, entry.Size, entry.Path + "/", seen, depth + 1);
                }
                p += len;
            }
        }
        public DiscEntry Find(string name) {
            var entry = Entries.Find(e => !e.Directory && e.Path.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (entry == null) throw new InvalidDataException("File not found in disc: " + name);
            return entry;
        }
        public string Hash() { stream.Position = 0; using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(stream)); }
        public static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
        public void Dispose() { stream.Dispose(); }
    }
    public static class Prepare {
        public static string Run(string input, string output) {
            using (var disc = new Disc(input)) {
                var cnf = disc.Find("SYSTEM.CNF");
                string config = Encoding.ASCII.GetString(disc.Read(cnf.Sector, cnf.Size));
                var boot = Regex.Match(config, @"^\s*BOOT\s*=\s*cdrom:\\?([^;\r\n]+)(?:;\d+)?", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (!boot.Success) throw new InvalidDataException("SYSTEM.CNF has no supported BOOT entry.");
                string bootPath = boot.Groups[1].Value.Trim().Replace('\\', '/').TrimStart('/');
                var entry = disc.Find(bootPath);
                byte[] exe = disc.Read(entry.Sector, entry.Size);
                if (exe.Length < 2048 || Encoding.ASCII.GetString(exe, 0, 8) != "PS-X EXE") throw new InvalidDataException("Invalid PS-X EXE header.");
                uint pc = BitConverter.ToUInt32(exe, 16), load = BitConverter.ToUInt32(exe, 24), size = BitConverter.ToUInt32(exe, 28);
                uint ram = load & 0x1fffffff;
                if (size == 0 || (long)size + 2048 > exe.Length || ram < 0x10000 || (long)ram + size > 0x200000 ||
                    (pc & 3) != 0 || (pc & 0x1fffffff) < ram || (pc & 0x1fffffff) >= (long)ram + size)
                    throw new InvalidDataException("Invalid executable payload, RAM bounds or entry point.");
                string exeHash;
                using (var sha = SHA256.Create()) exeHash = Disc.Hex(sha.ComputeHash(exe));
                string imageHash = disc.Hash();
                var manifest = new {
                    schemaVersion = 1, status = "analyzed-not-recompiled", volume = disc.Volume,
                    image = disc.Image, imageSha256 = imageHash, sectorSize = disc.Stride,
                    bootFile = bootPath, executableSha256 = exeHash, entryPoint = "0x" + pc.ToString("X8"),
                    loadAddress = "0x" + load.ToString("X8"), payloadSize = size,
                    compatibility = "Unverified. A serial or hash alone does not establish game compatibility.",
                    files = disc.Entries
                };
                string destination = System.IO.Path.Combine(System.IO.Path.GetFullPath(output), imageHash);
                Directory.CreateDirectory(destination);
                File.WriteAllBytes(System.IO.Path.Combine(destination, "boot.exe"), exe);
                File.WriteAllText(System.IO.Path.Combine(destination, "manifest.json"), new JavaScriptSerializer {MaxJsonLength = 16 * 1024 * 1024}.Serialize(manifest), new UTF8Encoding(false));
                return destination;
            }
        }
    }
}
