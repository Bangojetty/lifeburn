using System.Text.RegularExpressions;

namespace Utilities {
    public static class Utils {
        public const string colorCyan = "<color=#03B3C1>";
        public static string GetStringWithChosenText(string description) {
            if (description == null || description.Length <= 0) return "";
            string tempDescription = description;
            tempDescription = Regex.Replace(tempDescription, @"\{c\}", colorCyan);
            tempDescription = Regex.Replace(tempDescription, @"\{ce\}", "</color>");
            return tempDescription;
        }
        
        
    }
}