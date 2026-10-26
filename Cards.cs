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
    [BepInEx.BepInDependency("com.drakemods.libs")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public partial class Cards : BepInEx.BaseUnityPlugin
    {
        public const string CARD_CATEGORY = "Cards";

        /// <summary>Material donor — never the spear "card" prefab (broken throw/pickup physics).</summary>
        private const string ItemDonor = "LeatherScraps";

        /// <summary>Piece donor — networking/WearNTear only; visuals replaced.</summary>
        private const string PieceDonor = "sign";

        private readonly HarmonyLib.Harmony _harmony = new HarmonyLib.Harmony(GUID);

        private void Awake()
        {
            Loader.AddBundles();
            try { _harmony.PatchAll(typeof(Cards).Assembly); }
            catch (System.Exception ex) { Logger.LogError($"Deck patches failed (cards still load): {ex.Message}"); }
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

            if (Loader.CardBundle == null)
            {
                Logger.LogError("Card AssetBundle missing — aborting card registration.");
                return;
            }

            SpriteTools.EnsureLoaded();
            CardItemSanitize.EnsureSharedProjectile();

            var cards = CardLib.All;
            Logger.LogInfo(
                $"Creating {cards.Length} throwable cards (items from {ItemDonor}, upright pieces from {PieceDonor}).");

            try { CreateDeck(); }
            catch (System.Exception ex) { Logger.LogError($"Deck item failed: {ex}"); }

            foreach (var info in cards)
            {
                string internalName = "Card_" + info.SpriteName;
                Sprite? sprite = SpriteTools.GetSprite(info.SpriteName);
                if (sprite == null)
                    Logger.LogWarning($"Missing sprite '{info.SpriteName}' for {internalName}");

                // One bad card must not stop the rest, and must never leave a half-built prefab in the game's databases.
                try { CreateCard(internalName, info, sprite); }
                catch (System.Exception ex) { Logger.LogError($"Card item {internalName} failed: {ex}"); }
                try { CreateCardPiece(internalName + "_piece", info, sprite); }
                catch (System.Exception ex) { Logger.LogError($"Card piece {internalName}_piece failed: {ex}"); }
            }
        }

        private void CreateDeck()
        {
            var back = SpriteTools.TryGetSprite("back");
            var config = new ItemConfig
            {
                CraftingStation = CraftingStations.Workbench,
                Name = Deck.DeckData.DeckName,
                Description = "A full deck of 52 cards in one item. Right-click it to pull out any card, put cards back, or shuffle.",
                Icon = back,
                Amount = 1,
                StackSize = 1,
                Requirements =
                [
                    new RequirementConfig("Wood", 2, 0, true),
                    new RequirementConfig("Coal", 1, 0, true)
                ]
            };

            var customItem = new CustomItem(Deck.DeckData.DeckPrefab, ItemDonor, config);
            CardItemSanitize.SanitizeAndBuildItem(customItem, new CardInfo("back", Deck.DeckData.DeckName), back, isDeck: true);
            ItemManager.Instance.AddItem(customItem);
        }

        private void CreateCard(string internalName, CardInfo info, Sprite? sprite)
        {
            var config = new ItemConfig
            {
                CraftingStation = CraftingStations.Workbench,
                Name = info.FullName,
                Description = "A throwable playing card. The " + info.FullName,
                Icon = sprite,
                Amount = 1,
                StackSize = 1,
                Requirements =
                [
                    new RequirementConfig("Wood", 1, 0, true),
                    new RequirementConfig("Coal", 1, 0, true)
                ]
            };

            // LeatherScraps donor = stable drop/pickup. Throw is added in SanitizeAndBuildItem.
            var customItem = new CustomItem(internalName, ItemDonor, config);
            CardItemSanitize.SanitizeAndBuildItem(customItem, info, sprite);
            ItemManager.Instance.AddItem(customItem);
        }

        private void CreateCardPiece(string internalName, CardInfo info, Sprite? sprite)
        {
            var config = new PieceConfig
            {
                CraftingStation = CraftingStations.Workbench,
                PieceTable = PieceTables.Hammer,
                Category = CARD_CATEGORY,
                Name = info.FullName,
                Description = "A giant playing card you can hang. The " + info.FullName,
                Icon = sprite,
                Requirements =
                [
                    new RequirementConfig("Wood", 1, 0, true),
                    new RequirementConfig("Coal", 1, 0, true)
                ]
            };

            var customPiece = new CustomPiece(internalName, PieceDonor, config);
            PieceManager.Instance.AddPiece(customPiece);

            var go = customPiece.PiecePrefab;
            if (go == null)
            {
                Logger.LogError($"Piece prefab missing after clone for {internalName}");
                return;
            }

            CardItemSanitize.BuildCardPiece(go, sprite, info.FullName);
        }
    }
}
