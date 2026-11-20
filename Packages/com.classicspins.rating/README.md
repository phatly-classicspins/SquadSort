# ClassicSpins Rating (`com.classicspins.rating`)

A "rate this game" prompt you can drop into any Unity game. It is the rating flow of the Money Sort prototype
(`RatingUI` + `FlickSortGameplayUIController.TryShowRating`), made game-independent. It does not depend on
PrototypeFramework.

## What the player sees

1. They win the prompt level (15 by default) or any later level. This happens **once per install**.
2. A popup asks *"How was your experience with the game so far?"* and shows five stars. *Submit* stays off until a
   star is picked. The ✕ closes the popup.
3. **4–5 stars** opens the store's *write a review* page: the App Store (`itms-apps://…?action=write-review`) or
   Google Play (`market://details?id=…`). **1–3 stars** opens your feedback link.
4. When the player comes back to the game, a thank-you shows. The text differs for many and few stars.

The offer is saved **before** the popup shows, so closing or killing the app never brings it back.

## How a prompt can end

| How it ended | What the game should do (Money Sort does this) |
|---|---|
| `NotShown` | The normal level end. |
| `Closed` | The player closed the popup without rating. Play the level-end interstitial. |
| `Rated` | No interstitial on this level end, because the player has just been to the store. |

**When the thank-you shows without the player leaving the game:**

- No page to open: the store id is missing, there is no feedback URL, or the build is for a platform other than iOS
  or Android.
- The game did not lose focus within `LeaveTimeoutSeconds` (3 s) of opening the page. Money Sort waited forever here.

## Install

**Embedded:** copy `Packages/com.classicspins.rating/` into the other game's `Packages/` folder.

**From git:** add this line to `Packages/manifest.json`:

```json
"com.classicspins.rating": "https://github.com/FoxCubGames/cardslots_prototype.git?path=/Packages/com.classicspins.rating"
```

The package has two assemblies:

- **`ClassicSpins.Rating`** is the engine-free flow: `RatingPrompter`, `RatingSettings`, `RatingStoreLinks` and the
  `IRatingPopup` / `IRatingState` / `IRatingHost` ports. You can test it with plain `dotnet test`.
- **`ClassicSpins.Rating.Unity`** is the engine side:
  - `RatingKit`, the one-line set-up.
  - `RatingPopup`, the default popup. It is built in code and needs no prefab, TMP or art. It needs an `EventSystem`.
  - `UnityRatingHost`, which picks the store from the build target and opens the page.
  - `PlayerPrefsRatingState`.
  - `RatingAppFocus`, the pause/focus relay.

## Use: the quick way (a game with no UI system to plug in)

```csharp
using ClassicSpins.Rating;

// once, at start-up
var rating = RatingKit.Create(
    new RatingSettings { PromptAfterLevel = 15, IosAppId = "6804992035", FeedbackUrl = "https://help.example.com/…" },
    new RatingPopupStyle { Font = myFont, Header = myBrandColour });   // both optional

// after every win, before the level-end screens
var outcome = await rating.TryPromptAsync(completedLevel, cancellationToken);
if (outcome.Result == RatingResult.Closed) ShowInterstitial();
else if (outcome.Result == RatingResult.NotShown) MaybeShowInterstitial();
```

`RatingPopup.HandleBack()` closes the popup. Call it from the game's Android back-button handling.

## Use: your own dialog, save and lifecycle (for example, PrototypeFramework)

Build the prompter yourself:

```csharp
var prompter = new RatingPrompter(settings, myState, myPopup, new UnityRatingHost());
```

- **`IRatingPopup`**: `AskAsync` returns the number of stars, or 0 if the player closed the popup. `ThanksAsync` returns
  when the player closes the thank-you. Draw both with the game's own dialog, text and tokens.
- **`IRatingState`**: store the one-shot flag in the game's save. `MarkPromptShown` returns false when the save fails,
  and the prompt is then skipped.
- **Focus**: call `prompter.OnAppFocusChanged(bool)` from the game's own pause/focus hook. Do **not** create
  `RatingAppFocus` in a game that owns those messages. PrototypeFramework bans a second `OnApplicationPause`/`Focus`.
  Register an `IAppPhaseHandler` instead.

CardSlot does it this way. See `Assets/CardSlot/Features/RatingDialog/Presentation/CardSlotRating.cs` and `docs/workflow/DECISIONS.md` D-056.

## Settings

| Field | Default | Meaning |
|---|---|---|
| `PromptAfterLevel` | 15 | The prompt shows after a win of this level or any later level. 0 turns the prompt off. |
| `StoreMinStars` | 4 | Ratings with this many stars or more go to the store. Lower ratings go to the feedback page. |
| `IosAppId` | "" | The App Store id, in digits. |
| `AndroidPackage` | "" | The Play package. When empty, `Application.identifier` is used. |
| `FeedbackUrl` | "" | The page for low ratings. When empty, the player sees only the thank-you. |
| `LeaveTimeoutSeconds` | 3 | How long to wait for the game to lose focus after opening the page. |

**Note:** Money Sort only prompts on exactly level 15. This package uses "level 15 or any later level", so players
who are already past level 15 when the prompt ships still get it once.

## Not included

The native in-app review sheets (Apple `SKStoreReviewController`, Google Play In-App Review) are not used. Those sheets
need a native plugin or SDK, and they hide whether the player actually rated. The package opens the store page, as
Money Sort does. To use a native sheet instead, implement `IRatingHost.OpenUrl` (or your own flow) on top of that SDK.
