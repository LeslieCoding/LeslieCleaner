# Known Issues

- KI-001: Only OnePlus PKG110/API 36 is verified; API 29/30/33/35 and other OEMs remain NOT_VERIFIED.
- KI-002: Package visibility/OEM policy may return a partial inventory; the UI correctly labels results provider-visible only.
- KI-003: The test device has no share handler, so external chooser success is VERIFIED_WITH_LIMITATION; FileProvider contract itself passes.
- KI-004: Gradle/ddmlib installer returns Error -99 on PKG110; shell-owned install session is the verified workaround.
- KI-005: Some user-facing strings remain in Kotlin source and should move to Android resources before localization expansion.
- KI-006: Actual file/app deletion is intentionally out of scope for 1.0.0 and requires a separate R3/R4 design with preview, confirmation, read-back verification, and rollback.
- KI-007: Device validation is intentionally limited to OnePlus PKG110/API 36 per user instruction; other connected devices are not tested or modified.
- KI-008: Target SDK and update-time metadata are public PackageManager facts; availability can vary with OEM/package visibility policy.
