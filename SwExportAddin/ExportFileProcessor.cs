using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwExportAddin
{
    internal sealed class ExportFileProcessor
    {
        private readonly ISldWorks swApp;
        private readonly Logger logger;

        public ExportFileProcessor(ISldWorks swApp, Logger logger)
        {
            this.swApp = swApp;
            this.logger = logger;
        }

        public bool ExportSolidWorksFile(string path, bool exportPdf, bool exportDwg, out string failureReason)
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
