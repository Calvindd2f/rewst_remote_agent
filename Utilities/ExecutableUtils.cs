using System.Text.RegularExpressions;

namespace RewstAgent.Utilities
{
    public static class ExecutableUtils
    {
        /// <summary>
        /// Extracts the organization ID from the executable filename using regex pattern matching.
        /// The pattern looks for "rewst_" followed by any characters, then an underscore, 
        /// capturing the GUID that appears before the file extension.
        /// </summary>
        public static string GetOrgIdFromExecutableName()
        {
            // Get the executable path from command line arguments
            var executablePath = Environment.GetCommandLineArgs()[0];
            
            // The pattern matches "rewst_" followed by any characters, then captures everything
            // between the last underscore and the file extension
            var pattern = @"rewst_.*_(.+?)\.";
            
            var match = Regex.Match(executablePath, pattern, RegexOptions.IgnoreCase);
            
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
            
            return string.Empty;
        }
    }
}