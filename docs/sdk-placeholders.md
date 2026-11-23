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

| Rating | iOS App Store id empty (it was Chipsaway's `6827467015`); feedback URL `https://brainbucks.com/contact` | `RatingIosAppId` / `RatingFeedbackUrl` in `Assets/SqadSort/Features/Meta/Application/EconomyTuning.cs`, and `rating_ios_app_id` / `rating_feedback_url` in `Tools/Firebase/remote-config.template.json` | SqadSort's App Store id (digits only) and the support page for 1–3 star ratings. While the id is empty, a 4–5 star rating on iOS just thanks the player; Android uses the build's own package |

## Turning the real SDKs on

The six module installers exist in `Assets/SqadSort/Installers/` (MAX, Unity IAP, Adjust, Firebase Analytics /
Crashlytics / Remote Config) but are **not slotted** — `Scenes/Master.unity` ▸ `RootLifetimeScope` ▸ *Game Installers*
holds only `SqadSortFrameworkSettings`. Until they are, every build runs on the framework's fakes: the fake ads service
is always ready and **grants every reward without an ad**, and the fake store **grants every purchase for free**. Fine
for development, never for a store build.

Once the ids above are real:

1. Add the six installer assets to *Game Installers*, after `SqadSortFrameworkSettings` (CardSlot's order:
   Remote Config, Firebase Analytics, Crashlytics, Adjust, MAX, IAP).
2. In the Editor MAX then answers "no ad" unless the AppLovin editor stub serves one, and IAP goes through Unity's fake
   store — test the reward flows on a device with MAX test ads.
3. Build once for Android and iOS; the module preflight must stay clean with `AllowMissingUnitIds = 0`.

### Where ads already show (no code change needed)

| Placement | When | Gate |
|---|---|---|
| `interstitial_level_end` | After a win | From level `InterstitialFirstLevel` (12), every `InterstitialEveryNWins` (1) win, at least `InterstitialMinIntervalSeconds` (180 s) apart; never for Remove Ads owners, never right after a rewarded ad |
| `interstitial_level_fail` | After a loss | Same pacing and Remove Ads rule |
| `banner_gameplay` | Bottom of the gameplay screen | From level `BannerFirstLevel` (3); hidden for Remove Ads owners (also right after buying it) |
| `rewarded_win_double` | Win screen "×N" button | Multiplier from the needle wheel |
| `rewarded_revive` | Out of space ("Play on" ▸ Free) | `ReviveMaxAdsPerAttempt` (1) per attempt |
| `rewarded_booster` | Booster buy dialog "▶ Free" | `*AdAmount` (1) of that booster |
| `rewarded_life` | Out of lives ▸ ad refill | `LivesRefillAdAmount` (1) life |
| `rewarded_skip_level` | Skip Level offer | From the `SkipLevelFailCount` (3)rd loss in a row, from level 5, `SkipLevelMaxPerDay` (3) |
| `rewarded_gold_brick` | Gold Brick / Limited Time Reward popup | From level 10, every 4 h |

Remove Ads (`squad.sort.remove_ads`, non-consumable, +1500 coins) is sold in the Shop and offered by the Remove Ads
popup from level `RemoveAdsUnlockLevel` (15), counted by interstitials shown; the No Ads popup confirms the purchase.
All numbers are remote config (`EconomyTuning`), so they can be tuned without a build.

Also still to decide:

- **Bundle id.** Player Settings still carry Unity's template ids (`com.UnityTechnologies.com.unity.template.urpblank`
  on Android). Pick SqadSort's own before registering the app anywhere — the MAX, Adjust and Firebase apps, and
  both Firebase files, are all keyed to it, and the Firebase preflight checks that the files match it.
- **Store products.** `Assets/SqadSort/Resources/Config/products.json` ids (`squad.sort.*`) must match the products
  created on Google Play / App Store.
- **Unity IAP.** Link the project to a Unity Cloud project (Edit ▸ Project Settings ▸ Services).

Once everything above is real, run the module preflight again (a build, or Framework ▸ Doctor) — it should stay
clean with `AllowMissingUnitIds = 0`.
