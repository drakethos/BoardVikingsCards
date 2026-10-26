using System.Linq;
using Cards.Deck;
using Cards.Giant;
using Cards.Table;
using Cards.Target;
using Cards.Util;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Cards
{
    [BepInEx.BepInPlugin(GUID, ModName, Version)]
    [BepInEx.BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public partial class Cards : BepInEx.BaseUnityPlugin
    {
        public const string CARD_CATEGORY = "Cards";

        /// <summary>Material donor: stable drop/pickup physics. Never the spear "card" prefab.</summary>
        private const string ItemDonor = "LeatherScraps";

        /// <summary>Piece donor: networking/WearNTear only; visuals replaced.</summary>
        private const string PieceDonor = "sign";

        private static BepInEx.Configuration.ConfigEntry<UnityEngine.KeyCode>? _tiltKey;
        private static BepInEx.Configuration.ConfigEntry<float>? _tiltStep;
        private static BepInEx.Configuration.ConfigEntry<bool>? _alwaysTilt;

        /// <summary>Use the built-in tilt even when the Gizmo mod is installed.</summary>
        internal static bool AlwaysBuiltInTilt => _alwaysTilt?.Value ?? false;

        /// <summary>Hold while scrolling in build mode to tilt a giant card.</summary>
        internal static UnityEngine.KeyCode TiltKey => _tiltKey?.Value ?? UnityEngine.KeyCode.LeftControl;

        /// <summary>Degrees per scroll notch.</summary>
        internal static float TiltStep => _tiltStep?.Value ?? 7.5f;

        private static ItemDrop? _throwingCard;

        private readonly HarmonyLib.Harmony _harmony = new HarmonyLib.Harmony(GUID);

        private void Awake()
        {
            _tiltKey = Config.Bind("Giant card", "Tilt key", UnityEngine.KeyCode.LeftControl,
                "Hold this and scroll while placing a giant card to tilt it.");
            _tiltStep = Config.Bind("Giant card", "Tilt step", 7.5f,
                new BepInEx.Configuration.ConfigDescription("Degrees per scroll notch.",
                    new BepInEx.Configuration.AcceptableValueRange<float>(1f, 30f)));
            _alwaysTilt = Config.Bind("Giant card", "Built-in tilt with Gizmo", false,
                "The built-in tilt (hold the tilt key and scroll) is switched off when the Gizmo mod is installed, because Gizmo " +
                "rotates freely on every axis. Turn this on to use the built-in tilt as well.");
            var brightness = Config.Bind("Appearance", "Paper brightness", 0.62f,
                new BepInEx.Configuration.ConfigDescription(
                    "How bright the cards are. Lower it if cards glow in bright sun, raise it if they look dull.",
                    new BepInEx.Configuration.AcceptableValueRange<float>(0.05f, 1f)));
            CardMesh.SetBrightness(brightness.Value);
            brightness.SettingChanged += (_, _) => CardMesh.SetBrightness(brightness.Value);
            try { _harmony.PatchAll(typeof(Cards).Assembly); CardTargets.Apply(_harmony); }
            catch (System.Exception ex) { Logger.LogError($"Card patches failed (cards still load): {ex.Message}"); }
            PrefabManager.OnVanillaPrefabsAvailable += MakeCards;
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= MakeCards;
            _harmony.UnpatchSelf();
        }

        private void MakeCards()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= MakeCards;

            SpriteTools.EnsureLoaded();
            CardBacks.Init();
            CardIcons.Init();
            CardItemSanitize.EnsureSharedProjectile();

            Logger.LogInfo($"Creating cards: throwing card, playing card, deck, giant card ({CardLib.All.Length} faces).");

            // One bad item must not stop the rest, and must never leave a half-built prefab in the game's databases.
            try { CreateThrowingCard(); }
            catch (System.Exception ex) { Logger.LogError($"Throwing card failed: {ex}"); }
            try { CreatePlayingCard(); }
            catch (System.Exception ex) { Logger.LogError($"Playing card failed: {ex}"); }
            try { CreateDeck(); }
            catch (System.Exception ex) { Logger.LogError($"Deck item failed: {ex}"); }
            try { CreateGiantCard(); }
            catch (System.Exception ex) { Logger.LogError($"Giant card failed: {ex}"); }
            try { CreateTable("Card_Table_Small", "Card Table (Small)", new Vector2(1.5f, 1.0f), 12); }
            catch (System.Exception ex) { Logger.LogError($"Small card table failed: {ex}"); }
            try { CreateTable("Card_Table_Long", "Card Table (Long)", new Vector2(2.6f, 1.2f), 20); }
            catch (System.Exception ex) { Logger.LogError($"Long card table failed: {ex}"); }
            try { DealerVisor.Create(); }
            catch (System.Exception ex) { Logger.LogError($"Dealer visor failed: {ex}"); }
            try { CardTargets.Register(_throwingCard); }
            catch (System.Exception ex) { Logger.LogError($"Archery target support failed: {ex}"); }
        }

        private static Sprite[] FaceIcons() =>
            CardLib.All.Select(c => CardIcons.Square(c.SpriteName) ?? SpriteTools.TryGetSprite(c.SpriteName)!).ToArray();

        /// <summary>
        /// A card to throw or decorate with. One item; the face is the item's variant, picked when you craft it.
        /// </summary>
        private void CreateThrowingCard()
        {
            const string description = "A playing card you can throw. Pick the card when you craft it.";
            var config = new ItemConfig
            {
                CraftingStation = CraftingStations.Workbench,
                Name = "Throwing Card",
                Description = description,
                Icons = FaceIcons(),
                Amount = 1,
                StackSize = 1,
                Requirements =
                [
                    new RequirementConfig("Wood", 1, 0, true),
                    new RequirementConfig("Coal", 1, 0, true)
                ]
            };

            var customItem = new CustomItem(CardLib.ThrowingPrefab, ItemDonor, config);
            CardItemSanitize.SanitizeAndBuildItem(customItem, CardItemSanitize.Kind.Throwing, "Throwing Card", description,
                SpriteTools.TryGetSprite(CardLib.All[0].SpriteName), CardLib.All.Length);
            ItemManager.Instance.AddItem(customItem);
            _throwingCard = customItem.ItemDrop;
        }

        /// <summary>A card that came out of a deck. Not craftable on its own: it only exists to be played and put back.</summary>
        private void CreatePlayingCard()
        {
            const string description = "A card from a deck. Put it back in a deck to keep the set whole.";
            var config = new ItemConfig
            {
                Enabled = false,
                Name = "Playing Card",
                Description = description,
                Icons = FaceIcons(),
                Amount = 1,
                StackSize = 1
            };

            var customItem = new CustomItem(CardLib.PlayingPrefab, ItemDonor, config);
            CardItemSanitize.SanitizeAndBuildItem(customItem, CardItemSanitize.Kind.Playing, "Playing Card", description,
                SpriteTools.TryGetSprite(CardLib.All[0].SpriteName), CardLib.All.Length);
            ItemManager.Instance.AddItem(customItem);
        }

        private void CreateDeck()
        {
            const string description = "A full deck of 52 cards in one box. Pick the back design when you craft it. " +
                                       "Right-click it to pull out any card, put cards back, or shuffle.";
            var icons = CardBacks.DeckIcons;
            var config = new ItemConfig
            {
                CraftingStation = CraftingStations.Workbench,
                Name = DeckData.DeckName,
                Description = description,
                Icons = icons,
                Amount = 1,
                StackSize = 1,
                Requirements =
                [
                    new RequirementConfig("Wood", 20, 0, true),
                    new RequirementConfig("Coal", 5, 0, true)
                ]
            };

            var customItem = new CustomItem(DeckData.DeckPrefab, ItemDonor, config);
            CardItemSanitize.SanitizeAndBuildItem(customItem, CardItemSanitize.Kind.Deck, DeckData.DeckName, description,
                icons[CardBacks.DefaultBack], CardBacks.Count);
            ItemManager.Instance.AddItem(customItem);
        }

        /// <summary>A felt card table with seats around it. The seats are set at the table (2 to 8), so one piece suits any game.</summary>
        private void CreateTable(string prefabName, string title, Vector2 size, int wood)
        {
            var config = new PieceConfig
            {
                CraftingStation = CraftingStations.Workbench,
                PieceTable = PieceTables.Hammer,
                Category = CARD_CATEGORY,
                Name = title,
                Description = "A felt table for card games. Press E to sit; the table's builder is its dealer and sets how many " +
                              "seats it uses. Free play: no rules unless the dealer sets them.",
                Icon = CardBacks.DeckIcon(CardBacks.DefaultBack),
                Requirements =
                [
                    new RequirementConfig("Wood", wood, 0, true),
                    new RequirementConfig("LeatherScraps", 4, 0, true)
                ]
            };

            var customPiece = new CustomPiece(prefabName, PieceDonor, config);
            var go = customPiece.PiecePrefab;
            if (go == null)
            {
                Logger.LogError($"{prefabName} prefab missing after clone.");
                return;
            }

            TableBuilder.Build(go, size, title);
            PieceManager.Instance.AddPiece(customPiece);
        }

        private void CreateGiantCard()
        {
            var back = SpriteTools.TryGetSprite("back");
            var config = new PieceConfig
            {
                CraftingStation = CraftingStations.Workbench,
                PieceTable = PieceTables.Hammer,
                Category = CARD_CATEGORY,
                Name = "Giant Card",
                Description = "A giant playing card that stands on its edge. Press E to choose its face. " +
                              "While placing, hold Ctrl and scroll to lean it (or use the Gizmo mod), so cards can lean on each other.",
                Icon = CardIcons.Square("back") ?? back,
                Requirements =
                [
                    new RequirementConfig("Wood", 1, 0, true),
                    new RequirementConfig("Coal", 1, 0, true)
                ]
            };

            var customPiece = new CustomPiece(GiantCard.PieceName, PieceDonor, config);
            var go = customPiece.PiecePrefab;
            if (go == null)
            {
                Logger.LogError("Giant card prefab missing after clone.");
                return;
            }

            CardItemSanitize.BuildCardPiece(go, back, "Giant Card");
            go.AddComponent<GiantCard>();
            PieceManager.Instance.AddPiece(customPiece);
        }
    }
}
