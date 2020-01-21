﻿using ClangPowerTools.CMake;
using ClangPowerTools.Commands;
using ClangPowerTools.Events;
using ClangPowerTools.Helpers;
using ClangPowerTools.Services;
using ClangPowerTools.Tests;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using Task = System.Threading.Tasks.Task;

namespace ClangPowerTools
{
  public abstract class ClangCommand : BasicCommand
  {
    #region Members

    protected ItemsCollector mItemsCollector;
    protected FilePathCollector mFilePahtCollector;
    protected static RunningProcesses mRunningProcesses = new RunningProcesses();
    protected List<string> mDirectoriesPath = new List<string>();
    private static bool stopCommand = false;
    private readonly Dictionary<string, string> mVsVersions = new Dictionary<string, string>
    {
      {"11.0", "2010"},
      {"12.0", "2012"},
      {"13.0", "2013"},
      {"14.0", "2015"},
      {"15.0", "2017"},
      {"16.0", "2019"}
    };

    private bool mMissingLLVM = false;
    private IVsHierarchy mHierarchy;

    public event EventHandler<VsHierarchyDetectedEventArgs> HierarchyDetectedEvent;
    public event EventHandler<CloseDataStreamingEventArgs> CloseDataStreamingEvent;
    public event EventHandler<ActiveDocumentEventArgs> ActiveDocumentEvent;
    public event EventHandler<ClangCommandMessageEventArgs> IgnoredItemsEvent;

    public bool StopCommand
    {
      get { return stopCommand; }
      protected set { stopCommand = value; }
    }

    #endregion


    #region Properties

    public string Script { get; private set; }

    protected string VsEdition { get; set; }

    protected string VsVersion { get; set; }

    protected string WorkingDirectoryPath { get; set; }

    protected IVsHierarchy ItemHierarchy
    {
      get => mHierarchy;
      set
      {
        if (null == value)
          return;
        mHierarchy = value;
        OnFileHierarchyChanged(new VsHierarchyDetectedEventArgs(mHierarchy));
      }
    }


    #endregion


    #region Constructor


    public ClangCommand(AsyncPackage aPackage, Guid aGuid, int aId)
        : base(aPackage, aGuid, aId)
    {

      if (VsServiceProvider.TryGetService(typeof(DTE), out object dte))
      {
        var dte2 = dte as DTE2;
        VsEdition = dte2.Edition;
        mVsVersions.TryGetValue(dte2.Version, out string version);
        VsVersion = version;
      }
    }

    #endregion


    #region Public Methods

    public void OnMissingLLVMDetected(object sender, MissingLlvmEventArgs e)
    {
      mMissingLLVM = e.MissingLLVM;
    }

    #endregion

    #region Protected Methods

    protected void RunScript(int aCommandId)
    {
      string runModeParameters = ScriptGenerator.GetRunModeParamaters();
      string genericParameters = ScriptGenerator.GetGenericParamaters(aCommandId, VsEdition, VsVersion);

      CMakeBuilder cMakeBuilder = new CMakeBuilder();
      cMakeBuilder.Build();

      InvokeCommand(runModeParameters, genericParameters);

      cMakeBuilder.ClearBuildCashe();
    }

    //Collect files
    protected IEnumerable<IItem> CollectItemsDependingOnCommandLocation(CommandUILocation commandUILocation = CommandUILocation.ContextMenu)
    {
      mItemsCollector = new ItemsCollector();
      switch (commandUILocation)
      {
        case CommandUILocation.Toolbar:
          mItemsCollector.CollectActiveProjectItem();
          SetActiveDocumentEvent();
          break;
        case CommandUILocation.ContextMenu:
          mItemsCollector.CollectSelectedItems();
          break;
      }
      return mItemsCollector.Items;
    }

    private void SetActiveDocumentEvent()
    {
      if (mItemsCollector.Items.Count == 0)
      {
        OnActiveFileCheck(new ActiveDocumentEventArgs(false));
      }
      else
      {
        OnActiveFileCheck(new ActiveDocumentEventArgs(true));
      }
    }

    protected async Task PrepareCommmandAsync(CommandUILocation commandUILocation)
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      DocumentHandler.SaveActiveDocuments();

      if (!VsServiceProvider.TryGetService(typeof(DTE), out object dte))
        return;

      AutomationUtil.SaveDirtyProjects((dte as DTE2).Solution);
      CollectItemsDependingOnCommandLocation(commandUILocation);
    }

    protected void OnActiveFileCheck(ActiveDocumentEventArgs e)
    {
      ActiveDocumentEvent?.Invoke(this, e);
    }

    protected void OnFileHierarchyChanged(VsHierarchyDetectedEventArgs e)
    {
      HierarchyDetectedEvent?.Invoke(this, e);
    }

    protected void OnDataStreamClose(CloseDataStreamingEventArgs e)
    {
      CloseDataStreamingEvent?.Invoke(this, e);
    }

    protected void OnIgnoreItem(ClangCommandMessageEventArgs e)
    {
      IgnoredItemsEvent?.Invoke(this, e);
    }

    #endregion

    #region Private Methods

    private void InvokeCommand(string runModeParameters, string genericParameters)
    {
      var vsSolution = SolutionInfo.IsOpenFolderModeActive() == false ?
        (IVsSolution)VsServiceProvider.GetService(typeof(SVsSolution)) : null;

      foreach (var item in mItemsCollector.Items)
      {
        if (StopCommand)
          break;

        if (IgnoreItem(item, out string fileType))
        {
          OnIgnoreItem(new ClangCommandMessageEventArgs($"Cannot use Clang on {fileType}. To enable Clang, please remove the {fileType} from the ignore list using Clang Power Tools settings.", false));
          continue;
        }

        var itemRelatedParameters = ScriptGenerator.GetItemRelatedParameters(item);

        // From the first parameter is removed the last character which is mandatory "'"
        // and added to the end of the string to close the script
        Script = JoinUtility.Join(" ", runModeParameters.Remove(runModeParameters.Length - 1), itemRelatedParameters, genericParameters, "'");
        CommandTestUtility.ScriptCommand = Script;

        ItemHierarchy = vsSolution != null ? AutomationUtil.GetItemHierarchy(vsSolution, item) : null;

        PowerShellWrapper.Invoke(Script, mRunningProcesses);
      }

      if (StopCommand)
      {
        OnDataStreamClose(new CloseDataStreamingEventArgs(true));
        StopCommand = false;
      }
      else
      {
        OnDataStreamClose(new CloseDataStreamingEventArgs(false));
      }
    }

    private bool IgnoreItem(IItem item, out string fileType)
    {
      fileType = string.Empty;
      if (item is CurrentProjectItem)
      {
        ProjectItem projectItem = item.GetObject() as ProjectItem;
        CompilerSettingsModel compilerSettings = new SettingsProvider().GetCompilerSettingsModel();
        var fileName = projectItem.Name;
        fileType = $"File \"{fileName}\"";
        return compilerSettings.FilesToIgnore.Contains(fileName);
      }
      else if (item is CurrentProject)
      {
        Project project = item.GetObject() as Project;
        CompilerSettingsModel compilerSettings = new SettingsProvider().GetCompilerSettingsModel();
        var projName = project.Name;
        fileType = $"Project \"{projName}\"";
        return compilerSettings.FilesToIgnore.Contains(projName);
      }

      return false;
    }

    #endregion

  }
}
