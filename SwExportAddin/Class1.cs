using System;
using System.IO;
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

        private string GetIconPath()
        {
            try
            {
                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyFolder = Path.GetDirectoryName(assemblyPath);
                string iconPath = Path.Combine(assemblyFolder, "Icons", "icon.bmp");
                
                if (File.Exists(iconPath))
                {
                    Log("Icon found at: " + iconPath);
                    return iconPath;
                }
                else
                {
                    Log("Icon not found at: " + iconPath);
                    return "";
                }
            }
            catch (Exception ex)
            {
                Log("GetIconPath error: " + ex.Message);
                return "";
            }
        }

        private void AddCommand()
        {
            int errors = 0;
            string iconPath = GetIconPath();

            cmdGroup = cmdMgr.CreateCommandGroup2(
                CommandGroupId,
                CommandGroupTitle,
                "Export PDF/DWG",
                iconPath,
                -1,
                false,
                ref errors
            );

            if (cmdGroup == null)
            {
                throw new InvalidOperationException($"No se pudo crear el grupo de comandos. Errors={errors}");
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
                1,
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

        private sealed class ExportOptionsDialog : System.Windows.Forms.Form
        {
            private readonly System.Windows.Forms.CheckBox chkPdf;
            private readonly System.Windows.Forms.CheckBox chkDwg;

            public bool ExportPdf => chkPdf.Checked;
            public bool ExportDwg => chkDwg.Checked;

            public ExportOptionsDialog(string title)
            {
                Text = title;
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
                MinimizeBox = false;
                MaximizeBox = false;
                ShowInTaskbar = false;
                Width = 380;
                Height = 260;
                AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;

                var lbl = new System.Windows.Forms.Label
                {
                    Text = "¿Qué quieres exportar?",
                    AutoSize = true,
                    Left = 20,
                    Top = 20
                };

                chkPdf = new System.Windows.Forms.CheckBox
                {
                    Text = "PDF",
                    Checked = true,
                    AutoSize = true,
                    Left = 25,
                    Top = 60
                };

                chkDwg = new System.Windows.Forms.CheckBox
                {
                    Text = "DWG",
                    Checked = true,
                    AutoSize = true,
                    Left = 25,
                    Top = 100
                };

                var btnOk = new System.Windows.Forms.Button
                {
                    Text = "Aceptar",
                    DialogResult = System.Windows.Forms.DialogResult.OK,
                    Left = 80,
                    Top = 150,
                    Width = 100,
                    Height = 35
                };

                var btnCancel = new System.Windows.Forms.Button
                {
                    Text = "Cancelar",
                    DialogResult = System.Windows.Forms.DialogResult.Cancel,
                    Left = 200,
                    Top = 150,
                    Width = 100,
                    Height = 35
                };

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                Controls.Add(lbl);
                Controls.Add(chkPdf);
                Controls.Add(chkDwg);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);
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

            if (!AskExportFormats("Exportar Plano", out bool exportPdf, out bool exportDwg))
            {
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
            int errors = 0;
            int warnings = 0;
            bool pdfOk = true;
            bool dwgOk = true;

            if (exportPdf)
            {
                string pdf = Path.Combine(pdfFolder, fileName + ".pdf");
                pdfOk = model.Extension.SaveAs(pdf, 0, 0, null, ref errors, ref warnings);
            }

            if (exportDwg)
            {
                string dwg = Path.Combine(dwgFolder, fileName + ".dwg");
                dwgOk = model.Extension.SaveAs(dwg, 0, 0, null, ref errors, ref warnings);
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
            var model = swApp.IActiveDoc2;
            if (model == null)
            {
                System.Windows.Forms.MessageBox.Show("No hay ningún documento activo.");
                return;
            }

            if (!AskExportFormats("Exportar Carpeta Completa", out bool exportPdf, out bool exportDwg))
            {
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
                if (ExportDrawing(f, exportPdf, exportDwg)) success++; else failed++;
            }

            System.Windows.Forms.MessageBox.Show($"Exportación completada. Completados: {success}, Fallidos: {failed}");
        }

        public void RunExportSelect()
        {
            if (!AskExportFormats("Exportar Seleccionables", out bool exportPdf, out bool exportDwg))
            {
                return;
            }

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

                int success = 0;
                int failed = 0;
                foreach (var f in dlg.FileNames)
                {
                    if (ExportDrawing(f, exportPdf, exportDwg)) success++; else failed++;
                }

                System.Windows.Forms.MessageBox.Show($"Exportación completada. Completados: {success}, Fallidos: {failed}");
            }
        }

        private bool ExportDrawing(string path, bool exportPdf, bool exportDwg)
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
            bool pdfOk = true;
            bool dwgOk = true;

            if (exportPdf)
            {
                string pdf = Path.Combine(pdfFolder, name + ".pdf");
                pdfOk = model.Extension.SaveAs(pdf, 0, 0, null, ref errors, ref warnings);
            }

            if (exportDwg)
            {
                string dwg = Path.Combine(dwgFolder, name + ".dwg");
                dwgOk = model.Extension.SaveAs(dwg, 0, 0, null, ref errors, ref warnings);
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