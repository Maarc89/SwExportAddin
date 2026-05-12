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
        private readonly BatchExportHandler batchHandler;
        private readonly FolderExportHandler folderHandler;
        private readonly SelectExportHandler selectHandler;

        public ExportService(ISldWorks swApp, ExportDialogService dialogService, Logger logger)
        {
            var fileProcessor = new ExportFileProcessor(swApp, logger);
            batchHandler = new BatchExportHandler(swApp, dialogService, fileProcessor);
            folderHandler = new FolderExportHandler(swApp, dialogService, fileProcessor);
            selectHandler = new SelectExportHandler(swApp, dialogService, fileProcessor);
        }

        public void RunBatchExport() => batchHandler.Run();

        public void RunExportFolder() => folderHandler.Run();

        public void RunExportSelect() => selectHandler.Run();
    }
}
