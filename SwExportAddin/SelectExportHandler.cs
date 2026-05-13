using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;

namespace SwExportAddin
{
    internal sealed class SelectExportHandler
    {
        private readonly ISldWorks swApp;
        private readonly ExportDialogService dialogService;
        private readonly ExportFileProcessor fileProcessor;

        public SelectExportHandler(ISldWorks swApp, ExportDialogService dialogService, ExportFileProcessor fileProcessor)
        {
            this.swApp = swApp;
            this.dialogService = dialogService;
            this.fileProcessor = fileProcessor;
        }

        public void Run()
        {
            if (!dialogService.AskExportFormats("Exportar Seleccionables", out bool exportPdf, out bool exportDwg))
            {
                return;
            }

            IModelDoc2 model = swApp.IActiveDoc2 as IModelDoc2;
            string initialDir = null;
            if (model != null)
            {
                var path = model.GetPathName();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    initialDir = Path.GetDirectoryName(path);
                }
            }

            using (var dlg = new OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Filter = "SolidWorks Drawing (*.slddrw)|*.slddrw|All files (*.*)|*.*";
                if (!string.IsNullOrWhiteSpace(initialDir)) dlg.InitialDirectory = initialDir;

                var dr = dlg.ShowDialog();
                if (dr != DialogResult.OK) return;

                var selectedFiles = dlg.FileNames
                    .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (selectedFiles.Length == 0)
                {
                    MessageBox.Show("No se seleccionaron archivos válidos (.slddrw).", "Exportar Seleccionables");
                    return;
                }

                int success = 0;
                int failed = 0;
                var failedDetails = new List<string>();

                using (var progress = new ExportProgressDialog("Exportación", selectedFiles.Length))
                {
                    progress.Show();
                    progress.Activate();
                    for (int i = 0; i < selectedFiles.Length; i++)
                    {
                        if (progress.CancelRequested)
                        {
                            break;
                        }

                        var file = selectedFiles[i];
                        progress.UpdateProgress(i, selectedFiles.Length, Path.GetFileName(file));

                        var result = ExportProcessHelper.ProcessFileWithConflictPrompt(progress, fileProcessor, file, exportPdf, exportDwg);
                        if (result == FileProcessResult.Success)
                        {
                            success++;
                        }
                        else if (result == FileProcessResult.Failed)
                        {
                            failed++;
                            failedDetails.Add($"- {Path.GetFileName(file)}: fallo en exportación");
                        }
                        else if (result == FileProcessResult.Skipped)
                        {
                            failedDetails.Add($"- {Path.GetFileName(file)}: omitido");
                        }
                        else if (result == FileProcessResult.Cancelled)
                        {
                            break;
                        }

                        progress.UpdateProgress(i + 1, selectedFiles.Length, Path.GetFileName(file));
                        progress.Activate();
                    }

                    var summary = progress.CancelRequested
                        ? $"Exportación cancelada. Completados: {success}, Fallidos: {failed}"
                        : $"Exportación completada. Completados: {success}, Fallidos: {failed}";

                    if (failedDetails.Count > 0)
                    {
                        summary += "\n\nDetalle de fallidos:\n" + string.Join("\n", failedDetails);
                    }

                    progress.Finish(summary);
                    MessageBox.Show(summary);
                }
            }
        }
    }
}
