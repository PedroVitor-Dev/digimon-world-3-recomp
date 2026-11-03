using System;
using System.IO;
using System.Text;
using Dw3;

static class DiscTests {
    static void Put(byte[] data, int offset, uint value) { Array.Copy(BitConverter.GetBytes(value), 0, data, offset, 4); }
    static int Record(byte[] data, int offset, string name, uint sector, uint size, bool dir) {
        byte[] bytes = Encoding.ASCII.GetBytes(name);
        int length = 33 + bytes.Length + (bytes.Length % 2 == 0 ? 1 : 0);
        data[offset] = (byte)length; Put(data, offset + 2, sector); Put(data, offset + 10, size);
        data[offset + 25] = (byte)(dir ? 2 : 0); data[offset + 32] = (byte)bytes.Length;
        Array.Copy(bytes, 0, data, offset + 33, bytes.Length); return length;
    }
    static byte[] Fixture() {
        byte[] data = new byte[26 * 2048];
        int p = 16 * 2048; data[p] = 1; Encoding.ASCII.GetBytes("CD001").CopyTo(data, p + 1); data[p + 6] = 1;
        data[p + 129] = 8; Record(data, p + 156, "\0", 20, 2048, true);
        p = 20 * 2048;
        p += Record(data, p, "\0", 20, 2048, true);
        p += Record(data, p, "\u0001", 20, 2048, true);
        string config = "BOOT = cdrom:\\SLES_000.00;1\r\n";
        p += Record(data, p, "SYSTEM.CNF;1", 21, (uint)config.Length, false);
        Record(data, p, "SLES_000.00;1", 22, 4096, false);
        Encoding.ASCII.GetBytes(config).CopyTo(data, 21 * 2048);
        p = 22 * 2048; Encoding.ASCII.GetBytes("PS-X EXE").CopyTo(data, p);
        Put(data, p + 16, 0x80010000); Put(data, p + 24, 0x80010000); Put(data, p + 28, 2048);
        return data;
    }
    static void Reject(Action action) {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception("Expected invalid input to be rejected.");
    }
    static int Main() {
        string root = Path.Combine(Path.GetTempPath(), "dw3-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string iso = Path.Combine(root, "fixture.iso");
            byte[] data = Fixture(); File.WriteAllBytes(iso, data);
            string result = Prepare.Run(iso, Path.Combine(root, "prepared"));
            if (!File.Exists(Path.Combine(result, "boot.exe")) || !File.ReadAllText(Path.Combine(result, "manifest.json")).Contains("analyzed-not-recompiled")) throw new Exception("Preparation output missing.");
            Console.WriteLine("PASS ISO preparation and manifest");
            foreach (int offset in new int[] {16, 24}) {
                byte[] raw = new byte[26 * 2352];
                for (int s = 0; s < 26; s++) {
                    for (int b = 1; b < 11; b++) raw[s * 2352 + b] = 255;
                    raw[s * 2352 + 15] = (byte)(offset == 16 ? 1 : 2);
                    Array.Copy(data, s * 2048, raw, s * 2352 + offset, 2048);
                }
                string bin = Path.Combine(root, "fixture.bin"); File.WriteAllBytes(bin, raw);
                string cue = Path.Combine(root, "fixture.cue");
                File.WriteAllText(cue, "FILE \"fixture.bin\" BINARY\r\n TRACK 01 MODE" + (offset == 16 ? "1" : "2") + "/2352\r\n INDEX 01 00:00:00\r\n");
                Prepare.Run(cue, Path.Combine(root, "prepared"));
                Console.WriteLine("PASS CUE raw sector offset " + offset);
                if (offset == 24) {
                    raw[22 * 2352 + 18] = 32; File.WriteAllBytes(bin, raw);
                    Reject(() => Prepare.Run(cue, Path.Combine(root, "prepared")));
                    Console.WriteLine("PASS rejection of Mode2 Form2 executable");
                }
                File.AppendAllText(cue, "TRACK 02 AUDIO\r\n INDEX 01 00:01:00\r\n");
                Reject(() => { using (new Disc(cue)) {} });
            }
            Console.WriteLine("PASS rejection of unsupported multi-track cues");
            Put(data, 22 * 2048 + 28, 0x200000); File.WriteAllBytes(iso, data);
            Reject(() => Prepare.Run(iso, Path.Combine(root, "prepared")));
            Console.WriteLine("PASS rejection of oversized executable payload");
            data = Fixture(); Put(data, 16 * 2048 + 158, 99999); File.WriteAllBytes(iso, data);
            Reject(() => { using (new Disc(iso)) {} });
            Console.WriteLine("PASS rejection of extent outside image");
            File.WriteAllBytes(iso, new byte[64]);
            Reject(() => { using (new Disc(iso)) {} });
            Console.WriteLine("PASS rejection of truncated image");
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { Directory.Delete(root, true); }
    }
}
