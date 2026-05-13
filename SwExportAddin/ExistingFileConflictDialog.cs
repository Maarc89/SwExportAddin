using System;
using System.Windows.Forms;

namespace SwExportAddin
{
    internal enum ExistingFileDecision
    {
        Rename,
        Overwrite,
        Skip,
        Cancel
    }

    internal sealed class ExistingFileConflictDialog : Form
    {
        private readonly Label lblMessage;
        private readonly Button btnRename;
        private readonly Button btnOverwrite;
        private readonly Button btnSkip;
        private readonly Button btnCancel;

        public ExistingFileDecision Decision { get; private set; } = ExistingFileDecision.Cancel;

        public ExistingFileConflictDialog(string fileName, string destinationFolder)
        {
            Text = "Archivo existente";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            Width = 520;
            Height = 230;
            AutoScaleMode = AutoScaleMode.Font;

            lblMessage = new Label
            {
                Text = $"Ya existe un archivo para:\n{fileName}\n\nDestino: {destinationFolder}\n\n¿Qué quieres hacer?",
                AutoSize = false,
                Left = 20,
                Top = 15,
                Width = 470,
                Height = 100
            };

            btnRename = new Button
            {
                Text = "Renombrar",
                Left = 20,
                Top = 125,
                Width = 105,
                Height = 30
            };
            btnRename.Click += (s, e) => { Decision = ExistingFileDecision.Rename; DialogResult = DialogResult.OK; };

            btnOverwrite = new Button
            {
                Text = "Sobrescribir",
                Left = 135,
                Top = 125,
                Width = 105,
                Height = 30
            };
            btnOverwrite.Click += (s, e) => { Decision = ExistingFileDecision.Overwrite; DialogResult = DialogResult.OK; };

            btnSkip = new Button
            {
                Text = "Omitir",
                Left = 250,
                Top = 125,
                Width = 105,
                Height = 30
            };
            btnSkip.Click += (s, e) => { Decision = ExistingFileDecision.Skip; DialogResult = DialogResult.OK; };

            btnCancel = new Button
            {
                Text = "Cancelar",
                Left = 365,
                Top = 125,
                Width = 105,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };

            CancelButton = btnCancel;
            Controls.Add(lblMessage);
            Controls.Add(btnRename);
            Controls.Add(btnOverwrite);
            Controls.Add(btnSkip);
            Controls.Add(btnCancel);
        }
    }
}
