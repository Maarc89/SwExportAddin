using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;

namespace SwExportAddin
{
    internal enum FileProcessResult
    {
        Success,
        Failed,
        Skipped,
        Cancelled
    }

    internal sealed class FolderExportHandler
    {
        private readonly ISldWorks swApp;
        private readonly ExportDialogService dialogService;
        private readonly ExportFileProcessor fileProcessor;

        public FolderExportHandler(ISldWorks swApp, ExportDialogService dialogService, ExportFileProcessor fileProcessor)
        {
            this.swApp = swApp;
            this.dialogService = dialogService;
            this.fileProcessor = fileProcessor;
        }

        public void Run()
        {
            IModelDoc2 model = swApp.IActiveDoc2 as IModelDoc2;
            if (model == null)
            {
                MessageBox.Show("No hay ningún documento activo.");
                return;
            }

            string sourcePath = model.GetPathName();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                MessageBox.Show("Guarda primero el documento para poder localizar la carpeta.");
                return;
            }

            if (!dialogService.AskExportFormats("Exportar Carpeta Completa", out bool exportPdf, out bool exportDwg))
            {
                return;
            }

            var warningResult = MessageBox.Show(
                "Si la carpeta contiene muchos planos, el proceso puede tardar y SOLIDWORKS no podrá usarse mientras se exporta.\n\n¿Deseas continuar?",
                "Aviso de exportación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            if (warningResult != DialogResult.Yes)
            {
                return;
            }

            string drawingFolder = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(drawingFolder))
            {
                MessageBox.Show("No se pudo obtener la carpeta del documento.");
                return;
            }

            var files = Directory.GetFiles(drawingFolder, "*.slddrw")
                .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (files.Length == 0)
            {
                MessageBox.Show("No se encontraron archivos .slddrw válidos en la carpeta.");
                return;
            }

            int success = 0;
            int failed = 0;
            var failedDetails = new List<string>();

            using (var progress = new ExportProgressDialog("Exportación", files.Length))
            {
                progress.Show();
                progress.Activate();
                for (int i = 0; i < files.Length; i++)
                {
                    if (progress.CancelRequested)
                    {
                        break;
                    }

                    var file = files[i];
                    progress.UpdateProgress(i, files.Length, Path.GetFileName(file));

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

                    progress.UpdateProgress(i + 1, files.Length, Path.GetFileName(file));
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
