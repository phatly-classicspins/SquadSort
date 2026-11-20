# Firebase Analytics provider 0.1.0

Local, unpublished PrototypeFramework 0.9.0 service module. A consuming SKU adds **Analytics — Firebase** in Framework/Setup Wizard ▸ Modules. It contributes one singleton as both `IAnalyticsProvider` and `IAnalyticsCommandTarget` (`analytics-firebase`), replaces no boot node, and adds no gameplay SDK dependency. No vendor SDK is bundled here. The source tag in metadata is a distribution target, not a claim that a tag exists.

## Install in the consuming SKU

1. Add the module from the Wizard offer. Install its registry requirements: OpenUPM `https://package.openupm.com`, scope `com.google.external-dependency-manager`, pin `com.google.external-dependency-manager` **1.2.190**. Package metadata depends only on framework **0.9.0**, supported range **>=0.9.0 <0.10.0**.
2. Download Firebase Unity SDK **14.1.0** UPM tarballs from the [official Firebase Unity setup guide](https://firebase.google.com/docs/unity/setup#available-libraries). Place and commit `com.google.firebase.app-14.1.0.tgz` and `com.google.firebase.analytics-14.1.0.tgz` in the SKU's `Packages/ManualSdk/` (Git LFS recommended). Do this in the consuming project, never this framework repository. Do not also import the `.unitypackage` distribution or a second EDM4U copy.
3. Manually add these entries to the SKU's `Packages/manifest.json` dependencies. Wizard Install does not fetch/import SDKs or write these manual lines:

   ```json
   "com.google.firebase.app": "file:ManualSdk/com.google.firebase.app-14.1.0.tgz",
   "com.google.firebase.analytics": "file:ManualSdk/com.google.firebase.analytics-14.1.0.tgz"
   ```

   Paths are relative to `Packages/`, remain inside the project, and point to committed tarballs for reproducible CI. `PF_FIREBASE` comes from versionDefines on `com.google.firebase.analytics >=14.1.0`; never set it by hand. The SDK assembly explicitly references `Firebase.App.dll`, `Firebase.Analytics.dll`, `Firebase.Platform.dll`.
4. Register matching app identifiers in Firebase Console. Put Android `google-services.json` and/or iOS `GoogleService-Info.plist` under `Assets/`. Use the files for the correct project and target; preflight checks presence, not credentials or app-id correctness.
5. Create `FirebaseAnalyticsInstaller.asset` using Framework/Modules/Create Installer Assets or Assets ▸ Create ▸ PrototypeFramework ▸ Analytics ▸ Firebase Analytics Installer. Slot it on RootLifetimeScope's game installers in the Master Scene through the live Unity Editor. Doctor check 16 reports missing requirements.
6. Set Android **minSdk >=24**. Configure consent and, if needed, the ad signals switch before the first analytics work. Verify on devices before shipping.

When Firebase is absent the ungated installer uses a refusing Null bridge. Every bridge call warns with `pf-analytics-firebase` and refuses without throwing. The installer remains present; the provider never silently forwards to another SDK.

## Shared events and properties

Use framework `IAnalytics` through your existing forwarders. Every record supplies `WireName()` and `WriteParams()`; arbitrary SKU records work without SDK knowledge. Strings, long and double keep their wire types; bool becomes long 1/0 through framework `WireValues`. Names and parameter keys are unchanged. Event string values truncate at **100 UTF-16 code units**, user property values at **36**, avoiding split surrogate pairs. The module warns once per event wire name and once per property key. Property names use the shared `UserPropKeyValidator`; null values clear a property.

`AdRevenuePaid` becomes exactly one `ad_impression` with six fields: `ad_platform`, `ad_source`, `ad_format`, `ad_unit_name`, `value`, `currency`. `applovin_max` maps to `AppLovin`, matching the [Firebase Unity revenue example](https://firebase.google.com/docs/analytics/measure-ad-revenue#applovin); other mediator strings pass through. Placement/precision are omitted. The generic `ad_revenue_paid` event is never also sent. Funnel events generate no money. Purchase revenue is unsupported in 0.1.0; purchase facts still forward as ordinary wire events. Install only one provider sending to Firebase; avoid duplicate automatic ad impression collection from other integrations (including AdMob).

## Consent, configuration and pending work

Startup is lazy: granted, resolved Analytics consent plus first event, property or ordinary command triggers `CheckAndFixDependenciesAsync`. The provider reads only Analytics purpose from `IConsentService`. Grant callbacks alone do not start Firebase. Native launch defaults are collection-off. On successful dependency completion the provider sets `AnalyticsStorage` granted, configures `AdStorage`, `AdUserData` and `AdPersonalization`, then enables collection, before draining held work. Completion returns to the Unity main thread; all provider boundaries and SDK completion are no-throw.

The only config key is **`analytics.firebase.adSignals`**: absent ⇒ ad signals granted; numeric **0** or string **"0"** ⇒ every ad signal denied; other values ⇒ granted. It is read as `ConfigKey<object>` once at startup so absence differs from numeric zero and exact-typed `GameConfigCore` values retain their declared type. The SKU may use a numeric default/override (`1` / `0`) or a string default/override (`"1"` / `"0"`). `AnalyticsStorage` remains granted while Analytics consent permits startup. The module does not query personalized-ad consent or a CMP; the SKU sets this switch for its region policy before first work. Regrant uses the startup setting.

During startup, events, properties and accepted user-id commands are snapshotted in a FIFO memory queue of at most **128 entries**. Overflow refuses the newest work and logs each refusal; a refused command returns false. There is no persistence. Stalled initialization has no timeout; the pending queue remains bounded until completion or withdrawal. Withdrawal disables native collection and drops all held work, including when initialization completes later. Regrant enables an already-started SDK without replaying discarded work. Completion checks current Analytics consent again. A failed start/configuration latches failure for the provider lifetime, logs the result and drops held work; no implicit retries. Restart the app after fixing the dependency/configuration. An individual forwarding failure is logged/dropped without retry.

## Commands

Call `IAnalyticsCommands` from SKU Application/Composition services, on Unity's main thread. Do not reference a module or SDK assembly from gameplay.

| Provider | Command | Args | Result |
|---|---|---|---|
| `analytics-firebase` | `set_user_id` | Exactly one ordinal `id`, nonblank, at most 256 UTF-16 code units | Requires granted Analytics. True means applied or queued until startup. Duplicate/unknown keys, missing/empty id, extra args and oversized ids return false. |
| `analytics-firebase` | `privacy.forget_me` | None (null or empty list) | Bypasses Analytics gate; accepted only after SDK startup completes. Clears explicit user id and calls ResetAnalyticsData. Before/during startup returns false and never starts or queues an SDK operation. |

Every other command, including unknown `privacy.*`, returns false. Privacy reset clears **local analytics data and resets the app instance id**, as documented by [Firebase's API](https://firebase.google.com/docs/reference/unity/class/firebase/analytics/firebase-analytics#resetanalyticsdata). It does not delete server-side historical events, the player's account, purchases or non-Firebase data. It does not withdraw framework consent or change collection settings. A player who wants analytics off must also withdraw Analytics consent. Server-side deletion requires the product's separate account/privacy process.

## Safe native build defaults

Permitted post-build transforms run at explicit order **10000**: Android Gradle manifests (unityLibrary and existing launcher) receive application meta-data `firebase_analytics_collection_enabled=false`; iOS exported Info.plist receives boolean `FIREBASE_ANALYTICS_COLLECTION_ENABLED=NO`. Existing matching values are kept byte-for-byte. Android merge removal directives targeting protected collection metadata and malformed plist booleans are refused. Conflicting/duplicate/unknown values are preserved and diagnosed with module, key, existing and required value; the transform fails the build rather than shipping the conflict. Transform rules are engine-free and idempotent. No build preprocessor is added.

The framework's aggregated preflight ignores Unity-excluded backups (hidden, `~`, `.tmp`, `CVS` paths) and unrelated array/binary plist resources. It selects imported Android manifests, iOS `Info.plist` and custom plists actually containing Firebase collection keys. It refuses an in-play module with missing SDK define, missing platform service file, Android minSdk <24 or explicit collection-on/ambiguous flags in project Android manifests / iOS plist sources. Do not use `firebase_analytics_collection_deactivated=true` / `FIREBASE_ANALYTICS_COLLECTION_DEACTIVATED=YES`: permanent deactivation prevents later enabling collection. Missing collection flags are safe to leave absent in source because the export transform supplies defaults. Use literal booleans; resource-backed flags and malformed native XML require manual resolution. Existing different settings are never silently overwritten.

Firebase persists collection choices across app runs; native launch defaults and dependency setup alone do not prove behavior for an already-installed user with historical collection enabled. Test fresh installs, relaunch, withdrawal, and upgrades on each platform; review the final merged manifest / exported Info.plist and any other SDK that can change consent/collection. See [Firebase collection settings](https://firebase.google.com/docs/analytics/configure-data-collection).

## Verification limits

`Tests~/AnalyticsFirebaseHeadlessTests` source-links framework Domain/Application, this Core and the Null bridge, with overridable `PfFrameworkPackage`. These tests exercise primitive forwarding, revenue mapping, queue snapshots/overflow, completion dispatch, withdrawal, command refusal/privacy, limits, preflight and XML transformations. Shape tests inspect installer/SDK/Editor source and metadata; they do not compile a fake vendor SDK.

SDK-enabled compilation, EDM4U resolution, real Android/iOS exports, native launch collection-off, persistence across upgrade/relaunch, dashboard delivery and reset behavior are **device / SDK-runner owned and unverified** until exercised with Firebase 14.1.0. Headless passes do not certify them. Local Unity batchmode and PlayMode suites are never run.
