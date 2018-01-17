﻿using System;
using System.ComponentModel.Design;
using ClangPowerTools.DialogPages;
using ClangPowerTools.SilentFile;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ClangPowerTools.Commands
{
  /// <summary>
  /// Command handler
  /// </summary>
  internal sealed class ClangFormatCommand : ClangCommand
  {
    #region Members

    private ClangFormatPage mClangFormatPage;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ClangFormatCommand"/> class.
    /// Adds our command handlers for menu (commands must exist in the command table file)
    /// </summary>
    /// <param name="package">Owner package, not null.</param>
    public ClangFormatCommand(Package aPackage, Guid aGuid, int aId) : base(aPackage, aGuid, aId)
    {
      mClangFormatPage = (ClangFormatPage)Package.GetDialogPage(typeof(ClangFormatPage));

      if (ServiceProvider.GetService(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
      {
        var menuCommandID = new CommandID(CommandSet, Id);
        var menuCommand = new OleMenuCommand(this.RunClangFormat, menuCommandID);
        menuCommand.BeforeQueryStatus += mCommandsController.QueryCommandHandler;
        menuCommand.Enabled = true;
        commandService.AddCommand(menuCommand);
      }
    }

    #endregion

    #region  Methods

    /// <summary>
    /// This function is the callback used to execute the command when the menu item is clicked.
    /// See the constructor to see how the menu item is associated with this function using
    /// OleMenuCommandService service and MenuCommand class.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event args.</param>
    private void RunClangFormat(object sender, EventArgs e)
    {
      mCommandsController.Running = true;
      var task = System.Threading.Tasks.Task.Run(() =>
      {
        try
        {
          SaveActiveDocuments();
          CollectSelectedItems(mClangFormatPage.FileExtensions, mClangFormatPage.SkipFiles);

          var silentFileController = new SilentFileController();
          using (var guard = silentFileController.GetSilentFileChangerGuard())
          {
            FilePathCollector fileCollector = new FilePathCollector();
            var filesPath = fileCollector.Collect(mItemsCollector.GetItems);
            silentFileController.SilentFiles(Package, guard, filesPath);

            RunScript(mClangFormatPage);
          }
        }
        catch (Exception exception)
        {
          VsShellUtilities.ShowMessageBox(Package, exception.Message, "Error",
            OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
      }).ContinueWith(tsk => mCommandsController.AfterExecute());
    }

    #endregion

  }
}
