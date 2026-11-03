using System;
using System.IO;
using Dw3;

static class SaveVaultTests {
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject<T>(Action action) where T : Exception {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected rejection: " + typeof(T).Name);
    }
    static byte[] Card(byte marker) {
        byte[] card = new byte[SaveVault.CardSize];
        card[0] = (byte)'M'; card[1] = (byte)'C'; card[127] = (byte)('M' ^ 'C');
        for (int frame = 1; frame < 16; frame++) {
            card[frame * 128] = 0xa0; card[frame * 128 + 8] = card[frame * 128 + 9] = 0xff;
            card[frame * 128 + 127] = 0xa0;
        }
        card[8192] = marker; return card;
    }
    static int Main() {
        string root = Path.Combine(Path.GetTempPath(), "dw3-save-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string content = new String('a', 64), otherContent = new String('b', 64);
        string source = Path.Combine(root, "source.mcd");
        try {
            File.WriteAllBytes(source, Card(1));
            using (var vault = new SaveVault(Path.Combine(root,"vault"), content, 1)) {
                SaveSnapshot first = vault.Import(source);
                Require(File.ReadAllBytes(vault.CardPath)[8192] == 1, "Import failed");
                Require(vault.List().Length == 1, "Incoming checkpoint missing");
                Require(File.ReadAllBytes(source)[8192] == 1, "Import mutated source");
                Console.WriteLine("PASS raw-card import and initial checkpoint");
                Reject<IOException>(() => { using (new SaveVault(Path.Combine(root,"vault"), content, 1)) {} });
                Console.WriteLine("PASS exclusive vault writer lease");
                File.WriteAllBytes(source, Card(2));
                vault.Import(source);
                Require(vault.List().Length == 3, "Previous/incoming snapshots missing");
                vault.Restore(first.Id);
                Require(File.ReadAllBytes(vault.CardPath)[8192] == 1, "Restore failed");
                Require(vault.List().Length == 4, "Pre-restore progress missing");
                Console.WriteLine("PASS restore preserves newer progress first");
                using (new FileStream(vault.CardPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    Reject<IOException>(() => vault.Import(source));
                    Require(File.ReadAllBytes(vault.CardPath)[8192] == 1, "Failed replacement destroyed current progress");
                }
                Require(Directory.GetFiles(Path.GetDirectoryName(vault.CardPath), "*.tmp").Length == 0, "Temporary card leaked");
                Console.WriteLine("PASS failed atomic replacement preserves live card");
                string folder = Path.Combine(Path.GetDirectoryName(vault.CardPath), "backups", first.Id);
                byte[] changed = Card(9); File.WriteAllBytes(Path.Combine(folder, "card.mcd"), changed);
                Reject<InvalidDataException>(() => vault.Restore(first.Id));
                Require(File.ReadAllBytes(vault.CardPath)[8192] == 1, "Corrupt backup changed progress");
                Console.WriteLine("PASS corrupted backup rejected before replacement");
                File.WriteAllBytes(Path.Combine(folder,"card.mcd"), Card(1));
                string metadataPath=Path.Combine(folder,"manifest.json");
                string originalMetadata=File.ReadAllText(metadataPath);
                File.WriteAllText(metadataPath,originalMetadata.Replace(content, otherContent));
                Reject<InvalidDataException>(() => vault.Restore(first.Id));
                File.WriteAllText(metadataPath,originalMetadata);
                Reject<ArgumentException>(() => vault.Restore("../../card.mcd"));
                Console.WriteLine("PASS content identity mismatch and path traversal rejected");
                File.WriteAllBytes(vault.CardPath, new byte[] {1,2,3});
                vault.Restore(first.Id);
                Require(File.ReadAllBytes(vault.CardPath)[8192] == 1, "Recovery from damaged live card failed");
                var recovered = Directory.GetDirectories(Path.Combine(Path.GetDirectoryName(vault.CardPath),"recovery"));
                Require(recovered.Length == 1 && File.ReadAllBytes(Path.Combine(recovered[0],"card.mcd")).Length == 3, "Damaged original was lost");
                vault.List();
                Console.WriteLine("PASS damaged current card preserved separately before recovery");
                var invalid=Card(1); invalid[128] ^= 1; File.WriteAllBytes(source,invalid);
                Reject<InvalidDataException>(() => vault.Import(source));
                File.WriteAllBytes(source,new byte[16]); Reject<InvalidDataException>(() => vault.Import(source));
                Console.WriteLine("PASS invalid checksums and truncated imports rejected");
            }
            using(var vault=new SaveVault(Path.Combine(root,"vault"), content, 1)) {
                Require(File.ReadAllBytes(vault.CardPath)[8192] == 1,"Progress did not survive reopening");
                Console.WriteLine("PASS persistence across vault reopen");
            }
            using(var vault=new SaveVault(Path.Combine(root,"vault"), otherContent, 1)) Require(!File.Exists(vault.CardPath),"Content namespaces overlap");
            using(var vault=new SaveVault(Path.Combine(root,"vault"), content, 2)) Require(!File.Exists(vault.CardPath),"Card slots overlap");
            Reject<ArgumentOutOfRangeException>(() => { using(new SaveVault(root,content,3)) {} });
            Console.WriteLine("PASS content/slot isolation and invalid slot rejection");
            return 0;
        } catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Directory.Delete(root, true); }
    }
}
