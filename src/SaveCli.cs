using System;
using System.Globalization;
using System.Web.Script.Serialization;

namespace Dw3 {
    static class SaveCli {
        static int Main(string[] args) {
            if (args.Length < 4 || args.Length > 5 ||
                ((args[0] == "import" || args[0] == "restore") ? args.Length != 5 : args.Length != 4) ||
                (args[0] != "import" && args[0] != "backup" && args[0] != "list" && args[0] != "restore")) {
                Console.Error.WriteLine("Usage: Dw3.Saves.exe <import|backup|list|restore> <vault-root> <content-sha256> <slot:1|2> [card.mcd|backup-id]");
                Console.Error.WriteLine("Offline tool only. The game runtime is not integrated with this vault yet."); return 2;
            }
            try {
                using (var vault = new SaveVault(args[1], args[2], Int32.Parse(args[3], CultureInfo.InvariantCulture))) {
                    object result;
                    if (args[0] == "import") result = vault.Import(args[4]);
                    else if (args[0] == "restore") result = vault.Restore(args[4]);
                    else if (args[0] == "backup") result = vault.Backup();
                    else result = vault.List();
                    Console.WriteLine(new JavaScriptSerializer().Serialize(result));
                    return 0;
                }
            } catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
        }
    }
}
