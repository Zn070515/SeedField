using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// M0 diplomacy panel for civilization A. It only raises requests; Main owns the
/// world and executes host-authorised diplomacy commands.
/// </summary>
public partial class DiplomacyPanel : PanelContainer
{
    private const string PlayerCivilizationId = "A";
    private const string OpponentCivilizationId = "B";
    private const string TradeRegionId = "frontier";

    public event Action<TreatyKind>? ProposeTreatyRequested;

    public event Action<long>? BreakTreatyRequested;

    public event Action<long, bool>? RespondTreatyRequested;

    public event Action<long, bool>? RespondTradeRequested;

    public event Action<long>? CancelTradeRequested;

    public event Action<long, long, long, long>? ProposeTradeRequested;

    public event Action<long, long, long, long, long>? CounterTradeRequested;

    public event Action? CloseRequested;

    private Label _connection = null!;
    private ItemList _treaties = null!;
    private ItemList _proposals = null!;
    private ItemList _offers = null!;
    private SpinBox _giveMaterial = null!;
    private SpinBox _giveEnergy = null!;
    private SpinBox _wantMaterial = null!;
    private SpinBox _wantEnergy = null!;
    private Label _status = null!;
    private readonly List<long> _treatyIds = new();
    private readonly List<long> _proposalIds = new();
    private readonly List<bool> _proposalIncoming = new();
    private readonly List<long> _offerIds = new();
    private readonly List<bool> _offerIncoming = new();

    public override void _Ready()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.08f, 0.13f, 0.98f),
            BorderColor = new Color(0.25f, 0.50f, 0.65f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        };
        style.SetCornerRadiusAll(6);
        AddThemeStyleboxOverride("panel", style);

        Position = new Vector2(300, 24);
        Size = new Vector2(680, 672);
        CustomMinimumSize = Size;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        var title = new Label { Text = "A DIPLOMACY · TREATIES AND TRADE" };
        title.AddThemeFontSizeOverride("font_size", 19);
        box.AddChild(title);

        _connection = new Label { Text = "COMM · frontier stocks" };
        _connection.AddThemeFontSizeOverride("font_size", 12);
        _connection.Modulate = new Color(0.55f, 0.68f, 0.78f);
        box.AddChild(_connection);

        box.AddChild(SectionLabel("TREATY ACTIONS · TARGET B · EXPIRES IN 200 TICKS"));
        var treatyRow = new HBoxContainer();
        treatyRow.AddThemeConstantOverride("separation", 8);
        treatyRow.AddChild(ActionButton("PROPOSE NAP", () =>
            ProposeTreatyRequested?.Invoke(TreatyKind.NonAggression)));
        treatyRow.AddChild(ActionButton("PROPOSE TRADE", () =>
            ProposeTreatyRequested?.Invoke(TreatyKind.Trade)));
        treatyRow.AddChild(ActionButton("BREAK SELECTED", OnBreakTreatyPressed));
        box.AddChild(treatyRow);

        _treaties = new ItemList { CustomMinimumSize = new Vector2(640, 62) };
        box.AddChild(_treaties);

        box.AddChild(SectionLabel("PENDING PROPOSALS"));
        _proposals = new ItemList { CustomMinimumSize = new Vector2(640, 70) };
        box.AddChild(_proposals);

        var proposalRow = new HBoxContainer();
        proposalRow.AddThemeConstantOverride("separation", 8);
        proposalRow.AddChild(ActionButton("ACCEPT SELECTED", () => RespondProposal(true)));
        proposalRow.AddChild(ActionButton("REJECT SELECTED", () => RespondProposal(false)));
        var proposalHint = new Label
        {
            Text = "Only incoming proposals can be answered.",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        proposalHint.AddThemeFontSizeOverride("font_size", 11);
        proposalHint.Modulate = new Color(0.55f, 0.68f, 0.78f);
        proposalRow.AddChild(proposalHint);
        box.AddChild(proposalRow);

        box.AddChild(SectionLabel("TRADE OFFERS"));
        _offers = new ItemList { CustomMinimumSize = new Vector2(640, 92) };
        box.AddChild(_offers);

        var offerRow = new HBoxContainer();
        offerRow.AddThemeConstantOverride("separation", 8);
        offerRow.AddChild(ActionButton("ACCEPT SELECTED", () => RespondOffer(true)));
        offerRow.AddChild(ActionButton("REJECT SELECTED", () => RespondOffer(false)));
        offerRow.AddChild(ActionButton("COUNTER SELECTED", OnCounterOfferPressed));
        offerRow.AddChild(ActionButton("CANCEL SELECTED", OnCancelOfferPressed));
        box.AddChild(offerRow);

        box.AddChild(SectionLabel("CREATE / COUNTER TRADE OFFER · FRONTIER · TARGET B"));
        var goodsRow = new HBoxContainer();
        goodsRow.AddThemeConstantOverride("separation", 8);
        _giveMaterial = AddGoods(goodsRow, "Give M", 20);
        _giveEnergy = AddGoods(goodsRow, "Give E", 0);
        _wantMaterial = AddGoods(goodsRow, "Want M", 0);
        _wantEnergy = AddGoods(goodsRow, "Want E", 10);
        box.AddChild(goodsRow);

        var createRow = new HBoxContainer();
        createRow.AddThemeConstantOverride("separation", 8);
        createRow.AddChild(ActionButton("CREATE OFFER", OnCreateOfferPressed));
        var createHint = new Label
        {
            Text = "COUNTER uses these fields. Escrow is taken from A's frontier stock.",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        createHint.AddThemeFontSizeOverride("font_size", 11);
        createHint.Modulate = new Color(0.55f, 0.68f, 0.78f);
        createRow.AddChild(createHint);
        box.AddChild(createRow);

        _status = new Label
        {
            Text = "ready",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(640, 26),
        };
        _status.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(_status);

        var close = new Button { Text = "CLOSE  F3" };
        close.Pressed += () => CloseRequested?.Invoke();
        box.AddChild(close);
    }

    public void Refresh(WorldState world, bool canCommunicate)
    {
        var frontier = world.Regions.TryGetValue(TradeRegionId, out var region)
            ? region
            : null;
        _connection.Text =
            $"COMM A↔B {(canCommunicate ? "OK" : "NO PATH")} · " +
            $"A M{frontier?.GetMaterial(PlayerCivilizationId) ?? 0}/E{frontier?.GetEnergy(PlayerCivilizationId) ?? 0} · " +
            $"B M{frontier?.GetMaterial(OpponentCivilizationId) ?? 0}/E{frontier?.GetEnergy(OpponentCivilizationId) ?? 0}";

        _treatyIds.Clear();
        _treaties.Clear();
        foreach (var treaty in world.Treaties.Values
                     .Where(treaty => treaty.Involves(PlayerCivilizationId))
                     .OrderBy(treaty => treaty.Id))
        {
            _treatyIds.Add(treaty.Id);
            _treaties.AddItem(
                $"{Main.KindLabel(treaty.Kind)} · {treaty.Other(PlayerCivilizationId)} · " +
                $"signed t{treaty.SignedTick}");
        }

        if (_treaties.ItemCount > 0)
        {
            _treaties.Select(0);
        }

        _proposalIds.Clear();
        _proposalIncoming.Clear();
        _proposals.Clear();
        foreach (var proposal in world.TreatyProposals.Values
                     .Where(proposal => proposal.Involves(PlayerCivilizationId))
                     .OrderBy(proposal => proposal.Id))
        {
            var incoming = string.Equals(
                proposal.TargetCivilizationId,
                PlayerCivilizationId,
                StringComparison.Ordinal);
            _proposalIds.Add(proposal.Id);
            _proposalIncoming.Add(incoming);
            _proposals.AddItem(
                $"{(incoming ? "IN" : "OUT")} · {Main.KindLabel(proposal.Kind)} · " +
                $"{proposal.Other(PlayerCivilizationId)} · expires t{proposal.ExpiresTick}");
        }

        if (_proposals.ItemCount > 0)
        {
            _proposals.Select(0);
        }

        _offerIds.Clear();
        _offerIncoming.Clear();
        _offers.Clear();
        foreach (var offer in world.TradeOffers.Values
                     .Where(offer => offer.Involves(PlayerCivilizationId))
                     .OrderBy(offer => offer.Id))
        {
            var incoming = string.Equals(
                offer.TargetCivilizationId,
                PlayerCivilizationId,
                StringComparison.Ordinal);
            _offerIds.Add(offer.Id);
            _offerIncoming.Add(incoming);
            _offers.AddItem(
                $"{(incoming ? "IN" : "OUT")} · {offer.RegionId} · " +
                $"give M{offer.GiveMaterial}/E{offer.GiveEnergy} → " +
                $"want M{offer.WantMaterial}/E{offer.WantEnergy} · " +
                $"R{offer.Round}/{TradeNegotiationRules.MaxRounds} · " +
                $"expires t{offer.ExpiresTick}");
        }

        if (_offers.ItemCount > 0)
        {
            _offers.Select(0);
        }
    }

    public void SetStatus(string text, bool error)
    {
        _status.Text = text;
        _status.Modulate = error
            ? new Color(1.00f, 0.45f, 0.35f)
            : new Color(0.50f, 0.95f, 0.60f);
    }

    private void OnBreakTreatyPressed()
    {
        var index = SelectedIndex(_treaties);
        if (index < 0 || index >= _treatyIds.Count)
        {
            SetStatus("select an active treaty first", true);
            return;
        }

        BreakTreatyRequested?.Invoke(_treatyIds[index]);
    }

    private void RespondProposal(bool accept)
    {
        var index = SelectedIndex(_proposals);
        if (index < 0 || index >= _proposalIds.Count)
        {
            SetStatus("select a pending proposal first", true);
            return;
        }

        if (!_proposalIncoming[index])
        {
            SetStatus("outgoing proposal is waiting for B", true);
            return;
        }

        RespondTreatyRequested?.Invoke(_proposalIds[index], accept);
    }

    private void RespondOffer(bool accept)
    {
        var index = SelectedIndex(_offers);
        if (index < 0 || index >= _offerIds.Count)
        {
            SetStatus("select a trade offer first", true);
            return;
        }

        if (!_offerIncoming[index])
        {
            SetStatus("cannot accept or reject your own offer; use CANCEL", true);
            return;
        }

        RespondTradeRequested?.Invoke(_offerIds[index], accept);
    }

    private void OnCounterOfferPressed()
    {
        var index = SelectedIndex(_offers);
        if (index < 0 || index >= _offerIds.Count)
        {
            SetStatus("select a trade offer first", true);
            return;
        }

        if (!_offerIncoming[index])
        {
            SetStatus("cannot counter your own offer; use CANCEL", true);
            return;
        }

        var giveMaterial = (long)_giveMaterial.Value;
        var giveEnergy = (long)_giveEnergy.Value;
        var wantMaterial = (long)_wantMaterial.Value;
        var wantEnergy = (long)_wantEnergy.Value;

        if ((giveMaterial == 0 && giveEnergy == 0) ||
            (wantMaterial == 0 && wantEnergy == 0))
        {
            SetStatus("counter offer needs goods on both sides", true);
            return;
        }

        CounterTradeRequested?.Invoke(
            _offerIds[index],
            giveMaterial,
            giveEnergy,
            wantMaterial,
            wantEnergy);
    }

    private void OnCancelOfferPressed()
    {
        var index = SelectedIndex(_offers);
        if (index < 0 || index >= _offerIds.Count)
        {
            SetStatus("select a trade offer first", true);
            return;
        }

        if (_offerIncoming[index])
        {
            SetStatus("incoming offers must be accepted or rejected", true);
            return;
        }

        CancelTradeRequested?.Invoke(_offerIds[index]);
    }

    private void OnCreateOfferPressed()
    {
        var giveMaterial = (long)_giveMaterial.Value;
        var giveEnergy = (long)_giveEnergy.Value;
        var wantMaterial = (long)_wantMaterial.Value;
        var wantEnergy = (long)_wantEnergy.Value;

        if ((giveMaterial == 0 && giveEnergy == 0) ||
            (wantMaterial == 0 && wantEnergy == 0))
        {
            SetStatus("both sides of the offer need at least one good", true);
            return;
        }

        ProposeTradeRequested?.Invoke(giveMaterial, giveEnergy, wantMaterial, wantEnergy);
    }

    private static int SelectedIndex(ItemList list)
    {
        var selected = list.GetSelectedItems();
        return selected.Length == 0 ? -1 : selected[0];
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.Modulate = new Color(0.55f, 0.68f, 0.78f);
        return label;
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        return button;
    }

    private static SpinBox AddGoods(HBoxContainer row, string label, int value)
    {
        var caption = new Label { Text = label };
        caption.AddThemeFontSizeOverride("font_size", 12);
        caption.Modulate = new Color(0.55f, 0.68f, 0.78f);
        row.AddChild(caption);

        var spin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 10_000,
            Step = 1,
            Value = value,
            CustomMinimumSize = new Vector2(80, 0),
        };
        row.AddChild(spin);
        return spin;
    }
}
