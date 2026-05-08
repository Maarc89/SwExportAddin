using System;
using System.IO;
using System.Reflection;
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

            // No recrear el grupo en cada conexión: evita toolbars duplicadas y botones huérfanos
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

            cmdGroup.AddCommandItem2(
                "Export Batch",
                -1,
                "Exporta el drawing activo a PDF y DWG",
                "ExportBatch",
                0,
                nameof(RunBatchExport),
                "",
                0,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem)
            );

            cmdGroup.AddCommandItem2(
                "Export Folder",
                -1,
                "Exporta todos los drawings (.slddrw) de la carpeta del drawing activo a PDF y DWG",
                "ExportFolder",
                0,
                nameof(RunExportFolder),
                "",
                1,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem)
            );

            cmdGroup.AddCommandItem2(
                "Export Select",
                -1,
                "Selecciona ficheros .slddrw para exportar a PDF y DWG",
                "ExportSelect",
                0,
                nameof(RunExportSelect),
                "",
                2,
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

                // Eliminar TODAS las pestañas existentes con este título (pueden quedar duplicadas en caché)
                while (true)
                {
                    var existingTab = cmdMgr.GetCommandTab((int)swDocumentTypes_e.swDocDRAWING, CommandGroupTitle);
                    if (existingTab == null) break;

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
                int[] textTypes = {
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

        public void RunBatchExport()
        {
            var model = swApp.IActiveDoc2;
            if (model == null)
            {
                System.Windows.Forms.MessageBox.Show("No hay ningún documento activo.");
                return;
            }

            if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                System.Windows.Forms.MessageBox.Show("El documento activo no es un drawing.");
                return;
            }

            string sourcePath = model.GetPathName();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                System.Windows.Forms.MessageBox.Show("Guarda primero el drawing para poder exportarlo en su misma carpeta.");
                return;
            }

            string drawingFolder = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(drawingFolder))
            {
                System.Windows.Forms.MessageBox.Show("No se pudo obtener la carpeta del drawing.");
                return;
            }

            string pdfFolder = Path.Combine(drawingFolder, "PDF");
            string dwgFolder = Path.Combine(drawingFolder, "DWG");
            Directory.CreateDirectory(pdfFolder);
            Directory.CreateDirectory(dwgFolder);

            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            string pdf = Path.Combine(pdfFolder, fileName + ".pdf");
            string dwg = Path.Combine(dwgFolder, fileName + ".dwg");

            int errors = 0;
            int warnings = 0;

            bool pdfOk = model.Extension.SaveAs(pdf, 0, 0, null, ref errors, ref warnings);
            bool dwgOk = model.Extension.SaveAs(dwg, 0, 0, null, ref errors, ref warnings);

            System.Windows.Forms.MessageBox.Show(
                pdfOk && dwgOk
                    ? $"Exportación completada en:\n{pdfFolder}\n{dwgFolder}"
                    : $"La exportación falló. Errores: {errors}, Avisos: {warnings}"
            );
        }

        public void RunExportFolder()
        {
            var model = swApp.IActiveDoc2;
            if (model == null)
            {
                System.Windows.Forms.MessageBox.Show("No hay ningún documento activo.");
                return;
            }

            string sourcePath = model.GetPathName();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                System.Windows.Forms.MessageBox.Show("Guarda primero el drawing para poder localizar la carpeta.");
                return;
            }

            string drawingFolder = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(drawingFolder))
            {
                System.Windows.Forms.MessageBox.Show("No se pudo obtener la carpeta del drawing.");
                return;
            }

            var files = Directory.GetFiles(drawingFolder, "*.slddrw");
            if (files == null || files.Length == 0)
            {
                System.Windows.Forms.MessageBox.Show("No se encontraron drawings (.slddrw) en la carpeta.");
                return;
            }

            int success = 0;
            int failed = 0;

            foreach (var f in files)
            {
                if (ExportDrawing(f)) success++; else failed++;
            }

            System.Windows.Forms.MessageBox.Show($"Exportación completada. Éxitos: {success}, Fallos: {failed}");
        }

        public void RunExportSelect()
        {
            var model = swApp.IActiveDoc2;
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
                dlg.Filter = "SolidWorks Drawing (*.slddrw)|*.slddrw|All files (*.*)|*.*";
                if (!string.IsNullOrWhiteSpace(initialDir)) dlg.InitialDirectory = initialDir;

                var dr = dlg.ShowDialog();
                if (dr != System.Windows.Forms.DialogResult.OK) return;

                int success = 0; int failed = 0;
                foreach (var f in dlg.FileNames)
                {
                    if (ExportDrawing(f)) success++; else failed++;
                }

                System.Windows.Forms.MessageBox.Show($"Exportación completada. Éxitos: {success}, Fallos: {failed}");
            }
        }

        private bool ExportDrawing(string path)
        {
            int errors = 0;
            int warnings = 0;

            var doc = swApp.OpenDoc6(
                path,
                (int)swDocumentTypes_e.swDocDRAWING,
                0,
                "",
                ref errors,
                ref warnings
            );

            if (doc == null)
            {
                System.Diagnostics.Debug.WriteLine($"No se pudo abrir el drawing: {path}");
                return false;
            }

            var model = doc as IModelDoc2;
            if (model == null)
            {
                System.Diagnostics.Debug.WriteLine($"No se pudo cargar el modelo del drawing: {path}");
                return false;
            }

            string folder = Path.GetDirectoryName(path);
            string pdfFolder = Path.Combine(folder, "PDF");
            string dwgFolder = Path.Combine(folder, "DWG");
            Directory.CreateDirectory(pdfFolder);
            Directory.CreateDirectory(dwgFolder);

            string name = Path.GetFileNameWithoutExtension(path);
            string pdf = Path.Combine(pdfFolder, name + ".pdf");
            string dwg = Path.Combine(dwgFolder, name + ".dwg");

            bool pdfOk = model.Extension.SaveAs(pdf, 0, 0, null, ref errors, ref warnings);
            bool dwgOk = model.Extension.SaveAs(dwg, 0, 0, null, ref errors, ref warnings);

            try
            {
                swApp.CloseDoc(model.GetTitle());
            }
            catch { }

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

                RegistryKey hkcu = Registry.CurrentUser;
                RegistryKey hkcuAddin = hkcu.CreateSubKey($@"Software\SolidWorks\AddInsStartup\{{{t.GUID}}}");
                hkcuAddin.SetValue(null, 1, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                RegistryKey hklm = Registry.LocalMachine;
                hklm.DeleteSubKeyTree($@"SOFTWARE\SolidWorks\Addins\{{{t.GUID}}}");

                RegistryKey hkcu = Registry.CurrentUser;
                hkcu.DeleteSubKeyTree($@"Software\SolidWorks\AddInsStartup\{{{t.GUID}}}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}