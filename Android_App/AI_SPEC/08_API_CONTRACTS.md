# LeslieCleaner Android 2.0.0 — Internal Contracts

```kotlin
interface HealthReader { suspend fun read(): HealthObservation }
interface AnalysisCoordinator { suspend fun run(): AnalysisResult }
```

Contracts return typed observations and preserve unavailable/error causes. UI receives immutable
models and cannot invoke PackageManager, file storage, or destructive operations directly.

The Android implementation may use `Context` internally, but the pure analysis rules do not. New
interfaces are internal to the app until a stable need is evidenced; no external API promise is made.
