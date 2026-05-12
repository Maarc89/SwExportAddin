using System.Windows.Forms;

namespace SwExportAddin
{
    internal sealed class ExportOptionsDialog : Form
    {
        private readonly CheckBox chkPdf;
        private readonly CheckBox chkDwg;

        public bool ExportPdf => chkPdf.Checked;
        public bool ExportDwg => chkDwg.Checked;

        public ExportOptionsDialog(string title)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Width = 380;
            Height = 240;
            AutoScaleMode = AutoScaleMode.Font;

            var lbl = new Label
            {
                Text = "¿Qué quieres exportar?",
                AutoSize = true,
                Left = 20,
                Top = 20
            };

            chkPdf = new CheckBox
            {
                Text = "PDF",
                Checked = true,
                AutoSize = true,
                Left = 25,
                Top = 60
            };

            chkDwg = new CheckBox
            {
                Text = "DWG",
                Checked = true,
                AutoSize = true,
                Left = 25,
                Top = 100
            };

            var btnOk = new Button
            {
                Text = "Aceptar",
                DialogResult = DialogResult.OK,
                Left = 80,
                Top = 150,
                Width = 100,
                Height = 35
            };

            var btnCancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Left = 200,
                Top = 150,
                Width = 100,
                Height = 35
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(lbl);
            Controls.Add(chkPdf);
            Controls.Add(chkDwg);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }
    }
}