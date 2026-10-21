using System.Reflection;

namespace MatrixToolkit;

public static class BuildInfo
{
    public static int Number => typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>() is { } attribute
        ? Version.Parse(attribute.Version).Revision : 0;
    public static string Label => $"Build {Number:000}";
    public static string FullName => $"Matrix Toolkit · {Label}";
}
