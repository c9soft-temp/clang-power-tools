﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ClangPowerTools
{
  public class CPTSettings
  {
    public static CompilerSettingsModel CompilerSettings { get; set; }
    public static FormatSettingsModel FormatSettings { get; set; }

    private string settingsPath = string.Empty;
    private readonly string SettingsFileName = "cpt_settings.json";
    private readonly string GeneralConfigurationFileName = "GeneralConfiguration.config";
    private readonly string FormatConfigurationFileName = "FormatConfiguration.config";
    private readonly string TidyOptionsConfigurationFileName = "TidyOptionsConfiguration.config";
    private readonly string TidyPredefinedChecksConfigurationFileName = "TidyPredefinedChecksConfiguration.config";

    public CPTSettings()
    {
      SettingsPathBuilder settingsPathBuilder = new SettingsPathBuilder();
      settingsPath = settingsPathBuilder.GetPath("");
    }


    public void SerializeSettings()
    {
      List<object> models = CreateModelsList();

      using (StreamWriter file = File.CreateText(GetSettingsFilePath(settingsPath, SettingsFileName)))
      {
        JsonSerializer serializer = new JsonSerializer();
        serializer.Formatting = Formatting.Indented;
        serializer.Serialize(file, models);
      }
    }

    public void DeserializeSettings()
    {
      using (StreamReader sw = new StreamReader(GetSettingsFilePath(settingsPath, SettingsFileName)))
      {
        string json = sw.ReadToEnd();
        JsonSerializer serializer = new JsonSerializer();
        List<object> models = JsonConvert.DeserializeObject<List<Object>>(json);

        CompilerSettings = JsonConvert.DeserializeObject<CompilerSettingsModel>(models[0].ToString());
        FormatSettings = JsonConvert.DeserializeObject<FormatSettingsModel>(models[1].ToString());
      }
    }

    public void CheckOldSettings()
    {
      ClangOptions clangOptions = LoadOldSettingsFromFile(new ClangOptions(), GeneralConfigurationFileName);
      MapClangOptionsToCompilerSettings(clangOptions);

      ClangFormatOptions clangFormatOptions = LoadOldSettingsFromFile(new ClangFormatOptions(), FormatConfigurationFileName);
      MapClangFormatOptionsToFormatSettings(clangFormatOptions);


      SerializeSettings();
    }

    private T LoadOldSettingsFromFile<T>(T settings, string settingsFileName) where T : new()
    {
      string path = GetSettingsFilePath(settingsPath, settingsFileName);

      if (File.Exists(path))
      {
        SerializeSettings(path, ref settings);
      }
      return settings;
    }

    private void DeleteOldSettings()
    {
      string[] files = Directory.GetFiles(settingsPath, "*.config");
      foreach (var file in files)
      {
        File.Delete(file);
      }
    }

    public void ResetSettings()
    {
      CompilerSettings = new CompilerSettingsModel();
      FormatSettings = new FormatSettingsModel();
    }

    private static List<object> CreateModelsList()
    {
      List<object> models = new List<object>();
      models.Add(CompilerSettings);
      models.Add(FormatSettings);
      return models;
    }

    private string GetSettingsFilePath(string path, string fileName)
    {
      return Path.Combine(path, fileName);
    }

    private void SerializeSettings<T>(string path, ref T config) where T : new()
    {
      XmlSerializer serializer = new XmlSerializer();
      config = serializer.DeserializeFromFile<T>(path);
    }

    private void MapClangOptionsToCompilerSettings(ClangOptions clangOptions)
    {
      CompilerSettings.CompileFlags = clangOptions.ClangFlagsCollection;
      CompilerSettings.FilesToIgnore = clangOptions.FilesToIgnore;
      CompilerSettings.ProjectsToIgnore = clangOptions.ProjectsToIgnore;
      CompilerSettings.AdditionalIncludes = clangOptions.AdditionalIncludes;
      CompilerSettings.WarningsAsErrors = clangOptions.TreatWarningsAsErrors;
      CompilerSettings.ContinueOnError = clangOptions.Continue;
      CompilerSettings.ClangCompileAfterMSCVCompile = clangOptions.ClangCompileAfterVsCompile;
      CompilerSettings.VerboseMode = clangOptions.VerboseMode;
      CompilerSettings.Version = clangOptions.Version;
    }

    private void MapClangFormatOptionsToFormatSettings(ClangFormatOptions clangFormat)
    {
      FormatSettings.FileExtensions = clangFormat.FileExtensions;
      FormatSettings.FilesToIgnore = clangFormat.SkipFiles;
      FormatSettings.AssumeFilename = clangFormat.AssumeFilename;
      FormatSettings.CustomExecutable = clangFormat.ClangFormatPath.Value;
      FormatSettings.Style = clangFormat.Style;
      FormatSettings.FallbackStyle = clangFormat.FallbackStyle;
      FormatSettings.FormatOnSave = clangFormat.EnableFormatOnSave;
    }
  }
}
