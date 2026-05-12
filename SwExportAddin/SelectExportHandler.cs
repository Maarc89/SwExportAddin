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
                dlg.Filter = "SolidWorks Drawing/Part (*.slddrw;*.sldprt)|*.slddrw;*.sldprt|SolidWorks Drawing (*.slddrw)|*.slddrw|SolidWorks Part (*.sldprt)|*.sldprt|All files (*.*)|*.*";
                if (!string.IsNullOrWhiteSpace(initialDir)) dlg.InitialDirectory = initialDir;

                var dr = dlg.ShowDialog();
                if (dr != DialogResult.OK) return;

                var selectedFiles = dlg.FileNames
                    .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (selectedFiles.Length == 0)
                {
                    MessageBox.Show("No se seleccionaron archivos válidos (.slddrw/.sldprt).", "Exportar Seleccionables");
                    return;
                }

                int success = 0;
                int failed = 0;
                var failedDetails = new List<string>();
                foreach (var f in selectedFiles)
                {
                    if (fileProcessor.ExportSolidWorksFile(f, exportPdf, exportDwg, out string failureReason))
                    {
                        success++;
                    }
                    else
                    {
                        failed++;
                        failedDetails.Add($"- {Path.GetFileName(f)}: {failureReason}");
                    }
                }

                var summary = $"Exportación completada. Completados: {success}, Fallidos: {failed}";
                if (failedDetails.Count > 0)
                {
                    summary += "\n\nDetalle de fallidos:\n" + string.Join("\n", failedDetails);
                }

                MessageBox.Show(summary);
            }
        }
    }
}
