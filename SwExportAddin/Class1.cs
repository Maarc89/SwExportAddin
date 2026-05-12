using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

namespace SwExportAddin
{
    [ComVisible(true)]
    [Guid("CE1AB140-57A9-4C42-82C5-57FF212F9941")]
    public class SwAddin : ISwAddin
    {
        private const int CommandGroupId = 1;
        private const string CommandGroupTitle = "Export Tools";

        private ISldWorks swApp;
        private ICommandManager cmdMgr;
        private ICommandGroup cmdGroup;
        private ICommandTab cmdTab;
        private int addinID;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                swApp = (ISldWorks)ThisSW;
                addinID = Cookie;
                Log("ConnectToSW started.");

                swApp.SetAddinCallbackInfo2(0, this, addinID);
                cmdMgr = swApp.GetCommandManager(addinID);
                if (cmdMgr == null)
                {
                    Log("GetCommandManager returned null.");
                    return true;
                }

                try
                {
                    AddCommand();
                    Log("AddCommand completed.");
                }
                catch (Exception ex)
                {
                    Log("AddCommand failed: " + ex);
                }

                try
                {
                    AddDrawingCommandTab();
                    Log("AddDrawingCommandTab completed.");
                }
                catch (Exception ex)
                {
                    Log("AddDrawingCommandTab failed: " + ex);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log("ConnectToSW failed: " + ex);
                return true;
            }
        }

        public bool DisconnectFromSW()
        {
            Log("DisconnectFromSW called.");
            
            try
            {
                if (cmdGroup != null)
                {
                    cmdMgr.RemoveCommandGroup(CommandGroupId);
                }
            }
            catch { }

            cmdTab = null;
            cmdGroup = null;
            cmdMgr = null;
            swApp = null;
            return true;
        }

        private void Log(string message)
        {
            try
            {
                string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "SwExportAddin");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "SwExportAddin.log");
                File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{System.Environment.NewLine}");
            }
            catch
            {
            }
        }

        private void AddCommand()
        {
            int errors = 0;

            cmdGroup = cmdMgr.CreateCommandGroup2(
                CommandGroupId,
                CommandGroupTitle,
                "Export PDF/DWG",
                "",
                -1,
                false,
                ref errors
            );

            if (cmdGroup == null)
            {
                throw new InvalidOperationException($"No se pudo crear el grupo de comandos. Errors={errors}");
            }

            string smallIcon = EmbeddedIconManager.GetIconFile("ExportPlano.png", 16);
            string largeIcon = EmbeddedIconManager.GetIconFile("ExportPlano.png", 32);

            if (!string.IsNullOrWhiteSpace(smallIcon))
            {
                cmdGroup.SmallIconList = smallIcon;
                cmdGroup.SmallMainIcon = smallIcon;
            }

            if (!string.IsNullOrWhiteSpace(largeIcon))
            {
                cmdGroup.LargeIconList = largeIcon;
                cmdGroup.LargeMainIcon = largeIcon;
            }

            cmdGroup.AddCommandItem2(
                "Exportar Plano",
                -1,
                "Exporta el documento activo (.slddrw/.sldprt) a PDF y DWG",
                "Exportar Plano",
                0,
                nameof(RunBatchExport),
                "",
                0,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem)
            );

            cmdGroup.AddCommandItem2(
                "Exportar Carpeta Completa",
                -1,
                "Exporta los archivos .slddrw y .sldprt de la carpeta del documento activo a PDF y DWG",
                "Exportar Carpeta Completa",
                0,
                nameof(RunExportFolder),
                "",
                0,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem)
            );

            cmdGroup.AddCommandItem2(
                "Exportar Seleccionables",
                -1,
                "Selecciona ficheros .slddrw/.sldprt para exportar a PDF y DWG",
                "Exportar Seleccionables",
                0,
                nameof(RunExportSelect),
                "",
                0,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem)
            );

            cmdGroup.HasToolbar = true;
            cmdGroup.HasMenu = true;
            cmdGroup.Activate();
        }

        private void AddDrawingCommandTab()
        {
            try
            {
                Log("AddDrawingCommandTab starting...");

                while (true)
                {
                    var existingTab = cmdMgr.GetCommandTab((int)swDocumentTypes_e.swDocDRAWING, CommandGroupTitle);
                    if (existingTab == null)
                    {
                        break;
                    }

                    try
                    {
                        cmdMgr.RemoveCommandTab(existingTab);
                        Log("Removed duplicated command tab.");
                    }
                    catch (Exception ex)
                    {
                        Log("RemoveCommandTab failed: " + ex.Message);
                        break;
                    }
                }

                cmdTab = cmdMgr.AddCommandTab((int)swDocumentTypes_e.swDocDRAWING, CommandGroupTitle);
                if (cmdTab == null)
                {
                    Log("AddCommandTab returned null!");
                    return;
                }

                var cmdTabBox = cmdTab.AddCommandTabBox();
                if (cmdTabBox == null)
                {
                    Log("AddCommandTabBox returned null!");
                    return;
                }

                int cmdID0 = cmdGroup.get_CommandID(0);
                int cmdID1 = cmdGroup.get_CommandID(1);
                int cmdID2 = cmdGroup.get_CommandID(2);

                Log($"Command IDs: {cmdID0}, {cmdID1}, {cmdID2}");

                int[] cmdIDs = { cmdID0, cmdID1, cmdID2 };
                int[] textTypes =
                {
                    (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow,
                    (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow,
                    (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow
                };

                cmdTabBox.AddCommands(cmdIDs, textTypes);
                Log("Commands added to tab box successfully.");
            }
            catch (Exception ex)
            {
                Log($"AddDrawingCommandTab exception: {ex}");
            }
        }

        private bool AskExportFormats(string title, out bool exportPdf, out bool exportDwg)
        {
            using (var dlg = new ExportOptionsDialog(title))
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    exportPdf = false;
                    exportDwg = false;
                    return false;
                }

                exportPdf = dlg.ExportPdf;
                exportDwg = dlg.ExportDwg;

                if (!exportPdf && !exportDwg)
                {
                    System.Windows.Forms.MessageBox.Show("Selecciona al menos PDF o DWG.");
                    return false;
                }

                return true;
            }
        }

        public void RunBatchExport()
        {
            IModelDoc2 model = swApp.IActiveDoc2 as IModelDoc2;
            if (model == null)
            {
                System.Windows.Forms.MessageBox.Show("No hay ningún documento activo.");
                return;
            }

            int modelType = model.GetType();
            if (modelType != (int)swDocumentTypes_e.swDocDRAWING && modelType != (int)swDocumentTypes_e.swDocPART)
            {
                System.Windows.Forms.MessageBox.Show("El documento activo debe ser un drawing (.slddrw) o una pieza (.sldprt).");
                return;
            }

            if (!AskExportFormats("Exportar Plano/Pieza", out bool exportPdf, out bool exportDwg))
            {
                return;
            }

            string sourcePath = model.GetPathName();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                System.Windows.Forms.MessageBox.Show("Guarda primero el documento para poder exportarlo en su misma carpeta.");
                return;
            }

            string drawingFolder = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(drawingFolder))
            {
                System.Windows.Forms.MessageBox.Show("No se pudo obtener la carpeta del documento.");
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
                    System.Windows.Forms.MessageBox.Show($"La exportación falló. {dwgFailureReason}");
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show($"La exportación falló. Errores: {errors}, Avisos: {warnings}");
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

            System.Windows.Forms.MessageBox.Show(message.TrimEnd());
        }

        public void RunExportFolder()
        {
            IModelDoc2 model = swApp.IActiveDoc2 as IModelDoc2;
            if (model == null)
            {
                System.Windows.Forms.MessageBox.Show("No hay ningún documento activo.");
                return;
            }

            if (!AskExportFormats("Exportar Carpeta Completa", out bool exportPdf, out bool exportDwg))
            {
                return;
            }

            var warningResult = System.Windows.Forms.MessageBox.Show(
                "Si la carpeta contiene muchos planos/piezas, el proceso puede tardar y SOLIDWORKS no podrá usarse mientras se exporta.\n\n¿Deseas continuar?",
                "Aviso de exportación",
                System.Windows.Forms.MessageBoxButtons.YesNo,
                System.Windows.Forms.MessageBoxIcon.Warning
            );
            if (warningResult != System.Windows.Forms.DialogResult.Yes)
            {
                return;
            }

            string sourcePath = model.GetPathName();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                System.Windows.Forms.MessageBox.Show("Guarda primero el documento para poder localizar la carpeta.");
                return;
            }

            string drawingFolder = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(drawingFolder))
            {
                System.Windows.Forms.MessageBox.Show("No se pudo obtener la carpeta del documento.");
                return;
            }

            var files = Directory.GetFiles(drawingFolder, "*.slddrw")
                .Concat(Directory.GetFiles(drawingFolder, "*.sldprt"))
                .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (files.Length == 0)
            {
                System.Windows.Forms.MessageBox.Show("No se encontraron archivos .slddrw o .sldprt válidos en la carpeta.");
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

            System.Windows.Forms.MessageBox.Show(summary);
        }

        public void RunExportSelect()
        {
            if (!AskExportFormats("Exportar Seleccionables", out bool exportPdf, out bool exportDwg))
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

            using (var dlg = new System.Windows.Forms.OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Filter = "SolidWorks Drawing/Part (*.slddrw;*.sldprt)|*.slddrw;*.sldprt|SolidWorks Drawing (*.slddrw)|*.slddrw|SolidWorks Part (*.sldprt)|*.sldprt|All files (*.*)|*.*";
                if (!string.IsNullOrWhiteSpace(initialDir)) dlg.InitialDirectory = initialDir;

                var dr = dlg.ShowDialog();
                if (dr != System.Windows.Forms.DialogResult.OK) return;

                var selectedFiles = dlg.FileNames
                    .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (selectedFiles.Length == 0)
                {
                    System.Windows.Forms.MessageBox.Show("No se seleccionaron archivos válidos (.slddrw/.sldprt).", "Exportar Seleccionables");
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

                System.Windows.Forms.MessageBox.Show(summary);
            }
        }

        private bool ExportSolidWorksFile(string path, bool exportPdf, bool exportDwg)
        {
            return ExportSolidWorksFile(path, exportPdf, exportDwg, out _);
        }

        private bool ExportSolidWorksFile(string path, bool exportPdf, bool exportDwg, out string failureReason)
        {
            int errors = 0;
            int warnings = 0;
            failureReason = null;

            if (Path.GetFileName(path).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "archivo temporal (~$)";
                Log($"ExportSolidWorksFile: Skipped temporary file {path}");
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
                Log($"ExportSolidWorksFile: Unsupported extension {path}");
                return false;
            }

            var doc = swApp.OpenDoc6(
                path,
                docType,
                0,
                "",
                ref errors,
                ref warnings
            );

            if (doc == null)
            {
                failureReason = $"no se pudo abrir (Errors={errors}, Warnings={warnings})";
                System.Diagnostics.Debug.WriteLine($"No se pudo abrir el archivo: {path}");
                Log($"ExportSolidWorksFile: Failed to open {path}. Errors={errors}, Warnings={warnings}");
                return false;
            }

            IModelDoc2 model = doc as IModelDoc2;
            if (model == null)
            {
                failureReason = "no se pudo cargar como IModelDoc2";
                System.Diagnostics.Debug.WriteLine($"No se pudo cargar el modelo: {path}");
                Log($"ExportSolidWorksFile: Failed to cast to IModelDoc2: {path}");
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
                    Log($"ExportSolidWorksFile: PDF export failed for {path}. Errors={errors}, Warnings={warnings}");
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
                        Log($"ExportSolidWorksFile: DWG export failed for part {path}. {dwgFailureReason}");
                    }
                }
                else
                {
                    dwgOk = model.Extension.SaveAs(dwg, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
                    if (!dwgOk)
                    {
                        Log($"ExportSolidWorksFile: DWG export failed for {path}. Errors={errors}, Warnings={warnings}");
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
                Log($"ExportPartToDwg exception for {sourcePath}: {ex}");
                return false;
            }
        }

        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                RegistryKey hklm = Registry.LocalMachine;
                RegistryKey addinKey = hklm.CreateSubKey($@"SOFTWARE\SolidWorks\Addins\{{{t.GUID}}}");
                addinKey.SetValue(null, 1, RegistryValueKind.DWord);
                addinKey.SetValue("Description", "Export PDF/DWG Batch");
                addinKey.SetValue("Title", "SwExportAddin");
                addinKey.Close();

                RegistryKey hkcu = Registry.CurrentUser;
                RegistryKey hkcuAddin = hkcu.CreateSubKey($@"Software\SolidWorks\Addins\{{{t.GUID}}}");
                hkcuAddin.SetValue(null, 1, RegistryValueKind.DWord);
                hkcuAddin.SetValue("Description", "Export PDF/DWG Batch");
                hkcuAddin.SetValue("Title", "SwExportAddin");
                hkcuAddin.Close();

                RegistryKey hkcuStartup = hkcu.CreateSubKey($@"Software\SolidWorks\AddInsStartup\{{{t.GUID}}}");
                hkcuStartup.SetValue(null, 1, RegistryValueKind.DWord);
                hkcuStartup.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RegisterFunction failed: " + ex.Message);
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                RegistryKey hklm = Registry.LocalMachine;
                try
                {
                    hklm.DeleteSubKeyTree($@"SOFTWARE\SolidWorks\Addins\{{{t.GUID}}}");
                }
                catch { }

                RegistryKey hkcu = Registry.CurrentUser;
                try
                {
                    hkcu.DeleteSubKeyTree($@"Software\SolidWorks\Addins\{{{t.GUID}}}");
                }
                catch { }

                try
                {
                    hkcu.DeleteSubKeyTree($@"Software\SolidWorks\AddInsStartup\{{{t.GUID}}}");
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UnregisterFunction failed: " + ex.Message);
            }
        }
    }
}