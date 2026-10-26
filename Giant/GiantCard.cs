using Cards.Util;
using UnityEngine;

namespace Cards.Giant;

/// <summary>
/// The one giant card piece. Its face is a card code in the object's saved data, so a single piece shows any of the 53 cards
/// (and stays in sync for everyone). Press E to pick the face. The piece's origin is the middle of its bottom edge, so it
/// stands on the ground and tilts around that edge.
/// </summary>
internal class GiantCard : MonoBehaviour, Interactable, Hoverable
{
    internal const string PieceName = "Card_Giant";
    internal const string FaceKey = "BoardVikings_GiantFace";
    internal const string Back = "back";
    internal const string BackKey = "BoardVikings_GiantBack";

    /// <summary>The face the next placed card gets: whatever was picked last.</summary>
    internal static string NextFace = Back;

    /// <summary>The back design the next placed card gets: whatever was picked last.</summary>
    internal static int NextBack = CardBacks.DefaultBack;

    private ZNetView? _nview;
    private GameObject? _front;
    private GameObject? _backSprite;
    private string? _shown;
    private int _shownBack = -1;

    private void Awake()
    {
        _nview = GetComponent<ZNetView>();
        var visual = transform.Find("card_visual/front");
        _front = visual != null ? visual.gameObject : null;
        var backVisual = transform.Find("card_visual/back");
        _backSprite = backVisual != null ? backVisual.gameObject : null;

        // A placement ghost has no valid object; only real pieces get a face.
        if (IsReal && _nview!.IsOwner() && string.IsNullOrEmpty(_nview.GetZDO().GetString(FaceKey)))
        {
            _nview.GetZDO().Set(FaceKey, NextFace);
            _nview.GetZDO().Set(BackKey, NextBack);
        }

        Refresh();
        InvokeRepeating(nameof(Refresh), 0.5f, 0.5f);
    }

    private bool IsReal => _nview != null && _nview.IsValid();

    internal string Face => IsReal ? _nview!.GetZDO().GetString(FaceKey, Back) : Back;

    internal int BackDesign => IsReal ? CardBacks.Clamp(_nview!.GetZDO().GetInt(BackKey, CardBacks.DefaultBack)) : CardBacks.DefaultBack;

    internal void SetBackDesign(int index)
    {
        if (!IsReal)
            return;
        _nview!.ClaimOwnership();
        _nview.GetZDO().Set(BackKey, CardBacks.Clamp(index));
        NextBack = CardBacks.Clamp(index);
        Refresh();
    }

    internal void SetFace(string code)
    {
        if (!IsReal)
            return;
        _nview!.ClaimOwnership();
        _nview.GetZDO().Set(FaceKey, code);
        NextFace = code;
        Refresh();
    }

    private void Refresh()
    {
        if (_front == null)
            return;
        var face = Face;
        if (face != _shown)
        {
            _shown = face;
            // A giant card that shows "back" shows its chosen back design on both sides.
            var sprite = face == Back ? CardBacks.Back(BackDesign) : SpriteTools.TryGetSprite(face) ?? CardBacks.Back(BackDesign);
            CardItemSanitize.ApplySprite(_front, sprite, CardItemSanitize.PieceSize);
        }

        int design = BackDesign;
        if (design != _shownBack && _backSprite != null)
        {
            _shownBack = design;
            CardItemSanitize.ApplySprite(_backSprite, CardBacks.Back(design), CardItemSanitize.PieceSize);
            if (face == Back)
                CardItemSanitize.ApplySprite(_front, CardBacks.Back(design), CardItemSanitize.PieceSize);
        }
    }

    public string GetHoverName() => "Giant Card";

    // The game's Hoverable has this member; the compile-time reference interface lacks it, so it must exist (virtual) here
    // or the class fails to load (VTable setup).
    public virtual float GetHoverOffset() => 0f;

    public string GetHoverText() =>
        Localization.instance.Localize("Giant Card\n[<color=yellow><b>$KEY_Use</b></color>] Change card face");

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold || user != Player.m_localPlayer)
            return false;
        if (!PrivateArea.CheckAccess(transform.position, 0f, true, false))
            return true;

        GiantCardPicker.Open(this);
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
}
