namespace SeedField.Simulation;

/// <summary>Bilateral treaty kinds supported by the prototype diplomacy layer.</summary>
public enum TreatyKind
{
    NonAggression = 0,
    Trade = 1,
}

/// <summary>A pending, host-authorised proposal. It has no effect until accepted.</summary>
public sealed class TreatyProposalState
{
    internal TreatyProposalState(
        long id,
        TreatyKind kind,
        string proposerCivilizationId,
        string targetCivilizationId,
        long proposedTick,
        long expiresTick)
    {
        Id = id;
        Kind = kind;
        ProposerCivilizationId = proposerCivilizationId;
        TargetCivilizationId = targetCivilizationId;
        ProposedTick = proposedTick;
        ExpiresTick = expiresTick;
    }

    public long Id { get; }

    public TreatyKind Kind { get; }

    public string ProposerCivilizationId { get; }

    public string TargetCivilizationId { get; }

    public long ProposedTick { get; }

    public long ExpiresTick { get; }

    public bool Involves(string civilizationId) =>
        string.Equals(ProposerCivilizationId, civilizationId, StringComparison.Ordinal) ||
        string.Equals(TargetCivilizationId, civilizationId, StringComparison.Ordinal);

    public string Other(string civilizationId) =>
        string.Equals(ProposerCivilizationId, civilizationId, StringComparison.Ordinal)
            ? TargetCivilizationId
            : ProposerCivilizationId;
}

/// <summary>Prototype negotiation limits. Not a final balance value.</summary>
public static class TradeNegotiationRules
{
    public const int MaxRounds = 3;
}

/// <summary>
/// An active trade offer. The proposer's give side is escrowed at creation; the
/// target's want side is only checked and consumed at acceptance.
/// </summary>
public sealed class TradeOfferState
{
    internal TradeOfferState(
        long id,
        string proposerCivilizationId,
        string targetCivilizationId,
        string regionId,
        long giveMaterial,
        long giveEnergy,
        long wantMaterial,
        long wantEnergy,
        long negotiationId,
        int round,
        long proposedTick,
        long expiresTick)
    {
        Id = id;
        ProposerCivilizationId = proposerCivilizationId;
        TargetCivilizationId = targetCivilizationId;
        RegionId = regionId;
        GiveMaterial = giveMaterial;
        GiveEnergy = giveEnergy;
        WantMaterial = wantMaterial;
        WantEnergy = wantEnergy;
        NegotiationId = negotiationId;
        Round = round;
        ProposedTick = proposedTick;
        ExpiresTick = expiresTick;
    }

    public long Id { get; }

    public string ProposerCivilizationId { get; }

    public string TargetCivilizationId { get; }

    public string RegionId { get; }

    public long GiveMaterial { get; }

    public long GiveEnergy { get; }

    public long WantMaterial { get; }

    public long WantEnergy { get; }

    public long NegotiationId { get; }

    public int Round { get; }

    public long ProposedTick { get; }

    public long ExpiresTick { get; }

    public bool Involves(string civilizationId) =>
        string.Equals(ProposerCivilizationId, civilizationId, StringComparison.Ordinal) ||
        string.Equals(TargetCivilizationId, civilizationId, StringComparison.Ordinal);

    public string Other(string civilizationId) =>
        string.Equals(ProposerCivilizationId, civilizationId, StringComparison.Ordinal)
            ? TargetCivilizationId
            : ProposerCivilizationId;
}

/// <summary>An active bilateral treaty. Parties are stored in ordinal order.</summary>
public sealed class TreatyState
{
    internal TreatyState(
        long id,
        TreatyKind kind,
        string civilizationA,
        string civilizationB,
        string proposerCivilizationId,
        long signedTick)
    {
        Id = id;
        Kind = kind;
        CivilizationA = civilizationA;
        CivilizationB = civilizationB;
        ProposerCivilizationId = proposerCivilizationId;
        SignedTick = signedTick;
    }

    public long Id { get; }

    public TreatyKind Kind { get; }

    public string CivilizationA { get; }

    public string CivilizationB { get; }

    public string ProposerCivilizationId { get; }

    public long SignedTick { get; }

    public bool Involves(string civilizationId) =>
        string.Equals(CivilizationA, civilizationId, StringComparison.Ordinal) ||
        string.Equals(CivilizationB, civilizationId, StringComparison.Ordinal);

    public string Other(string civilizationId) =>
        string.Equals(CivilizationA, civilizationId, StringComparison.Ordinal)
            ? CivilizationB
            : CivilizationA;
}
