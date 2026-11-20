# com.classicspins.pf-ads-max

An optional **service module**: the AppLovin MAX plugin behind the framework's unchanged
`IAdsService` port. A SKU that wants real ads installs this package; every other SKU keeps the
framework's `FakeAdsService` and never sees the dependency.

`pf-module.json` is the single source of truth for what the module needs. Install it from
**`Framework/Setup Wizard ▸ Modules`**, which writes the registries and the pinned MAX package into
`Packages/manifest.json` and then prints the human-zone checklist (`manualSteps`).

## Layout — four roles (AD-MOD-1)

| Role | Folder | Assembly | Holds |
|---|---|---|---|
| Core | `Runtime/Core/` | `ClassicSpins.PrototypeFramework.Ads.Max.Core` — engine-free, ungated | `IMaxBridge`, `MaxRevenue`, `MaxAdUnits`, `MaxAdsService`, `MaxRevenueReporter`, `MaxAdsInitNode`, `MaxAdsPrivacyOptions`, `MaxAdsPreflightRules` |
| Sdk | `Runtime/Sdk/` | `ClassicSpins.PrototypeFramework.Ads.Max.Sdk` — gated on `PF_MAX` | `MaxBridge` (the only file naming a MAX type), the `PfModuleMarker_PF_MAX` marker |
| runtime | `Runtime/` | `ClassicSpins.PrototypeFramework.Ads.Max` — ungated | `MaxAdsInstaller`, `NullMaxBridge` |
| Editor | `Editor/` | `ClassicSpins.PrototypeFramework.Ads.Max.Editor` — editor-only, ungated (`#if PF_MAX` inside) | `MaxAdsPreflight : IModulePreflight` (fact gathering only) |

`PF_MAX` is armed by a `versionDefines` entry on `com.applovin.mediation.ads` (expression `8.0.0`)
in the `Sdk`, runtime and Editor asmdefs — never a Player Settings symbol. The MAX package must arrive through
UPM (≥ 8.0.0): only its UPM distribution ships the `MaxSdk.Scripts` asmdef an assembly can reference.

## Absence refuses — it never falls back to the fake

The installer is **not** define-gated. It always registers `MaxAdsService` as `IAdsService` and
replaces the framework's `AdsInit` node. Only the bridge behind them depends on `PF_MAX`:

- **MAX resolved** ⇒ `MaxBridge`, a thin shell over the plugin's static API.
- **MAX absent** (or older than 8.0.0) ⇒ `NullMaxBridge`: every show resolves `Failed`, `AdsInit`
  degrades (`AdsReady` is still emitted), and each refusal logs a warning naming `pf-ads-max`.

## Behaviour

- **Banner.** `MaxBannerAds` implements the framework's `IBannerAds` port: one bottom-centre banner per
  placement, unit id from `ads.max.<platform>.<placement>.unitId` → `ads.max.<placement>.unitId` →
  `ads.max.<platform>.banner.unitId` → `ads.max.banner.unitId`, auto-refresh by MAX, revenue through
  `OnRevenuePaid`. A wish made before init is applied on the bridge's `OnInitialized`. It takes no slot in
  `MaxAdVisibility` yet (no `BannerVisible` / insets). Every apply asks `IAdGate.CanShow`, so a remove-ads
  owner is never shown a banner; after a purchase the game calls `Show`/`Hide` again to take a banner down.
  Mark each banner placement `ads.<placement>.banner = 1` in the bundled config so the build preflight
  checks the banner keys (never the interstitial default) for it.
- **Formats.** Interstitial and rewarded through `IAdsService`. A placement is rewarded when
  `ads.<placement>.rewarded` is non-zero (the framework's own ad-gate convention; absent ⇒
  interstitial).
- **Unit lookup** (`MaxAdUnits`, read through `IGameConfig`). For placement `P`, platform `X`
  (`android` / `ios`, mapped by the installer from `UNITY_ANDROID` / `UNITY_IOS`) and format `F`
  (`rewarded` / `interstitial`): `ads.max.X.P.unitId` → `ads.max.P.unitId` → `ads.max.X.F.unitId` →
  `ads.max.F.unitId`; the first non-empty value wins. With no platform (the Editor on a desktop target)
  the `X` tiers are skipped. The pre-v2 keys (`ads.max.P.unitId`, `ads.P.rewarded`) keep working
  unchanged — one format default per format now covers every placement that has no key of its own.
- **Requested units, auto-load, retry.** After a successful init the format defaults load on their own,
  plus anything requested before init. A placement's own unit loads once it is requested: `Preload(P)`,
  or the first `IsReadyAsync(P)` / `ShowAsync(P)`, which then answer not-ready / `Failed` (call
  `Preload` early so the first show finds an ad). A failed load of a requested unit, while the SDK is
  initialized, retries after `2^min(attempt, 6)` s (2, 4, …, 64, 64 — attempt counted from 1); a
  successful load resets the count; at most one retry is pending per unit. Every show reloads its unit
  afterwards. Delays are realtime (they keep counting while the game pauses `timeScale` under an ad).
- **Outcomes.** A rewarded unit resolves `Rewarded` when the reward arrives and `Skipped` when it is
  dismissed first; an interstitial resolves `Shown`. Display failure, a display timeout, a bridge that
  throws, an uninitialized SDK, no unit id on any tier, an unrequested or not-loaded unit (a load is then
  requested) or an already-cancelled token resolve `Failed`. `ShowAsync` never throws; once the ad is up
  the token is ignored. The placement name is passed to MAX with the show.
- **Display timeout.** `ads.max.displayTimeoutSeconds` (int; absent or ≤ 0 ⇒ 10) bounds only the gap
  between `Show` and MAX's displayed / display-failed callback; no signal in time ⇒ `Failed`, the
  visibility pair closes, and later callbacks for that show are ignored. Once displayed there is no
  timeout. **Known gaps:** a Hidden that never arrives after Displayed means the show never returns;
  a timed-out show the SDK displays later covers the screen with no visibility fact — and if that late
  show is a rewarded ad, the game gets no reward for it: the show already returned `Failed` and its
  per-show callback handlers were already unsubscribed, so a late `OnRewarded` has nothing left to
  resolve. Its visibility slot is already closed too, so the one-fullscreen refusal does not see the
  late show and a second show can stack on top of it.
- **One visibility state.** `AdVisibilityChanged` comes from one place: `MaxAdVisibility`, a Core
  singleton with one slot per surface: the show, the consent flow around SDK init, and the
  privacy-options window. The show opens its slot at show-start and closes it in a `finally`.
  `MaxAdsInitNode` does the same around SDK init, and `MaxAdsPrivacyOptions` around MAX's re-shown
  consent form. The fact is derived
  (`FullscreenAdVisible` = any slot open, no banner) and published only when it changes, so the input
  lock, audio duck and tick gate hold until the **last** surface closes, never the first. **One
  fullscreen surface at a time:** a `ShowAsync` while another show, the consent flow or the
  privacy-options form is open is refused (`Failed`, a warning, auto-logged as `AdFailed`) before any SDK call, and publishes nothing,
  like every refusal before the show.
- **Readiness push (`AdReadinessChanged`).** `MaxAdReadiness`, one Core singleton, binds each placement
  to its unit on `Preload` / `IsReadyAsync` / `ShowAsync` — before SDK init too — and owns the one
  `ready(unit)`: initialized ∧ requested ∧ not on screen ∧ MAX's loaded read. `IsReadyAsync` is that
  read (taken before any load it requests) and publishes nothing. A placement's baseline is never
  published; a load, load failure, show-start, show-end or config change (which re-resolves every
  placement's unit) publishes `AdReadinessChanged(placement, ready)` only where the value changed, for
  every placement bound to that unit (two placements sharing one unit both dim while either is shown).
  When SDK init completes, every bound placement gets its settled value — `false` after a failed init.
  Subscribe first, then call `IsReadyAsync` once; offer only while the latest `Ready` and
  `IAdGate.CanShow` agree. Readiness across MAX's silent reload of an expired ad is device-only and
  unverified.
- **Analytics.** Every returned result is auto-logged through `AdAnalyticsMapping` → `IAnalytics`,
  unless `ads.max.autoLog` is `0` (absent ⇒ on).
- **Ad revenue.** Every paid interstitial or rewarded impression MAX reports (`OnAdRevenuePaidEvent`)
  is tracked through `IAnalytics.Track` as exactly one framework `AdRevenuePaid` by
  `MaxRevenueReporter`: `Mediator = "applovin_max"`, `Currency = "USD"`, `AdFormat` normalized to
  `rewarded`·`interstitial`·`banner`·`mrec`·`app_open` (an unknown label falls back to the format of
  the callback that fired), `Precision` to `exact`·`estimated`·`publisher_defined`·`undefined`
  (unknown ⇒ `undefined`), `Placement` = MAX's echo of the show placement or `""` (never looked up from
  a unit binding), unit id and network verbatim. A negative revenue (MAX's missing `-1`) or a
  non-finite one is dropped with a warning; `0` is kept. The record is emitted whatever the consent
  state — the analytics sink applies consent. `ads.max.revenueLog` (int; absent ⇒ on, `0` ⇒ off),
  read at callback time, switches it independently of `ads.max.autoLog`. Money is counted only from
  this record, never from `AdShown`/`AdRewarded`. The bridge sets
  `MaxSdkBase.InvokeEventsOnUnityMainThread = true` before init so callbacks arrive on the main thread;
  Core never marshals, and logs one warning if a callback arrives off the thread it was built on. A
  throwing `Track` or config read is a warning, never a throw into MAX's callback.
- **Dev config.** `ads.max.verboseLogging` (int, non-zero ⇒ on, `0` ⇒ off) and `ads.max.testDeviceIds`
  (comma-separated advertising ids, trimmed, empties dropped) are applied once, before SDK init; an
  absent key (or an empty id list) makes no SDK call.
- **Developer tools.** MAX's Mediation and Creative Debuggers are registered as the framework's
  `IDevTool` — ids `ads-max.mediation-debugger` and `ads-max.creative-debugger`, one type each
  (`MaxMediationDebuggerTool`, `MaxCreativeDebuggerTool`). A tool is available only once the SDK has
  initialized and while `ads.max.devTools` (int; absent ⇒ on, `0` ⇒ off) is not `0`, both read on every
  call; a throwing config read means off. `Open()` returns `false` with one warning when unavailable or
  when the SDK call throws, and never throws. The SKU lists `IReadOnlyList<IDevTool>`, maps each id to a
  `LocKey` and decides which builds expose the menu — no reflection on MAX's types.
- **Privacy settings (`IAdsPrivacyOptions`).** `MaxAdsPrivacyOptions` replaces the framework's
  `NullAdsPrivacyOptions` (declared `replacement` in `pf-module.json` `ports[]`) and re-shows MAX's own
  consent form (its CMP — Google UMP where it applies) to an existing user. `IsAvailable` = the SDK has
  initialized ∧ MAX reports a supported CMP, both read on every call (a throwing read is a warning and
  false). `ShowAsync` never throws; it refuses — `false`, one warning naming `pf-ads-max`, no SDK call,
  no publish — on a cancelled token, a call still running (MAX keeps one completion callback), any open
  fullscreen slot, unavailability, or a refused exclusive slot. Otherwise it opens its own
  `PrivacyOptions` slot (so `AdVisibilityChanged(true)` holds input, audio and the tick gate, and every
  ad show is refused), calls MAX once, resolves `true` when MAX completes with no error and `false`
  with a warning naming MAX's error otherwise, and closes the slot in a `finally`. The token is ignored
  once the form is up and nothing bounds it. MAX resets the current consent before it shows the form;
  the outcome is MAX's and nothing is written back. The SKU consumes the port behind its own interface,
  draws its settings row only while `IsAvailable`, and never re-registers it. The in-editor stub reports
  no supported CMP, so the row never draws in the Editor.
- **Can-show** keys on the bridge's own initialized state, never on boot status.

## Consent is MAX's

MAX's Terms and Privacy Policy flow (Google UMP in GDPR regions, the iOS ATT prompt when configured)
runs **inside SDK init**. `MaxAdsInitNode` reads no consent purpose from the framework, passes no
consent flag to MAX and writes MAX's outcome back nowhere. It still requires `ConsentResolved`, so
MAX's prompt follows the SKU's own agree gate. The flow is configured in AppLovin's Integration
Manager (`AppLovinInternalSettings`), which is one of the checklist steps.

- **One consent UI per region.** In GDPR regions MAX's form is the only consent form, for any purpose;
  the SKU draws none beside it.
- **ATT is raised only by MAX's flow**, never by SKU code.
- **The SKU declares `FrameworkSettings.ServesNoConsentRegion`** (default `false`, under which first-boot
  consent resolves with nothing granted; `true` grants `Analytics`). `PersonalizedAds` is never granted
  by the framework — MAX owns it.
- **"Change privacy settings" is an EEA/UK launch precondition.** The module fills `IAdsPrivacyOptions`;
  a SKU that serves the EEA or the UK draws the row. Review-held — no gate checks it.

`MaxAdsInitNode` keeps `Id = "AdsInit"`, `Provides = {AdsReady}` and `Required = false`, carries **no
timeout** (the init await may be a player reading a privacy prompt), holds its consent-flow
visibility slot around the init (opened before, closed in a `finally`; never refused), applies the dev config before it and starts the
auto-load after a successful one, and runs SDK init at most once per process: a
rerun — including `RerunFromAsync(ConsentResolved)` — returns the memoized outcome. So a failed MAX
init disables ads for the rest of the process: every rerun returns the memoized failure, and ads come
back only on the next launch.

## The build refuses a half-wired install

The module ships **no** build hook. Its Editor role implements the framework's `IModulePreflight`, and
the framework's one preprocessor (`ModulePreflightBuildHook`, `callbackOrder` −1000 — ahead of the
Android resolver's scene post-process and of MAX's own preprocessors) runs it for the build target. Any
problem fails the build with one `BuildFailedException` listing every problem from every module, each
line prefixed `[com.classicspins.pf-ads-max]`. In an interactive editor a dialog first offers a fix
action per problem and re-checks; under `-batchmode` only the exception exists.

The decisions live in the engine-free `MaxAdsPreflightRules` (headless-tested row by row);
`MaxAdsPreflight` only gathers facts — it never touches MAX's create-on-miss settings singletons, and it
reaches the Android resolver by reflection only.

| Code | Target | Fires when | Fix action |
|---|---|---|---|
| `define-without-package` | every | `PF_MAX` armed, `com.applovin.mediation.ads` not resolved | Package Manager |
| `package-without-define` | every | package resolved (8.x, or a pre-UPM 7.x), `PF_MAX` not armed | Package Manager |
| `installer-without-define` | every | `MaxAdsInstaller` slotted into an enabled build scene, `PF_MAX` not armed | ping the installer |
| `sdk-key-empty` | Android, iOS | package resolved, armed, `AppLovinSettings` SDK key empty (skipped when unarmed — unknowable) | Integration Manager |
| `consent-flow-disabled` / `consent-privacy-url` | Android, iOS | MAX's consent flow off, or on with an empty privacy-policy URL (`AppLovinInternalSettings.json`) | Integration Manager |
| `unit-id` | Android, iOS | a placement in `game.adplacements.json` has no non-empty unit on **any** tier of the runtime lookup (platform = the build target, format from the bundled `ads.<placement>.rewarded`) in the **bundled** config defaults the slotted installers register (one per placement; remote-only values count as missing) | — |
| `edm4u-missing` | Android | `GooglePlayServices.PlayServicesResolver` not loaded | Package Manager |
| `android-unresolved` | Android | `ProjectSettings/AndroidResolverDependencies.xml` absent, or lacks `com.applovin:applovin-sdk:` | resolve (`ResolveSync(true)`, by reflection) |
| `gradle-deps-token` | Android | `Assets/Plugins/Android/mainTemplate.gradle` exists without `**DEPS**` | open the template |
| `adapter-count` | Android | installed `com.applovin.mediation.adapters.*.android` count ≠ resolved `com.applovin.mediation:*-adapter:*` count | resolve |

When the installer is slotted for Android or iOS and the project has no `game.adplacements.json`, the
`unit-id` rule has nothing to check; the Editor logs a `[pf-ads-max]` warning instead (never a build
failure).

When the package is unresolved, `PF_MAX` is unarmed and no installer is slotted, the preflight reports
nothing (this repo, SKU #0). Whether the resolver auto-resolves under `-batchmode` on the CI runner is
still undecided; `android-unresolved` is what surfaces the answer on the first CI build either way.

**Shipping before every unit exists.** Set `ads.max.allowMissingUnitIds = 1` (int) in the bundled config
defaults and a placement with no unit id is logged as a build warning instead of failing the build. At
runtime such a placement is never ready: an interstitial is skipped, a rewarded show resolves `Failed`, a
banner is not shown. It waives `unit-id` only; remove the flag once every unit id is filled.

## Tests

`Tests~/AdsMaxHeadlessTests` compiles the framework's Domain + Application sources, `Runtime/Core/**`
and `Runtime/NullMaxBridge.cs` under plain `dotnet test` — never `Runtime/Sdk/` or `Editor/` (those are
held by source scan). CI runs it at step
**5c** through `Tools/CI/module-tests.sh`. In a consumer project, point it at the resolved framework
package: `dotnet test <module>/Tests~/AdsMaxHeadlessTests -p:PfFrameworkPackage=<framework root>`.

## Not verified here

What only a device in a consumer project can show — a real rewarded and interstitial watch, the order
of MAX's native reward/hidden callbacks, and MAX's consent flow on screen — is not proven by this
repo. The in-editor MAX stub shows fake ads and proves nothing about a device.

Not shipped: banner insets / `BannerAwareArea` reflow, MREC, app-open, writing consent back. Not verified here: the real order of
MAX's loaded / displayed / hidden callbacks on a device, which the retry and the display timeout rely
on; main-thread delivery of MAX's callbacks under `InvokeEventsOnUnityMainThread`; the literal
`AdInfo.AdFormat` / `RevenuePrecision` labels a device reports; dashboard USD totals; and, for privacy
options, how many prompts a forced-GDPR device shows, MAX's CMP re-prompt on Android 14+ / iOS 17+, and
main-thread delivery of the CMP completion callback.
