using System;
using System.Text.RegularExpressions;

public class Program {
    public static void Main() {
        string pattern = @"^\s*(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3}(?:\s+[+-]\d{2}:\d{2})?)\s+\[(?<level>[^\]]+)\]\s+\[(?<kind>[^\]]+)\]\s+(?<message>.*)$";
        string[] tests = {
            "2026-08-28 19:09:28.402 +08:00 [INF] [ScanStart] Scan started", // Normal
            "2026-08-28 19:09:28.402+08:00 [INF] [ScanStart] No space before zone", // Missing space in timezone? (ts format is zzz, which usually implies a space if formatted like that)
            "2026-08-28 19:09:28.402  [INF] [ScanStart] Double space", // Extra spaces after timestamp
            "2026-08-28 19:09:28.402 +08:00[INF][ScanStart] No spaces between components",
        };

        foreach (var t in tests) {
            var m = Regex.Match(t, pattern);
            Console.WriteLine($"Test: '{t}' -> Match={m.Success}");
            if (m.Success) {
                Console.WriteLine($"  ts={m.Groups["timestamp"].Value}");
                Console.WriteLine($"  lvl={m.Groups["level"].Value}");
                Console.WriteLine($"  kind={m.Groups["kind"].Value}");
                Console.WriteLine($"  msg={m.Groups["message"].Value}");
            }
        }
    }
}
