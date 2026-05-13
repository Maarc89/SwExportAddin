using System.IO;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwExportAddin
{
    internal sealed class BatchExportHandler
    {
        private readonly ISldWorks swApp;
        private readonly ExportDialogService dialogService;
        private readonly ExportFileProcessor fileProcessor;

        public BatchExportHandler(ISldWorks swApp, ExportDialogService dialogService, ExportFileProcessor fileProcessor)
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

            int modelType = model.GetType();
            if (modelType != (int)swDocumentTypes_e.swDocDRAWING && modelType != (int)swDocumentTypes_e.swDocPART)
            {
                MessageBox.Show("El documento activo debe ser un drawing (.slddrw) o una pieza (.sldprt).");
                return;
            }

            string sourcePath = model.GetPathName();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                MessageBox.Show("Guarda primero el documento antes de exportarlo.");
                return;
            }

            if (!dialogService.AskExportFormats("Exportar Plano/Pieza", out bool exportPdf, out bool exportDwg))
            {
                return;
            }

            var result = ExportProcessHelper.ProcessFileWithConflictPrompt(null, fileProcessor, sourcePath, exportPdf, exportDwg);
            if (result == FileProcessResult.Cancelled)
            {
                return;
            }

            if (result == FileProcessResult.Failed)
            {
                MessageBox.Show("La exportación falló.");
                return;
            }

            string drawingFolder = Path.GetDirectoryName(sourcePath);
            var message = "Exportación completada en:\n";
            if (exportPdf)
            {
                message += Path.Combine(drawingFolder, "PDF") + "\n";
            }
            if (exportDwg)
            {
                message += Path.Combine(drawingFolder, "DWG") + "\n";
            }

            MessageBox.Show(message.TrimEnd());
        }
    }
}
