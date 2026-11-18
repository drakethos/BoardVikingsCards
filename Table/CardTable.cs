using System;
using Cards.Deck;
using Cards.Util;
using UnityEngine;

namespace Cards.Table;

/// <summary>
/// The card table piece. It holds no game logic of its own: the whole table (seats, hands, deck, rules) is a <see cref="TableState"/>
/// saved in the piece's object data. E opens the panel; every action goes to the table's owner as a short text command, which
/// changes the state, and every client redraws from the saved state.
/// </summary>
internal class CardTable : MonoBehaviour, Hoverable, Interactable
{
    internal const string StateKey = "BoardVikings_Table";
    internal const string ActRpc = "BoardVikings_TableAct";

    /// <summary>Table top size in meters (copied into every placed table).</summary>
    public Vector2 Size = new Vector2(2.6f, 1.2f);

    /// <summary>Height of the table top above the piece origin.</summary>
    public float TopHeight = 0.81f;

    public string Title = "Card Table";

    private ZNetView? _nview;
    private string _raw = "";
    private TableState _state = new TableState();
    private bool _eaten;

    /// <summary>The saved table changed (someone sat down, dealt, ...).</summary>
    internal event Action? Changed;

    internal TableState State => _state;

    private bool IsReal => _nview != null && _nview.IsValid();

    private void Awake()
    {
        _nview = GetComponent<ZNetView>();
        _state.Normalize();
        if (IsReal)
            _nview!.Register<string>(ActRpc, RPC_Act);
        Refresh();
        InvokeRepeating(nameof(Refresh), 0.1f, 0.25f);
    }

    /// <summary>Read the saved state; redraw and tell listeners when it changed.</summary>
    internal void Refresh()
    {
        if (!IsReal)
            return;
        var raw = _nview!.GetZDO().GetString(StateKey, "");
        if (raw == _raw && _raw.Length > 0)
            return;
        _raw = raw;
        _state = TableState.Parse(raw);
        GetComponent<TableView>()?.Redraw(_state);
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- acting

    /// <summary>The player who built the table (0 when unknown).</summary>
    internal long Creator
    {
        get
        {
            var piece = GetComponent<Piece>();
            return piece != null ? piece.GetCreator() : 0;
        }
    }

    internal static long MyId => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0;

    internal static string MyName => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "";

    /// <summary>Am I wearing the dealer's visor?</summary>
    internal static bool MyVisor => DealerVisor.IsWorn(Player.m_localPlayer);

    /// <summary>
    /// The dealer: whoever wears the visor, or the player who holds the deal while the rules are unlocked. The first player to
    /// use the table takes the deal. (The visor is claimed in every command, so it is an honor system, like the hidden hands.)
    /// </summary>
    internal bool IsDealerMe => _state.PlayerIsDealer(MyId, MyVisor);

    /// <summary>Do an action as the local player (it runs here when this client owns the table, otherwise on the owner).</summary>
    internal void Act(string action)
    {
        if (!IsReal)
            return;
        if (_nview!.IsOwner())
            Run(action);
        else
            _nview.InvokeRPC(ActRpc, action);
    }

    /// <summary>Put the deck in my inventory on the table (the item goes; its cards, in order, go on the felt). False when I cannot.</summary>
    internal bool PlaceDeckFromInventory()
    {
        var player = Player.m_localPlayer;
        var item = DeckApi.FindDeck();
        if (player == null || item == null || _state.deck.Count > 0)
            return false;
        var cards = DeckApi.ToTableCards(item);
        if (cards.Count == 0)
            return false;
        player.GetInventory().RemoveItem(item);
        Act(TableActions.PlaceDeck(MyId, MyVisor, item.m_variant, cards));
        return true;
    }

    /// <summary>
    /// Lay a playing card from my inventory onto a spread (the item is removed). Spread -1 starts a new row.
    /// False when it is not my turn, or the item is not a playing card.
    /// </summary>
    internal bool LayCarried(ItemDrop.ItemData? item, int spread, bool faceUp)
    {
        var player = Player.m_localPlayer;
        var code = DeckData.LooseCardCode(item);
        if (player == null || item == null || code == null)
            return false;
        int card = CardLib.IndexOf(code);
        if (card < 0 || !CanLayNow())
            return false;
        if (!player.GetInventory().RemoveItem(item))
            return false;
        Act(TableActions.PlaceCard(MyId, MyVisor, card, spread, faceUp));
        return true;
    }

    /// <summary>Seated, and it is my turn (or I am the dealer). Mirrors the check the table owner runs.</summary>
    private bool CanLayNow()
    {
        int seat = _state.SeatOf(MyId);
        bool seated = seat >= 0 && seat < _state.seatCount;
        return seated && (_state.mode == TableState.Free || _state.turn == seat || IsDealerMe);
    }

    /// <summary>
    /// The table is being broken (hammer or damage), not unloaded with the zone. The cards in play are not dropped: they are
    /// destroyed with the table, and so is any card or deck lying on the felt.
    /// </summary>
    internal void EatCards()
    {
        if (_eaten || !IsReal || !_nview!.IsOwner())
            return;
        _eaten = true;
        int inPlay = _state.CardTotal();
        var zdo = _nview.GetZDO();
        if (zdo != null)
            zdo.Set(StateKey, "");
        _state = new TableState();
        _raw = "";
        int loose = EatLooseOnFelt();
        if (inPlay > 0 || loose > 0)
            Jotunn.Logger.LogInfo($"Card table broke; {inPlay} cards in play and {loose} on the felt were destroyed.");
    }

    /// <summary>Playing cards, throwing cards and decks resting on the table top. Cards on the ground beside it are left alone.</summary>
    private int EatLooseOnFelt()
    {
        if (ZNetScene.instance == null)
            return 0;
        int eaten = 0;
        var drops = UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < drops.Length; i++)
        {
            var drop = drops[i];
            if (!drop)
                continue;
            var prefab = drop.m_itemData?.m_dropPrefab;
            if (prefab == null)
                continue;
            var name = prefab.name;
            if (name != CardLib.PlayingPrefab && name != CardLib.ThrowingPrefab && name != DeckData.DeckPrefab)
                continue;
            if (!OnFelt(drop.transform.position))
                continue;
            var view = drop.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && !view.IsOwner())
                view.ClaimOwnership();
            ZNetScene.instance.Destroy(drop.gameObject);
            eaten++;
        }

        return eaten;
    }

    private bool OnFelt(Vector3 world)
    {
        var local = transform.InverseTransformPoint(world);
        if (Mathf.Abs(local.x) > Size.x * 0.5f + 0.2f)
            return false;
        if (Mathf.Abs(local.z) > Size.y * 0.5f + 0.2f)
            return false;
        return local.y >= TopHeight - 0.25f && local.y <= TopHeight + 0.55f;
    }

    /// <summary>Pick the deck up: I get a deck item holding what is left of it, in order.</summary>
    internal bool TakeDeckToInventory()
    {
        var player = Player.m_localPlayer;
        if (player == null || _state.deck.Count == 0)
            return false;
        var cards = new System.Collections.Generic.List<int>(_state.deck);
        int back = _state.deckBack;
        Act(TableActions.TakeDeck(MyId, MyVisor));
        return DeckApi.MakeDeckItem(player, cards, back);
    }

    private void RPC_Act(long sender, string action)
    {
        if (IsReal && _nview!.IsOwner())
            Run(action);
    }

    private void Run(string action)
    {
        var zdo = _nview!.GetZDO();
        var state = TableState.Parse(zdo.GetString(StateKey, ""));
        if (TableActions.Apply(state, action, Creator))
            zdo.Set(StateKey, state.Serialize());
        Refresh();
    }

    // ---------------------------------------------------------------- hover and E

    public string GetHoverName() => Title;

    // The game's Hoverable has this member; the compile-time reference interface lacks it, so it must exist (virtual) here
    // or the class fails to load (VTable setup).
    public virtual float GetHoverOffset() => 0f;

    public string GetHoverText()
    {
        int seat = _state.SeatOf(MyId);
        string who;
        if (IsDealerMe)
            who = "you are the dealer";
        else if (_state.dealer == 0)
            who = "no dealer yet";
        else if (_state.dealer == MyId)
            who = "you hold the deal";
        else
            who = string.IsNullOrEmpty(_state.dealerName) ? "dealer chosen" : _state.dealerName + " is the dealer";
        string use = seat >= 0 ? "Open your seat" : "Sit down";
        if (Localization.instance == null)
            return Title;
        return Localization.instance.Localize(
            $"{Title}  ({_state.Taken()}/{_state.seatCount} seated, {who})\n[<color=yellow><b>$KEY_Use</b></color>] {use}");
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold || user != Player.m_localPlayer)
            return false;
        // The first player to use the table becomes the dealer. Someone who only pressed E, and never sat, loses it to the next player.
        if (!_state.DealerIsSeated())
            Act(TableActions.Touch(MyId, MyVisor, MyName));
        TablePanel.Open(this);
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        if (user != Player.m_localPlayer)
            return false;
        return LayCarried(item, -1, true);
    }
}
