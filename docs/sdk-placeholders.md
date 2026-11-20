# SDK placeholders — replace before release

The real SDKs (AppLovin MAX, Adjust, Firebase Analytics / Crashlytics / Remote Config, Unity IAP) are installed,
but SqadSort has no accounts with them yet. So that Android and iOS builds pass the module preflight in the
meantime, every value below is a **placeholder**. A build made now runs on them: ads report "no ad available",
Adjust and Firebase start against nothing and record nothing. None of this may ship.

Search the project for `TODO(sdk-ids)` / `TODO_SQADSORT` to find each one.

| SDK | Placeholder | Where | Replace with |
|---|---|---|---|
| MAX ad units | All six unit ids empty; `AllowMissingUnitIds = 1` waives the preflight (logged as a warning) | `Assets/SqadSort/Features/Meta/Application/AdUnitConfig.cs` | The six unit ids (Android / iOS × rewarded, interstitial, banner) from AppLovin ▸ MAX ▸ Ad Units, **then set `AllowMissingUnitIds` back to `0`** |
| MAX SDK key | `TODO_SQADSORT_MAX_SDK_KEY` | `Assets/MaxSdk/Resources/AppLovinSettings.asset` (AppLovin ▸ Integration Manager) | The account SDK key |
| MAX consent | Privacy policy URL `https://example.com/sqadsort/privacy-policy-todo` | `ProjectSettings/AppLovinInternalSettings.json` (Integration Manager ▸ Terms and Privacy Policy Flow) | SqadSort's real privacy policy URL (and terms URL, if any) |
| Adjust | App token `sqadsorttodo` | `AdjustAppToken` in `AdUnitConfig.cs` | The 12-character app token from the Adjust dashboard; add `analytics.adjust.token.<wire_name>` event tokens if events should reach Adjust |
| Firebase (Android) | `Assets/google-services.json`, project `sqadsort-placeholder` | `Assets/` | The file downloaded from Firebase Console for the Android app |
| Firebase (iOS) | `Assets/GoogleService-Info.plist`, project `sqadsort-placeholder` | `Assets/` | The file downloaded from Firebase Console for the iOS app |

Also still to decide:

- **Bundle id.** Player Settings still carry Unity's template ids (`com.UnityTechnologies.com.unity.template.urpblank`
  on Android). Pick SqadSort's own before registering the app anywhere — the MAX, Adjust and Firebase apps, and
  both Firebase files, are all keyed to it, and the Firebase preflight checks that the files match it.
- **Store products.** `Assets/SqadSort/Resources/Config/products.json` ids (`squad.sort.*`) must match the products
  created on Google Play / App Store.
- **Unity IAP.** Link the project to a Unity Cloud project (Edit ▸ Project Settings ▸ Services).

Once everything above is real, run the module preflight again (a build, or Framework ▸ Doctor) — it should stay
clean with `AllowMissingUnitIds = 0`.
