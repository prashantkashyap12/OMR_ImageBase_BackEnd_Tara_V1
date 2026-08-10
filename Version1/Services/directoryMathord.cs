namespace SQCScanner.Services
{
    public class directoryMathord
    {

        private static readonly HashSet<string> ExcludedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TempManager"
        };

        public static List<string> GetFilteredDirectories(string path)
        {
            if (!Directory.Exists(path))
                return new List<string>();

            return Directory.GetDirectories(path).Where(d => !ExcludedFolders.Contains(Path.GetFileName(d))).ToList();
        }

    }
}
