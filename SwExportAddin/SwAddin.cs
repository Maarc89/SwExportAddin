using System;
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
        private const string CommandGroupTitle = "Exportación";

        private readonly Logger logger = new Logger();
        private readonly ExportDialogService dialogService = new ExportDialogService();

        private ISldWorks swApp;
        private ICommandManager cmdMgr;
        private ICommandGroup cmdGroup;
        private ICommandTab cmdTab;
        private int addinID;
        private ExportService exportService;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                swApp = (ISldWorks)ThisSW;
                addinID = Cookie;
                exportService = new ExportService(swApp, dialogService, logger);
                logger.Log("ConnectToSW started.");

                swApp.SetAddinCallbackInfo2(0, this, addinID);
                cmdMgr = swApp.GetCommandManager(addinID);
                if (cmdMgr == null)
                {
                    logger.Log("GetCommandManager returned null.");
                    return true;
                }

                CleanupCommandTabs();
                RemoveCommandGroupIfExists();

                try
                {
                    AddCommand();
                    logger.Log("AddCommand completed.");
                }
                catch (Exception ex)
                {
                    logger.Log("AddCommand failed: " + ex);
                }

                try
                {
                    AddDrawingCommandTab();
                    logger.Log("AddDrawingCommandTab completed.");
                }
                catch (Exception ex)
                {
                    logger.Log("AddDrawingCommandTab failed: " + ex);
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Log("ConnectToSW failed: " + ex);
                return true;
            }
        }

        public bool DisconnectFromSW()
        {
            logger.Log("DisconnectFromSW called.");

            try
            {
                RemoveCommandGroupIfExists();
            }
            catch
            {
            }

            cmdTab = null;
            cmdGroup = null;
            cmdMgr = null;
            swApp = null;
            exportService = null;
            return true;
        }

        private void AddCommand()
        {
            int errors = 0;

            cmdGroup = cmdMgr.CreateCommandGroup2(
                CommandGroupId,
                CommandGroupTitle,
                "Export PDF/DWG for drawings",
                "",
                -1,
                false,
                ref errors
            );

            if (cmdGroup == null)
            {
                throw new InvalidOperationException($"No se pudo crear el grupo de comandos. Errors={errors}");
            }

            string smallIcon = IconManager.GetIconFile("ExportPlano.png", 16);
            string largeIcon = IconManager.GetIconFile("ExportPlano.png", 32);

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
                "Exporta el documento activo (.slddrw) a PDF y DWG",
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
                "Exporta los archivos .slddrw de la carpeta del documento activo a PDF y DWG",
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
                logger.Log("AddDrawingCommandTab starting...");

                if (cmdGroup == null)
                {
                    logger.Log("AddDrawingCommandTab aborted because cmdGroup is null.");
                    return;
                }

                var drawingType = (int)swDocumentTypes_e.swDocDRAWING;
                var existingTab = cmdMgr.GetCommandTab(drawingType, CommandGroupTitle);
                if (existingTab != null)
                {
                    try
                    {
                        cmdMgr.RemoveCommandTab(existingTab);
                        logger.Log("Removed existing command tab with the same title.");
                    }
                    catch (Exception ex)
                    {
                        logger.Log("RemoveCommandTab failed: " + ex.Message);
                    }
                }

                cmdTab = cmdMgr.AddCommandTab(drawingType, CommandGroupTitle);
                if (cmdTab == null)
                {
                    logger.Log("AddCommandTab returned null!");
                    return;
                }

                var cmdTabBox = cmdTab.AddCommandTabBox();
                if (cmdTabBox == null)
                {
                    logger.Log("AddCommandTabBox returned null!");
                    return;
                }

                int cmdID0 = cmdGroup.get_CommandID(0);
                int cmdID1 = cmdGroup.get_CommandID(1);
                int cmdID2 = cmdGroup.get_CommandID(2);

                logger.Log($"Command IDs: {cmdID0}, {cmdID1}, {cmdID2}");

                int[] cmdIDs = { cmdID0, cmdID1, cmdID2 };
                int[] textTypes =
                {
                    (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow,
                    (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow,
                    (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow
                };

                cmdTabBox.AddCommands(cmdIDs, textTypes);
                logger.Log("Commands added to tab box successfully.");
            }
            catch (Exception ex)
            {
                logger.Log($"AddDrawingCommandTab exception: {ex}");
            }
        }

        public void RunBatchExport() => exportService?.RunBatchExport();

        public void RunExportFolder() => exportService?.RunExportFolder();

        public void RunExportSelect() => exportService?.RunExportSelect();

        private void CleanupCommandTabs()
        {
            if (cmdMgr == null)
            {
                return;
            }

            try
            {
                while (true)
                {
                    var existingTab = cmdMgr.GetCommandTab((int)swDocumentTypes_e.swDocDRAWING, "Export Tools");
                    if (existingTab == null)
                    {
                        break;
                    }

                    cmdMgr.RemoveCommandTab(existingTab);
                    logger.Log("Removed legacy Export Tools tab.");
                }
            }
            catch (Exception ex)
            {
                logger.Log("CleanupCommandTabs failed: " + ex.Message);
            }
        }

        private void RemoveCommandGroupIfExists()
        {
            if (cmdMgr == null)
            {
                return;
            }

            try
            {
                cmdMgr.RemoveCommandGroup(CommandGroupId);
                logger.Log("Removed existing command group.");
            }
            catch (Exception ex)
            {
                logger.Log("RemoveCommandGroup failed: " + ex.Message);
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
                catch
                {
                }

                RegistryKey hkcu = Registry.CurrentUser;
                try
                {
                    hkcu.DeleteSubKeyTree($@"Software\SolidWorks\Addins\{{{t.GUID}}}");
                }
                catch
                {
                }

                try
                {
                    hkcu.DeleteSubKeyTree($@"Software\SolidWorks\AddInsStartup\{{{t.GUID}}}");
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UnregisterFunction failed: " + ex.Message);
            }
        }
    }
}