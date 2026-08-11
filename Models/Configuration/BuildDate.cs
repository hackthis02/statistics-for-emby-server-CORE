using System;
using System.Linq;
using System.Reflection;

public static class BuildDateInfo
{
    public static DateTime? GetBuildDate()
    {
        var attribute = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate");

        if (attribute != null && DateTime.TryParse(attribute.Value, out DateTime buildDate))
        {
            return buildDate;
        }

        return null;
    }
}