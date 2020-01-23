﻿using ClangPowerTools.MVVM.Interfaces;

namespace ClangPowerTools.MVVM.Models
{
  class FormatOptionToggleModel : IFormatOption
  {
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsEnabled { get; set; } = false;
  }
}
