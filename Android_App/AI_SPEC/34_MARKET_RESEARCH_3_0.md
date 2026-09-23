# LeslieCleaner Android 3.0.0 — Market and Platform Research

Research date: 2026-09-09. Sources are official manufacturer, Apple, Google, or Android
documentation where available.

## Observed common modules

| Product/system | Observed module | 3.0.0 translation |
|---|---|---|
| Huawei Optimizer | redundant/large files, app residue, unwanted installers, WeChat data | junk analysis categories and explicit system cleanup handoff |
| Xiaomi Security | cleaner, deep clean, virus/malware scan, privacy/app behavior, battery/performance | security center, safe candidate preview, health and app signals |
| OPPO Phone Manager | overall score, clean storage, privacy permissions, virus scan, payment protection, component check | maintenance score, privacy signals, security checklist, no false payment guarantee |
| vivo iManager | space cleanup, data usage, vulnerability scan | storage insight, data/health extension point, vulnerability checklist |
| OnePlus Phone Manager | storage usage and scan/phone manager entry | OnePlus-compatible storage and settings handoff |
| Apple iPhone Storage | storage breakdown, recommendations, offload unused apps | storage breakdown and app review, not Android deletion impersonation |
| Google Play Protect | periodic harmful-app checks, warnings, disable/remove possible harmful apps | Play Protect status/entry and local evidence-based signals |

## Platform constraints that affect implementation

1. Android 11+ filters package visibility; broad visibility must be justified and minimized. We keep
   provider-visible labeling and avoid `QUERY_ALL_PACKAGES` unless a later policy review authorizes it.
2. Android does not provide a general public API for a third-party app to toggle every other app's
   autostart. 3.0.0 can detect declared boot receivers where visible, explain the signal, and open
   the relevant app/system settings for user action.
3. A normal app cannot honestly claim full-device virus removal. Play Protect is the platform trust
   boundary; our app can surface its entry and add local static signals, not replace it.
4. App cache/data clearing of other packages is system-mediated and confirmation-sensitive. The app
   can preview safe, measurable candidates and hand off to Android/OEM storage settings.
5. Cloud security definitions require a real endpoint, data-minimization policy, signature/version
   verification, failure fallback, and release ownership. No endpoint or credentials were supplied,
   so 3.0.0 defines the contract and offline fallback; production cloud activation is a later REG.

## Official source links

- Huawei cleanup: https://consumer.huawei.com/en/support/content/en-us15952804/
- Xiaomi Play Protect guidance: https://www.mi.com/global/support/faq/details/KA-497482/
- Xiaomi cleaner guidance: https://www.mi.com/global/support/faq/details/KA-569684/
- Xiaomi Security privacy/cloud scan boundary: https://privacy.mi.com/security-pad-global/en_US/
- OPPO Phone Manager cleanup: https://support.oppo.com/uk/answer/?a_id=10571&curPage=18&selModelsId=Accessories&selPbId=62195e14859f45bfab85bb24b50b155c
- vivo iManager guide: https://asia-exstatic-vivofs.vivo.com/PSee2l50xoirPK7y/1727431071157/fb56ca15e5ae5423a2189aeb59ec6359.pdf
- Apple storage management: https://support.apple.com/en-mide/guide/iphone/iph47c931112/ios
- Google Play Protect: https://support.google.com/android/answer/2812853
- Android package visibility: https://developer.android.com/training/package-visibility/declaring
- Android background restrictions: https://developer.android.com/topic/performance/background-optimization
