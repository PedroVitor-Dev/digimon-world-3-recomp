using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace Dw3 {
    static class Program {
        [STAThread] static int Main(string[] args) {
            if (args.Length != 0) {
                if (args.Length != 3 || args[0] != "prepare") { Console.Error.WriteLine("Usage: Dw3.Launcher.exe prepare <disc.cue|bin|iso> <output-directory>"); return 2; }
                try { Console.WriteLine(Prepare.Run(args[1], args[2])); return 0; }
                catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Launcher()); return 0;
        }
    }
    sealed class Launcher : Form {
        readonly TextBox path = new TextBox {Dock = DockStyle.Fill};
        readonly TextBox log = new TextBox {Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical};
        readonly Button browse = new Button {Text = "Selecionar dump", AutoSize = true};
        readonly Button prepare = new Button {Text = "Analisar e preparar", AutoSize = true};
        public Launcher() {
            Text = "Digimon World 3 | Recompilation Project";
            MinimumSize = new Size(720, 420); Size = new Size(860, 500);
            Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterScreen;
            var layout = new TableLayoutPanel {Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 5, ColumnCount = 1};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label {Text = "DIGIMON WORLD 3", Font = new Font("Segoe UI", 22, FontStyle.Bold), AutoSize = true}, 0, 0);
            layout.Controls.Add(new Label {Text = "Projeto em desenvolvimento. Esta etapa prepara o dump; o jogo ainda nao esta disponivel para jogar.", Dock = DockStyle.Fill}, 0, 1);
            layout.Controls.Add(path, 0, 2);
            var actions = new FlowLayoutPanel {Dock = DockStyle.Fill}; actions.Controls.Add(browse); actions.Controls.Add(prepare);
            layout.Controls.Add(actions, 0, 3); layout.Controls.Add(log, 0, 4); Controls.Add(layout);
            log.Text = "Selecione o CUE/BIN/ISO extraido da sua copia. Arquivos .7z devem ser extraidos primeiro.\r\nOs resultados ficam em local/prepared e nao entram no Git.";
            browse.Click += delegate { using (var dialog = new OpenFileDialog {Filter = "Disco PlayStation|*.cue;*.bin;*.iso", CheckFileExists = true}) { if (dialog.ShowDialog(this) == DialogResult.OK) path.Text = dialog.FileName; } };
            prepare.Click += async delegate {
                browse.Enabled = prepare.Enabled = path.Enabled = false;
                log.Text = "Lendo ISO9660, validando executavel e calculando SHA-256...";
                string input = path.Text;
                try {
                    string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
                    string result = await Task.Run(() => Prepare.Run(input, Path.Combine(root, "local", "prepared")));
                    log.Text = "Analise concluida.\r\n" + result + "\r\n\r\nGerados: boot.exe e manifest.json.\r\nCompatibilidade nao verificada. Recompilacao, Vulkan e menu F1 em jogo ainda pendentes.";
                } catch (Exception e) { log.Text = "Falha: " + e.Message; }
                finally { browse.Enabled = prepare.Enabled = path.Enabled = true; }
            };
        }
    }
}
