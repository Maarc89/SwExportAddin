using System;
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
                "Exporta el drawing activo a PDF y DWG",
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
                "Exporta todos los drawings (.slddrw) de la carpeta del drawing activo a PDF y DWG",
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
                "Selecciona ficheros .slddrw para exportar a PDF y DWG",
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

            if (modelType == (int)swDocumentTypes_e.swDocPART && exportDwg)
            {
                if (!exportPdf)
                {
                    System.Windows.Forms.MessageBox.Show("La exportación DWG de .sldprt no está soportada con este método. Selecciona PDF.");
                    return;
                }

                System.Windows.Forms.MessageBox.Show("Para archivos .sldprt se omitirá DWG y se exportará solo PDF.");
                exportDwg = false;
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

            if (exportPdf)
            {
                string pdf = Path.Combine(pdfFolder, fileName + ".pdf");
                pdfOk = model.Extension.SaveAs(pdf, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
            }

            if (exportDwg)
            {
                string dwg = Path.Combine(dwgFolder, fileName + ".dwg");
                dwgOk = model.Extension.SaveAs(dwg, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
            }

            if (!pdfOk || !dwgOk)
            {
                System.Windows.Forms.MessageBox.Show($"La exportación falló. Errores: {errors}, Avisos: {warnings}");
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

            if (exportDwg && files.Any(f => string.Equals(Path.GetExtension(f), ".sldprt", StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.Forms.MessageBox.Show("Aviso: para archivos .sldprt se omitirá DWG y se exportará solo PDF.");
            }

            int success = 0;
            int failed = 0;

            foreach (var f in files)
            {
                if (ExportSolidWorksFile(f, exportPdf, exportDwg)) success++; else failed++;
            }

            System.Windows.Forms.MessageBox.Show($"Exportación completada. Completados: {success}, Fallidos: {failed}");
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

                if (exportDwg && selectedFiles.Any(f => string.Equals(Path.GetExtension(f), ".sldprt", StringComparison.OrdinalIgnoreCase)))
                {
                    System.Windows.Forms.MessageBox.Show("Aviso: para archivos .sldprt se omitirá DWG y se exportará solo PDF.");
                }

                int success = 0;
                int failed = 0;
                foreach (var f in selectedFiles)
                {
                    if (ExportSolidWorksFile(f, exportPdf, exportDwg)) success++; else failed++;
                }

                System.Windows.Forms.MessageBox.Show($"Exportación completada. Completados: {success}, Fallidos: {failed}");
            }
        }

        private bool ExportSolidWorksFile(string path, bool exportPdf, bool exportDwg)
        {
            int errors = 0;
            int warnings = 0;

            if (Path.GetFileName(path).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
            {
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
                Log($"ExportSolidWorksFile: Unsupported extension {path}");
                return false;
            }

            bool exportDwgForThisFile = exportDwg;
            if (ext == ".sldprt" && exportDwg)
            {
                if (!exportPdf)
                {
                    Log($"ExportSolidWorksFile: DWG not supported for .sldprt with current method: {path}");
                    return false;
                }

                exportDwgForThisFile = false;
                Log($"ExportSolidWorksFile: DWG skipped for .sldprt, exporting PDF only: {path}");
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
                System.Diagnostics.Debug.WriteLine($"No se pudo abrir el archivo: {path}");
                Log($"ExportSolidWorksFile: Failed to open {path}. Errors={errors}, Warnings={warnings}");
                return false;
            }

            IModelDoc2 model = doc as IModelDoc2;
            if (model == null)
            {
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

            if (exportPdf)
            {
                string pdf = Path.Combine(pdfFolder, outputName + ".pdf");
                pdfOk = model.Extension.SaveAs(pdf, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
                if (!pdfOk)
                {
                    Log($"ExportSolidWorksFile: PDF export failed for {path}. Errors={errors}, Warnings={warnings}");
                }
            }

            if (exportDwgForThisFile)
            {
                string dwg = Path.Combine(dwgFolder, outputName + ".dwg");
                dwgOk = model.Extension.SaveAs(dwg, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
                if (!dwgOk)
                {
                    Log($"ExportSolidWorksFile: DWG export failed for {path}. Errors={errors}, Warnings={warnings}");
                }
            }

            try
            {
                swApp.CloseDoc(model.GetTitle());
            }
            catch
            {
            }

            return pdfOk && dwgOk;
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