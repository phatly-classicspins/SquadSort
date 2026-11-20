using UnityEngine;

namespace Game.Views
{
    /// <summary>
    /// The SKU's single source of visual truth (rule #17, G21) — a transcription of
    /// <c>docs/design/design-tokens.md</c> v1.0 (direction B · Warm Toybox, D-017) and the motion tokens of
    /// <c>docs/design/animation-list.md</c> (D-022). Views index it; nothing redeclares a colour, size or
    /// duration. Units: native Figma UI uses the 720×1280 rig; older layouts retain their 1080-unit compatibility root.
    /// </summary>
    public static class DesignTokens
    {
        // Popup-Booster mockups: native 720px, Lilita One, warm brown title stroke.
        public static class BoosterUnlock
        {
            public static readonly Color TitleFace = Hex("FFFBEF");
            public static readonly Color TitleStroke = Hex("8A3A12");
            public static readonly Color Description = Hex("88380C");
            public static readonly Color ActionStroke = Hex("078800");
            public const float TitleSize = 60f, BodySize = 39.35f, ActionSize = 48f;
            public const float OutlineWidth = 0.221f;
        }
        public static Color Hex(string rrggbb, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString("#" + rrggbb, out var c)) c = Color.magenta;
            c.a = alpha;
            return c;
        }

        // Native Figma Win gradients, sampled from the text properties panel.
        public static readonly Color FigmaIce = Hex("B1EAFF"), FigmaWarm = Hex("FFF3B1");

        // ── Palette (§1) ─────────────────────────────────────────────────────────────────────────────
        public static readonly Color GroundTop = Hex("FFF6E6");
        public static readonly Color GroundBottom = Hex("FFD9A8");
        public static readonly Color Surface = Hex("FFF8EC");
        public static readonly Color SurfaceSunken = Hex("F2C68C");
        public static readonly Color TrayRim = Hex("B9733A");
        public static readonly Color TrayInner = Hex("F6CF98");
        // CR-007: the target pegs (a warm grey-brown that reads against every card colour)
        public static readonly Color PegPole = Hex("8E7B6C");
        public static readonly Color Rail = Hex("C98B52");
        public static readonly Color Ink = Hex("5A3415");
        public static readonly Color InkSoft = Hex("9A6A3F");
        public static readonly Color Primary = Hex("21B8A6");
        public static readonly Color PrimaryEdge = Hex("138A7B");
        public static readonly Color Secondary = Hex("E8613C");
        public static readonly Color SecondaryEdge = Hex("B7411F");
        public static readonly Color Danger = Hex("E2604A");
        /// <summary>D-138: the low-slot capsules' blink (blinks per second) and their alpha range (the art is #FF0000 at 37%).</summary>
        public const float SlotWarnRate = 1.6f, SlotWarnAlphaLow = 0.25f;
        /// <summary>D-138: the RV Slot bubble — a cream card with a red rim, dark text, like the tutorial panels.</summary>
        public static readonly Color NudgeFill = Hex("FFF6E2");
        public static readonly Color NudgeRim = Hex("E0402A");
        public static readonly Color NudgeText = Hex("7A2E10");
        public static readonly Color DangerText = Hex("D2402A");
        public static readonly Color Disabled = Hex("CDBFAE");
        public static readonly Color Scrim = Hex("462308", PopupDim);       // D-127: booster pick mode / first-use guides, as dark as the popups
        /// <summary>D-126 / D-127: every dim — popup backdrops (prefabs) and tutorials — sits at 245/255 (Ethan / Nhu 12-11).</summary>
        public const float PopupDim = 245f / 255f;
        public static readonly Color TutorialScrim = Hex("2A1405", PopupDim);   // D-127 (was 0.75)   // Ethan 11-11: darker (was 462308 at 0.45; blending in linear space reads lighter than the alpha says)
        public static readonly Color TutorialSpot = Hex("FFE6CC", 0f);   // the lit spot on a new element: fully clear (Phat 11-11: the 0.38 cream wash of mock-up Popup-Element2 made the target look faded)
        // booster helper (Figma WF/Flow-Booster): the Hand / Magnet hints and the soft shade behind the banner
        public static readonly Color BoosterHintStack = Hex("88380C");
        public static readonly Color BoosterHintPole = Hex("374BC9");
        public static readonly Color BoosterBannerGlow = Hex("000000", 0.51f);
        // the warm burst of light behind the Shuffle / Magnet icon as it flies across the banner (GDD 3.4, animRef/shuffle)
        public static readonly Color BoosterBannerRays = Hex("FFE58A", 0.9f);
        // CR-012 D elements (mock-ups *-v03, D-037): a face-down card is a real card back, never grey; a hidden pole is
        // warm stone; a locked stack sits in clear ice with its counter on a deep-ice badge
        public static readonly Color CardBack = Hex("D9A86C");
        public static readonly Color CardBackPattern = Hex("B9834A");
        public static readonly Color CardBackEdge = Hex("F6E3C4");
        public static readonly Color PoleHidden = Hex("9C8F84");
        public static readonly Color Ice = Hex("BFEAF7", 0.45f);
        public static readonly Color IceDeep = Hex("2E8FB8");
        public static readonly Color Smoke = Hex("FFF3E0", 0.85f);
        public static readonly Color SmokeSparkle = Hex("FFF299");   // the warm half of the reveal sparkles (MoneySort HiddenRevealBurst)
        public static readonly Color SmokePoleReveal = Hex("9A979E");   // a hidden pole's reveal cloud: thick grey smoke, not black (Phat 10-11)
        // CR-012 E lives (mock-ups *-v04, D-039): the heart sprite carries both; the count on it is white text
        public static readonly Color Heart = Hex("F0506E");
        public static readonly Color HeartEdge = Hex("C2334F");
        public static readonly Color OnColor = Color.white;
        /// <summary>D-067: Money Sort's brown text on its cream shop panels (Lilita One, no outline) — the copied shop art only.</summary>
        public static readonly Color MsText = Hex("742F08");
        /// <summary>D-067 Shop Animation (Money Sort PriceButtonShine / ShopCardSparkles): the streak over a buy button, the card twinkles.</summary>
        public static readonly Color MsShine = new Color(1f, 1f, 1f, 0.75f), MsSparkle = new Color(1f, 0.97f, 0.8f, 1f);
        /// <summary>D-067: the solid fill behind the Store's cards (Money Sort ShopUI "Panel", no sprite).</summary>
        public static readonly Color MsShopPanel = Hex("C54812");
        /// <summary>D-069: the countdown under the Home Starter Pack icon (Money Sort HomeUI "TimerText").</summary>
        public static readonly Color MsTimer = Hex("FFEF48");
        /// <summary>D-076: the bottom colour of Money Sort's TMP gradient presets on its level complete screen (top is white): "Figma Gold"
        /// (Reward, the coin amount), "Figma Warm White" (button labels), "Figma Ice" (the progress count and New Feature tag).</summary>
        public static readonly Color MsGold = Hex("FDF492"), MsWarmWhite = Hex("FFF3B1"), MsIce = Hex("B1EAFF");
        /// <summary>Money Sort's "Booster Unlock Brown" TMP gradient (top, bottom): the tip on the unlock popups.</summary>
        public static readonly Color MsBrownTop = Hex("7A3611"), MsBrownBottom = Hex("692B0A");
        /// <summary>Money Sort's SettingsUI toggle labels (SFX / Music / Vibration): plain Lilita One, no outline.</summary>
        public static readonly Color MsSettingsLabel = Hex("88380C");
        /// <summary>Money Sort's ErrorUI (no ads): the title's "Figma Life Popup Pink" bottom, and the message's brown gradient bottom
        /// (top is <see cref="MsText"/>).</summary>
        public static readonly Color MsErrorPink = Hex("FB9EAD"), MsErrorTextBottom = Hex("721D05");
        /// <summary>Money Sort's NoInternetUI: the reconnect ring turning behind the globe.</summary>
        public static readonly Color MsReconnect = Hex("FF8C26");
        /// <summary>D-076: the tint of the win bonus button while no rewarded ad is ready.</summary>
        public static readonly Color MsDisabled = Hex("9A9A9A");
        // CR-007: the outline on a stack that can be tapped
        public const float HintOutlineWidth = 3f; // exposed native-pixel rim around a selectable art chip
        public static readonly Color HintOutline = Hex("FFF6C2");
        public static readonly Color TextShadow = new Color(0f, 0f, 0f, 0.25f);
        // FTUE: the glow ring around the stack the hand points at
        public static readonly Color TutorialRing = Hex("FFFFFF", 0.9f);
        // the "lit" ring around the new booster tile / the full Win progress square (mock-ups unlock-hand-v02, win-progress-full-v03:
        // `border: 8px solid #fff; box-shadow: 0 0 24px rgba(255,255,255,.9)`), baked by GlowRingArtGenerator into ui/glow_ring.png
        public const float GlowRingWidth = 8f, GlowRingHalo = 24f, GlowRingRadius = 48f, GlowRingHaloAlpha = 0.9f;
        // a dark chip behind light text on a button (the life cost on Retry)
        public static readonly Color ChipShade = Hex("5A3415", 0.28f);
        // 3D board (CR-003): the warm key light and the flat ambient that fills its shadows
        public static readonly Color KeyLight = Hex("FFF1DD");
        public static readonly Color Ambient = Hex("D9CBBD");
        // Abhi's table (Art.unity, D-072): white lights (warmed by 5000 K in the view) and the ambient his default skybox gave,
        // read at the sky, the horizon and the ground
        public static readonly Color TableLight = Hex("FFFFFF");
        public static readonly Color TableAmbientSky = Hex("6881A5"), TableAmbientEquator = Hex("7C8BA0"), TableAmbientGround = Hex("6C6762");
        // 3D card: a face's frame line is its colour this far toward white; a card's sides are it this far toward black
        public const float CardFrameLighten = 0.75f, CardSideDarken = 0.18f, CardBackSideDarken = 0.15f;

        // ── Card colours (§2): glossary color_0..7 (CR-012: 8 colours) ──────────────────────────────────
        public static readonly Color[] CardFace =
            { Hex("F2475B"), Hex("3D8BFF"), Hex("FFC93C"), Hex("3CC26B"), Hex("9B5CF6"), Hex("FF8A3D"), Hex("FF6FB5"), Hex("2FCFE0") };
        /// <summary>The same-colour count on a stack's top chip: a dark see-through disc of this size under a white number.</summary>
        public static readonly Color StackCountDisc = new Color(0.09f, 0.11f, 0.16f, 0.62f);
        public const float StackCountSize = 54f;
        // Ethan 11-11: the lock countdown on a frozen (locked) stack — the stack count (disc + number), this much larger
        public const float LockCountScale = 1.6f;
        public const float LockCountSize = 84f;   // the outlined Lilita number on the ice (Phat 11-11: "bold xong có viền")
        // Ethan 11-11: a thicker outline in a deep blue that blends with the ice (the shared outline material is 0.221, black)
        public const float LockCountOutline = 0.36f;
        public static readonly Color LockCountEdge = Hex("1F6FB2");   // Phat 11-11: 0B3E66 still read as black
        /// <summary>D-100: the felt of a holding slot still locked behind an RV Slot — the table felt dimmed toward slate.</summary>
        public static readonly Color BufferLockedFelt = Hex("3A4A44");
        /// <summary>Abhi's RV badge on a locked group (Locked RV mock-up): this share of the group's width, never wider than the max.</summary>
        public const float RvBadgeFill = 0.96f, RvBadgeMaxWidth = 170f;   // new mock-up: a 75px locked group contains a 72px badge
        /// <summary>A flying chip's streak starts at its face colour at this opacity and fades out (GDD §3.2).</summary>
        public const float FlightTrailAlpha = 0.6f;
        /// <summary>Covered cards: 68% brightness tint + hatch overlay (§2).</summary>
        public static readonly Color CoveredTint = new Color(0.68f, 0.68f, 0.68f, 1f);

        // ── Type (§3) — sizes in reference px ────────────────────────────────────────────────────────
        public const float TypeLogo = 150f, TypeDisplay = 84f, TypeTitleMax = 76f, TypeButton = 66f, TypeHeading = 54f,
            TypeBody = 46f, TypeHud = 44f, TypeCount = 40f, TypeLabel = 30f;
        /// <summary>Ribbon titles shrink to fit (GDD §10, CR-001): min(76, 1500 / characters).</summary>
        public static float TitleSize(int characters) => Mathf.Min(TypeTitleMax, 1500f / Mathf.Max(1, characters));

        // ── Spacing / layout (§4, §5) ────────────────────────────────────────────────────────────────
        public const float ScreenMargin = 40f;
        // Reference layouts: expose each card edge, and open a six-card fan through 60 degrees.
        public const float ReferencePileSpacing = 18f, ReferenceFanStep = 12f, ReferenceFanSpread = 60f;
        public const float HudTop = 66f, HudHeight = 104f, RoundButton = 104f;
        public const float TargetsTop = 200f, TargetsHeight = 400f, TargetSlotWidth = 205f, TargetSlotGap = 40f,   // D-072: pegs 245 apart as in Art.unity (was 300)
            TargetSlotWidthTwo = 470f, TargetSlotGapTwo = 60f, TargetCardTop = 120f, TargetCardStep = 34f;
        // New holding-rail mock-up, 1080x1920 (Phat 10-11): outer rect approximately (307,676,465,193).
        public const float ArtBufferTop = 690f, ArtBufferHeight = 160f, ArtBufferEndLeft = 64.5f, ArtBufferEndRight = 50.5f, ArtBufferOffsetX = 7f,
            ArtHeldOffsetY = -8f;
        public const float BufferLabelTop = 606f, BufferTop = 650f, BufferHeight = 200f, BufferCellTop = 30f,
            BufferCellWidth = 66f, BufferCellHeight = 140f, BufferInnerWidth = 960f, BufferInset = 34f;
        // CR-012 C2 (D-035): the booster bar is back under the tray, so the tray ends at y 1600 (was 1860 since CR-005)
        public const float TrayTop = 900f, TrayHeight = 700f, TrayRimInset = 22f;
        // WF/Flow-Booster: compact diagonal hand for picks; downward hand above the booster tile.
        public const float BoosterGuideHandSize = 0.12f;
        public const float TutorialBubbleTop = 1660f;   // CR-012 F: the FTUE bubble sits over the (dimmed) booster bar, under the tray
        // the booster bar (mock-ups gameplay-boosters-v02): three tiles centred, measured from the screen bottom
        public const float BoosterTileWidth = 170f, BoosterTileHeight = 182f, BoosterTileGap = 90f, BoosterBarBottom = 102f,
            BoosterLabelGap = 8f, BoosterLabelHeight = 44f, BoosterBadge = 72f, TypeBoosterLabel = 34f;
        // CR-011: the tray is sized to the level (padding around the stacks, never smaller than these)
        public const float TrayPadding = 36f, TrayMinWidth = 560f, TrayMinHeight = 420f;
        public const float CardWidth = 150f, CardHeight = 216f, CardBody = 206f, CardUnderHeight = 214f, CardStep = 16f;
        public const float PanelSideInset = 90f, RibbonOverhang = 30f, RibbonRise = 70f, RibbonHeight = 184f;

        // ── Motion (animation-list.md, seconds) ──────────────────────────────────────────────────────
        public static class Motion
        {
            public const float Fly = 0.32f, FlyStagger = 0.05f, FlyArc = 120f, FlyTilt = 8f, ToBuffer = 0.28f, Release = 0.32f,
                Compact = 0.15f, LandBump = 0.12f, LandBumpScale = 0.9f, Complete = 0.35f, CompleteOvershoot = 1.15f,
                Enter = 0.25f, EnterOffset = 120f, Reveal = 0.18f, Press = 0.08f, PressScale = 0.94f, Shake = 0.25f,
                ShakeAmp = 10f, WarnPeriod = 0.8f, Overflow = 0.4f, Expand = 0.35f, Rewind = 0.25f, Dialog = 0.2f,
                DialogFrom = 0.9f, BtnPress = 0.08f, BtnRelease = 0.15f, BtnScale = 0.95f, CountUp = 1.2f, Confetti = 1.2f,
                HandPeriod = 1.0f, UnlockPop = 0.4f, UnlockGiftDelay = 0.15f, RevealDelay = 1.0f, RevealDur = 0.25f,
                StarPop = 0.28f, StarPopScale = 1.22f, StarStagger = 0.04f,   // D-056 rating stars
                TabFade = 0.2f,   // Home ⇄ Shop footer tab: the Shop page fades in / out over Home, the footer stays put
                BannerIn = 0.25f, BannerFrom = 0.8f, BannerHold = 0.6f, BannerOut = 0.2f;   // the booster helper banner
            public const int ConfettiCount = 40, BurstCount = 16;
        }
    }
}
