using System;
using System.Windows.Forms;

namespace SwExportAddin
{
    internal sealed class ExportProgressDialog : Form
    {
        private readonly Label lblStatus;
        private readonly Label lblCounter;
        private readonly ProgressBar progressBar;
        private readonly Button btnCancel;
        private bool cancelRequested;
        private bool finished;

        public bool CancelRequested => cancelRequested;

        public ExportProgressDialog(string title, int totalFiles)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            Width = 460;
            Height = 180;
            AutoScaleMode = AutoScaleMode.Font;
            ControlBox = false;
            Shown += (s, e) => BringToFront();

            lblStatus = new Label
            {
                Text = "Procesando archivos...",
                AutoSize = true,
                Left = 20,
                Top = 20
            };

            lblCounter = new Label
            {
                Text = $"0 / {totalFiles}",
                AutoSize = true,
                Left = 20,
                Top = 50
            };

            progressBar = new ProgressBar
            {
                Left = 20,
                Top = 75,
                Width = 400,
                Height = 22,
                Minimum = 0,
                Maximum = Math.Max(totalFiles, 1),
                Value = 0
            };

            btnCancel = new Button
            {
                Text = "Cancelar",
                Left = 335,
                Top = 110,
                Width = 85,
                Height = 28
            };
            btnCancel.Click += BtnCancel_Click;

            Controls.Add(lblStatus);
            Controls.Add(lblCounter);
            Controls.Add(progressBar);
            Controls.Add(btnCancel);
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (finished)
            {
                Close();
                return;
            }

            cancelRequested = true;
            btnCancel.Enabled = false;
            lblStatus.Text = "Cancelando...";
            Application.DoEvents();
        }

        public void UpdateProgress(int processedFiles, int totalFiles, string currentFile)
        {
            if (processedFiles < 0)
            {
                processedFiles = 0;
            }

            if (totalFiles < 1)
            {
                totalFiles = 1;
            }

            if (processedFiles > progressBar.Maximum)
            {
                progressBar.Maximum = Math.Max(totalFiles, processedFiles);
            }
            else if (totalFiles > progressBar.Maximum)
            {
                progressBar.Maximum = totalFiles;
            }

            progressBar.Value = Math.Min(processedFiles, progressBar.Maximum);
            lblCounter.Text = $"{processedFiles} / {totalFiles}";
            lblStatus.Text = string.IsNullOrWhiteSpace(currentFile)
                ? "Procesando archivos..."
                : $"Procesando: {currentFile}";
            Application.DoEvents();
        }

        public void Finish(string finalMessage)
        {
            finished = true;
            lblStatus.Text = finalMessage;
            btnCancel.Text = "Cerrar";
            btnCancel.Enabled = true;
            ControlBox = true;
            Application.DoEvents();
        }
    }
}
