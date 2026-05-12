using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwExportAddin
{
    internal sealed class ExportService
    {
        private readonly ISldWorks swApp;
        private readonly ExportDialogService dialogService;
        private readonly Logger logger;

        public ExportService(ISldWorks swApp, ExportDialogService dialogService, Logger logger)
        {
            this.swApp = swApp;
            this.dialogService = dialogService;
            this.logger = logger;
        }

        public void RunBatchExport()
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

            if (!dialogService.AskExportFormats("Exportar Plano/Pieza", out bool exportPdf, out bool exportDwg))
            {
                return;
            }

            string sourcePath = model.GetPathName();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                MessageBox.Show("Guarda primero el documento para poder exportarlo en su misma carpeta.");
                return;
            }

            string drawingFolder = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(drawingFolder))
            {
                MessageBox.Show("No se pudo obtener la carpeta del documento.");
                return;
            }

            string pdfFolder = Path.Combine(drawingFolder, "PDF");
            string dwgFolder = Path.Combine(drawingFolder, "DWG");
            Directory.CreateDirectory(pdfFolder);
            Directory.CreateDirectory(dwgFolder);

            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            int errors = 0;
            int warnings = 0;
            bool pdfOk = true;
            bool dwgOk = true;
            string dwgFailureReason = null;

            if (exportPdf)
            {
                string pdf = Path.Combine(pdfFolder, fileName + ".pdf");
                pdfOk = model.Extension.SaveAs(pdf, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
            }

            if (exportDwg)
            {
                string dwg = Path.Combine(dwgFolder, fileName + ".dwg");
                if (modelType == (int)swDocumentTypes_e.swDocPART)
                {
                    dwgOk = ExportPartToDwg(model, sourcePath, dwg, out dwgFailureReason);
                }
                else
                {
                    dwgOk = model.Extension.SaveAs(dwg, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
                }
            }

            if (!pdfOk || !dwgOk)
            {
                if (!string.IsNullOrWhiteSpace(dwgFailureReason))
                {
                    MessageBox.Show($"La exportación falló. {dwgFailureReason}");
                }
                else
                {
                    MessageBox.Show($"La exportación falló. Errores: {errors}, Avisos: {warnings}");
                }

                return;
            }

            var message = "Exportación completada en:\n";
            if (exportPdf)
            {
                message += $"{pdfFolder}\n";
            }
            if (exportDwg)
            {
                message += $"{dwgFolder}\n";
            }

            MessageBox.Show(message.TrimEnd());
        }

        public void RunExportFolder()
        {
            IModelDoc2 model = swApp.IActiveDoc2 as IModelDoc2;
            if (model == null)
            {
                MessageBox.Show("No hay ningún documento activo.");
                return;
            }

            if (!dialogService.AskExportFormats("Exportar Carpeta Completa", out bool exportPdf, out bool exportDwg))
            {
                return;
            }

            var warningResult = MessageBox.Show(
                "Si la carpeta contiene muchos planos/piezas, el proceso puede tardar y SOLIDWORKS no podrá usarse mientras se exporta.\n\n¿Deseas continuar?",
                "Aviso de exportación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            if (warningResult != DialogResult.Yes)
            {
                return;
            }

            string sourcePath = model.GetPathName();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                MessageBox.Show("Guarda primero el documento para poder localizar la carpeta.");
                return;
            }

            string drawingFolder = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(drawingFolder))
            {
                MessageBox.Show("No se pudo obtener la carpeta del documento.");
                return;
            }

            var files = Directory.GetFiles(drawingFolder, "*.slddrw")
                .Concat(Directory.GetFiles(drawingFolder, "*.sldprt"))
                .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (files.Length == 0)
            {
                MessageBox.Show("No se encontraron archivos .slddrw o .sldprt válidos en la carpeta.");
                return;
            }

            int success = 0;
            int failed = 0;
            var failedDetails = new List<string>();

            foreach (var f in files)
            {
                if (ExportSolidWorksFile(f, exportPdf, exportDwg, out string failureReason))
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

        public void RunExportSelect()
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
                    if (ExportSolidWorksFile(f, exportPdf, exportDwg, out string failureReason))
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

        private bool ExportSolidWorksFile(string path, bool exportPdf, bool exportDwg, out string failureReason)
        {
            int errors = 0;
            int warnings = 0;
            failureReason = null;

            if (Path.GetFileName(path).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "archivo temporal (~$)";
                logger.Log($"ExportSolidWorksFile: Skipped temporary file {path}");
                return false;
            }

            int docType;
            string ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (ext == ".slddrw")
            {
                docType = (int)swDocumentTypes_e.swDocDRAWING;
            }
            else if (ext == ".sldprt")
            {
                docType = (int)swDocumentTypes_e.swDocPART;
            }
            else
            {
                failureReason = "extensión no soportada";
                logger.Log($"ExportSolidWorksFile: Unsupported extension {path}");
                return false;
            }

            var doc = swApp.OpenDoc6(path, docType, 0, "", ref errors, ref warnings);
            if (doc == null)
            {
                failureReason = $"no se pudo abrir (Errors={errors}, Warnings={warnings})";
                logger.Log($"ExportSolidWorksFile: Failed to open {path}. Errors={errors}, Warnings={warnings}");
                return false;
            }

            IModelDoc2 model = doc as IModelDoc2;
            if (model == null)
            {
                failureReason = "no se pudo cargar como IModelDoc2";
                logger.Log($"ExportSolidWorksFile: Failed to cast to IModelDoc2: {path}");
                return false;
            }

            string folder = Path.GetDirectoryName(path);
            string pdfFolder = Path.Combine(folder, "PDF");
            string dwgFolder = Path.Combine(folder, "DWG");
            Directory.CreateDirectory(pdfFolder);
            Directory.CreateDirectory(dwgFolder);

            string name = Path.GetFileNameWithoutExtension(path);
            bool hasDrawingTwin = File.Exists(Path.Combine(folder, name + ".slddrw"));
            bool hasPartTwin = File.Exists(Path.Combine(folder, name + ".sldprt"));
            string outputName = (hasDrawingTwin && hasPartTwin)
                ? (name + (ext == ".slddrw" ? "_DRW" : "_PRT"))
                : name;

            bool pdfOk = true;
            bool dwgOk = true;
            string dwgFailureReason = null;

            if (exportPdf)
            {
                string pdf = Path.Combine(pdfFolder, outputName + ".pdf");
                pdfOk = model.Extension.SaveAs(pdf, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
                if (!pdfOk)
                {
                    logger.Log($"ExportSolidWorksFile: PDF export failed for {path}. Errors={errors}, Warnings={warnings}");
                }
            }

            if (exportDwg)
            {
                string dwg = Path.Combine(dwgFolder, outputName + ".dwg");
                if (ext == ".sldprt")
                {
                    dwgOk = ExportPartToDwg(model, path, dwg, out dwgFailureReason);
                    if (!dwgOk)
                    {
                        logger.Log($"ExportSolidWorksFile: DWG export failed for part {path}. {dwgFailureReason}");
                    }
                }
                else
                {
                    dwgOk = model.Extension.SaveAs(dwg, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
                    if (!dwgOk)
                    {
                        logger.Log($"ExportSolidWorksFile: DWG export failed for {path}. Errors={errors}, Warnings={warnings}");
                    }
                }
            }

            try
            {
                swApp.CloseDoc(model.GetTitle());
            }
            catch
            {
            }

            if (!pdfOk || !dwgOk)
            {
                if (!pdfOk && !dwgOk)
                {
                    failureReason = $"falló PDF y DWG (Errors={errors}, Warnings={warnings})";
                    if (!string.IsNullOrWhiteSpace(dwgFailureReason))
                    {
                        failureReason += $". {dwgFailureReason}";
                    }
                }
                else if (!pdfOk)
                {
                    failureReason = $"falló PDF (Errors={errors}, Warnings={warnings})";
                }
                else
                {
                    failureReason = !string.IsNullOrWhiteSpace(dwgFailureReason)
                        ? dwgFailureReason
                        : $"falló DWG (Errors={errors}, Warnings={warnings})";
                }

                return false;
            }

            return true;
        }

        private bool ExportPartToDwg(IModelDoc2 model, string sourcePath, string outputPath, out string failureReason)
        {
            failureReason = null;

            var part = model as IPartDoc;
            if (part == null)
            {
                failureReason = "no se pudo convertir a IPartDoc para exportar DWG";
                return false;
            }

            try
            {
                object alignment = null;
                object views = null;

                bool ok = part.ExportToDWG2(
                    outputPath,
                    sourcePath,
                    (int)swExportToDWG_e.swExportToDWG_ExportSheetMetal,
                    true,
                    alignment,
                    false,
                    false,
                    1,
                    views
                );

                if (!ok)
                {
                    failureReason = "DWG de pieza no disponible para este archivo (solo compatible en casos como chapa desplegada).";
                }

                return ok;
            }
            catch (Exception ex)
            {
                failureReason = "error en flujo DWG de pieza: " + ex.Message;
                logger.Log($"ExportPartToDwg exception for {sourcePath}: {ex}");
                return false;
            }
        }
    }
}
