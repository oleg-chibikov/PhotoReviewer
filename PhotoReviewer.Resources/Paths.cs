using System;
using System.IO;
using System.Reflection;
using JetBrains.Annotations;

namespace PhotoReviewer.Resources
{
    public static class Paths
    {
        [NotNull] private static readonly string ProgramName =
                $"{((AssemblyCompanyAttribute) Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCompanyAttribute), false)).Company}\\{nameof(PhotoReviewer)}"
            ;

        [NotNull] public static readonly string SettingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProgramName);
    }
}