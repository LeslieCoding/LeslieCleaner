using System;
using System.Text.RegularExpressions;

public class Program {
    private static readonly Regex LogLineRegex = new(@"^\s*(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d+(?:\s*[+-]\d{2}:\d{2})?)\s+\[(?<level>[^\]]+)\]\s+\[(?<kind>[^\]]+)\]\s+(?<message>.*)$", RegexOptions.Compiled);

    public static void Main() {
        string[] testLines = {
            "2026-08-28 19:34:29.868 +08:00 [INF] [ScanStart] Success (Standard)",
            "2026-08-28 19:34:29.86 +08:00 [WRN] [DeleteStart] Fail (2 ms digits) -> Now should pass",
            "2026-08-28 19:34:29.8687 +08:00 [ERR] [Exception] Fail (4 ms digits) -> Now should pass",
            "2026-08-28 19:34:29.868+08:00 [ERR] [Exception] Fail (No space before TZ) -> Now should pass",
            "2026-08-28 19:34:29.868  +08:00 [INF] [ScanStart] Success (Extra spaceTZ)",
            "   2026-08-28 19:34:29.868 +08:00 [INF] [ScanStart] Success (Leading space)"
        };

        for (int i = 0; i < testLines.Length; i++) {
            var line = testLines[i];
            var match = LogLineRegex.Match(line);
            if (match.Success) {
                Console.WriteLine($"[{i}] MATCHED: '{line}'");
                Console.WriteLine($"    TS: '{match.Groups["timestamp"].Value}'");
            } else {
                Console.WriteLine($"[{i}] FAILED : '{line}'");
            }
        }
    }
}
