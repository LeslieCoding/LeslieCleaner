using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class Program {
    private static readonly Regex LogLineRegex = new(@"^\s*(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3}(?:\s+[+-]\d{2}:\d{2})?)\s+\[(?<level>[^\]]+)\]\s+\[(?<kind>[^\]]+)\]\s+(?<message>.*)$", RegexOptions.Compiled);

    public static void Main() {
        string line = "2026-08-28 19:30:05.123 +08:00 [ERR] [Exception] System.NullReferenceException: message";
        Console.WriteLine($"Input Line: {line}");

        var match = LogLineRegex.Match(line);
        
        int errorCount = 0;
        var exceptions = new Dictionary<string, int>();

        if (match.Success) {
            Console.WriteLine("Result: regex matched successfully.");
            string level = match.Groups["level"].Value;
            if (level == "ERR") {
                errorCount++;
                Console.WriteLine("- Error count incremented.");
            }
            // Since it matched, we do NOT enter the fallback logic for exceptions!
        } else {
            Console.WriteLine("Result: regex failed to match.");
            // Fallback simulation
            if (line.Contains("System.")) {
                Console.WriteLine("- Exception captured via fallback.");
                exceptions["System.NullReferenceException"] = 1;
            }
        }

        Console.WriteLine($"Final Error Count: {errorCount}");
        Console.WriteLine($"Final Exceptions Found: {exceptions.Count}");
    }
}
