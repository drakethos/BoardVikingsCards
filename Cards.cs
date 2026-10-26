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

        /// <summary>Material donor — never the spear "card" prefab (broken throw/pickup physics).</summary>
        private const string ItemDonor = "LeatherScraps";

        /// <summary>Piece donor — networking/WearNTear only; visuals replaced.</summary>
        private const string PieceDonor = "sign";

        private void Awake()
        {
            Loader.AddBundles();
            PrefabManager.OnVanillaPrefabsAvailable += MakeCards;
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= MakeCards;
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

            foreach (var info in cards)
            {
                string internalName = "Card_" + info.SpriteName;
                Sprite? sprite = SpriteTools.GetSprite(info.SpriteName);
                if (sprite == null)
                    Logger.LogWarning($"Missing sprite '{info.SpriteName}' for {internalName}");

                CreateCard(internalName, info, sprite);
                CreateCardPiece(internalName + "_piece", info, sprite);
            }
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
            ItemManager.Instance.AddItem(customItem);
            CardItemSanitize.SanitizeAndBuildItem(customItem, info, sprite);
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
