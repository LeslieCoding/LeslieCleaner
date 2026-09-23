import re

# The regex pattern from LogSummaryBuilder.cs
pattern = r"^\s*(?P<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3}(?:\s+[+-]\d{2}:\d{2})?)\s+\[(?P<level>[^\]]+)\]\s+\[(?P<kind>[^\]]+)\]\s+(?P<message>.*)$"

# Test cases inspired by DiagnosticsServiceTests.cs
test_cases = [
    ("2026-08-28 14:30:05.123 +08:00 [INF] [ScanStart] Scan started", True, "Standard INFO"),
    ("2026-08-28 14:30:05.123 +08:00 [WRN] [DeleteStart] Delete started", True, "Standard WRN"),
    ("2026-08-28 14:30:05.123 +08:00 [ERR] [Exception] Error A", True, "Standard ERR"),
    # What if no timezone?
    ("2026-08-28 14:30:05.123 [INF] [ScanStart] Scan started", True, "No Timezone"),
    # What if no space before level? (Should fail regex which expects \\s+)
    ("2026-08-28 14:30:05.123+08:00[INF][ScanStart]Scan started", False, "No spaces"),
    # What if different timezone format? e.g. +0800 (common in some systems)
    ("2026-08-28 14:30:05.123 +0800 [INF] [ScanStart] Scan started", False, "Timezone without colon"),
]

print(f"{'Test Case':<60} | {'Expected':<8} | {'Actual':<8} | {'Result'}")
print("-" * 90)

for line, expected_match, description in test_cases:
    match = re.match(pattern, line)
    actual_match = match is not None
    result = "PASS" if actual_match == expected_match else "FAIL"
    print(f"{line:<60} | {str(expected_match):<8} | {str(actual_match):<8} | {result} ({description})")
    if match:
        print(f"   Captured -> ts={match.group('timestamp')}, level={match.group('level')}, kind={match.group('kind')}, msg={match.group('message')}")

