using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace Dw3 {
    public sealed class SaveSnapshot {
        public int SchemaVersion;
        public string Id;
        public string ContentSha256;
        public int Slot;
        public string CreatedUtc;
        public string Reason;
        public string CardSha256;
        public int Size;
    }

    // Offline storage boundary. A future runtime must coordinate through the same lock.
    public sealed class SaveVault : IDisposable {
        public const int CardSize = 128 * 1024;
        readonly string directory;
        readonly string contentHash;
        readonly int slot;
        readonly FileStream lease;
        bool disposed;
        public string CardPath { get { return Path.Combine(directory, "card.mcd"); } }
        public SaveVault(string root, string contentSha256, int cardSlot) {
            if (contentSha256 == null || !Regex.IsMatch(contentSha256, "^[a-f0-9]{64}$")) throw new ArgumentException("Expected lowercase content SHA-256.");
            if (cardSlot != 1 && cardSlot != 2) throw new ArgumentOutOfRangeException("cardSlot");
            contentHash = contentSha256; slot = cardSlot;
            directory = Path.Combine(Path.GetFullPath(root), contentHash, "slot" + slot);
            Directory.CreateDirectory(Path.Combine(directory, "backups"));
            lease = new FileStream(Path.Combine(directory, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        void RequireOpen() { if (disposed) throw new ObjectDisposedException("SaveVault"); }
        public static void ValidateCard(byte[] bytes) {
            if (bytes == null || bytes.Length != CardSize) throw new InvalidDataException("Expected a raw 128 KiB memory card; container formats are unsupported.");
            if (bytes[0] != 'M' || bytes[1] != 'C') throw new InvalidDataException("Missing MC signature.");
            for (int frame = 0; frame < 16; frame++) {
                byte checksum = 0;
                for (int i = 0; i < 127; i++) checksum ^= bytes[frame * 128 + i];
                if (checksum != bytes[frame * 128 + 127]) throw new InvalidDataException("Invalid header/directory checksum in frame " + frame);
            }
        }
        static byte[] ReadCard(string path) {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                if (stream.Length != CardSize) throw new InvalidDataException("Expected a raw 128 KiB card.");
                byte[] bytes = new byte[CardSize];
                int done = 0;
                while (done < bytes.Length) { int count = stream.Read(bytes, done, bytes.Length - done); if (count == 0) throw new EndOfStreamException(); done += count; }
                ValidateCard(bytes); return bytes;
            }
        }
        static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void WriteDurably(string path, byte[] bytes) {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
        }
        SaveSnapshot Snapshot(byte[] card, string reason, bool quarantine = false) {
            string id = Guid.NewGuid().ToString("N");
            string parent = Path.Combine(directory, quarantine ? "recovery" : "backups");
            Directory.CreateDirectory(parent);
            string pending = Path.Combine(parent, ".pending-" + id);
            string destination = Path.Combine(parent, id);
            var snapshot = new SaveSnapshot { SchemaVersion = 1, Id = id, ContentSha256 = contentHash, Slot = slot,
                CreatedUtc = DateTime.UtcNow.ToString("o"), Reason = reason, CardSha256 = Hash(card), Size = card.Length };
            Directory.CreateDirectory(pending);
            try {
                WriteDurably(Path.Combine(pending, "card.mcd"), card);
                WriteDurably(Path.Combine(pending, "manifest.json"), new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(snapshot)));
                Directory.Move(pending, destination);
                return snapshot;
            } catch {
                // Remove only files created by this transaction; keep committed snapshots intact.
                File.Delete(Path.Combine(pending, "card.mcd")); File.Delete(Path.Combine(pending, "manifest.json"));
                if (Directory.Exists(pending)) Directory.Delete(pending);
                throw;
            }
        }
        void ReplaceLive(byte[] bytes) {
            string temporary = Path.Combine(directory, ".card-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                WriteDurably(temporary, bytes);
                if (File.Exists(CardPath)) File.Replace(temporary, CardPath, null);
                else File.Move(temporary, CardPath);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public SaveSnapshot Import(string source) {
            RequireOpen();
            byte[] bytes = ReadCard(source);
            if (File.Exists(CardPath)) Snapshot(ReadCard(CardPath), "before-import");
            // Snapshot the validated incoming bytes before replacing the live card.
            SaveSnapshot incoming = Snapshot(bytes, "import-candidate");
            ReplaceLive(bytes);
            return incoming;
        }
        public SaveSnapshot Backup() { RequireOpen(); return Snapshot(ReadCard(CardPath), "manual-backup"); }
        public SaveSnapshot[] List() {
            RequireOpen();
            var result = new System.Collections.Generic.List<SaveSnapshot>();
            foreach (string folder in Directory.GetDirectories(Path.Combine(directory, "backups"))) {
                string id = Path.GetFileName(folder);
                if (!Regex.IsMatch(id, "^[a-f0-9]{32}$")) continue; // Ignore incomplete transactions.
                byte[] card;
                result.Add(LoadSnapshot(id, out card)); // Report corruption rather than silently hiding it.
            }
            result.Sort((a,b) => String.CompareOrdinal(a.CreatedUtc, b.CreatedUtc));
            return result.ToArray();
        }
        SaveSnapshot LoadSnapshot(string id, out byte[] card) {
            if (id == null || !Regex.IsMatch(id, "^[a-f0-9]{32}$")) throw new ArgumentException("Invalid backup ID.");
            string folder = Path.Combine(directory, "backups", id);
            string metadata = Path.Combine(folder, "manifest.json");
            if (new FileInfo(metadata).Length > 16384) throw new InvalidDataException("Oversized backup manifest.");
            var snapshot = new JavaScriptSerializer().Deserialize<SaveSnapshot>(File.ReadAllText(metadata));
            if (snapshot == null || snapshot.SchemaVersion != 1 || snapshot.Id != id || snapshot.ContentSha256 != contentHash || snapshot.Slot != slot || snapshot.Size != CardSize)
                throw new InvalidDataException("Backup identity/version mismatch.");
            card = ReadCard(Path.Combine(folder, "card.mcd"));
            if (Hash(card) != snapshot.CardSha256) throw new InvalidDataException("Backup SHA-256 mismatch.");
            return snapshot;
        }
        public SaveSnapshot Restore(string id) {
            RequireOpen(); byte[] restored;
            SaveSnapshot snapshot = LoadSnapshot(id, out restored); // Validate before touching current progress.
            if (File.Exists(CardPath)) {
                // Preserve bytes even when the current card is corrupt; do not block recovery.
                if (new FileInfo(CardPath).Length > 1024 * 1024) throw new InvalidDataException("Current card exceeds recovery limit; preserve it separately before restoring.");
                byte[] current = File.ReadAllBytes(CardPath);
                bool damaged = false;
                try { ValidateCard(current); } catch (InvalidDataException) { damaged = true; }
                Snapshot(current, damaged ? "before-restore-damaged" : "before-restore", damaged);
            }
            ReplaceLive(restored);
            return snapshot;
        }
        public void Dispose() { if (!disposed) { disposed = true; lease.Dispose(); } }
    }
}
