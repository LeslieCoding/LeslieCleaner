# LeslieCleaner Android 3.0.0 — Task Tree

## EPIC-3: Evidence-based mobile maintenance center

| Task | Goal | Depends on | Risk | Verification |
|---|---|---|---|---|
| T-040 | Rebuild 2.0.0 REG and engineering baseline | none | R1 | REG gate and trace |
| T-041 | Define invariants and verification matrix | T-040 | R1 | static audit |
| T-042 | Add health observation contracts and public readers | T-041 | R2 | JVM reader/rule tests |
| T-043 | Add analysis coordinator/result state with duplicate-run guard | T-042 | R2 | coordinator tests |
| T-044 | Integrate health snapshot and result summary in existing UI shell | T-043 | R2 | OnePlus UI dump |
| T-045 | Upgrade app attention/detail explanation and snapshot migration | T-042 | R2 | unit + OnePlus detail |
| T-046 | Strengthen audit event lifecycle and failure evidence | T-043 | R2 | audit tests |
| T-047 | Version/package 2.0.0 | T-044,T-045,T-046 | R1 | aapt2 + install |
| T-048 | Full build, instrumentation, OnePlus regression, acceptance | T-047 | R2 | release evidence |
| T-050 | Re-run 3.0.0 REG with market research and platform boundaries | none | R1 | REG/gate/trace |
| T-051 | Implement junk candidate contracts, preview, and system cleanup handoff | T-050 | R2 | unit/UI/OnePlus |
| T-052 | Implement security center and Play Protect handoff | T-050 | R2 | signal tests/UI |
| T-053 | Implement startup visibility and OEM/system settings handoff | T-050 | R2 | receiver tests/UI |
| T-054 | Implement acceleration orchestration from memory/storage facts | T-042,T-051 | R2 | unit/UI/OnePlus |
| T-055 | Implement offline-first signed rule update contract | T-050 | R2 | parser/signature/fallback tests |
| T-056 | Expand module navigation/cards while preserving visual system | T-051,T-052,T-053,T-054 | R2 | Compose semantics/OnePlus |
| T-057 | Version 3.0.0 and complete regression/acceptance | T-051..T-056 | R2 | Gradle/OnePlus/audit |

## Execution order

REG → invariants/matrix → contracts/readers → coordinator → UI integration → persistence/audit →
versioning → build/test/debug/retest → OnePlus regression → acceptance/documentation.
