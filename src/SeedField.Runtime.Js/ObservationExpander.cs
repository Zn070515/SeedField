namespace SeedField.Runtime.Js;

/// <summary>
/// Trusted JS that turns the compact controller payload back into the documented
/// observation object. It runs inside the controller harness before <c>decide</c>;
/// user scripts never see the positional arrays or the string table.
/// </summary>
internal static class ObservationExpander
{
    public const string Script = """
        function __seedfield_expand(o) {
            if (o.s === undefined) { return o; }
            var s = o.s;
            var kinds = ["NonAggression", "Trade"];
            var ores = ["Copper", "Lead", "Titanium", "Coal"];
            var i, src, out, row;

            src = o.entities;
            out = new Array(src.length);
            for (i = 0; i < src.length; i++) {
                row = src[i];
                out[i] = {
                    entityId: row[0],
                    civilizationId: s[row[1]],
                    regionId: s[row[2]],
                    x: row[3],
                    y: row[4],
                    health: row[5],
                    maxHealth: row[6],
                    isSelf: row[7] === 1,
                    hasSensor: row[8] === 1,
                    sensorRangeMm: row[9],
                    hasWeapon: row[10] === 1,
                    weaponRangeMm: row[11],
                    hasDrive: row[12] === 1,
                    hasComm: row[13] === 1,
                    hasDrill: row[14] === 1,
                    drillRangeMm: row[15]
                };
            }
            o.entities = out;

            src = o.messages;
            out = new Array(src.length);
            for (i = 0; i < src.length; i++) {
                row = src[i];
                out[i] = {
                    messageId: row[0],
                    fromEntityId: row[1],
                    fromCivilizationId: s[row[2]],
                    targetRegionId: s[row[3]],
                    topic: s[row[4]],
                    payload: row[5],
                    sentTick: row[6],
                    deliveredTick: row[7]
                };
            }
            o.messages = out;

            src = o.shipments;
            out = new Array(src.length);
            for (i = 0; i < src.length; i++) {
                row = src[i];
                out[i] = {
                    shipmentId: row[0],
                    fromRegionId: s[row[1]],
                    toRegionId: s[row[2]],
                    amount: row[3],
                    sentTick: row[4],
                    arriveTick: row[5],
                    route: row[6]
                };
            }
            o.shipments = out;

            src = o.energyShipments;
            out = new Array(src.length);
            for (i = 0; i < src.length; i++) {
                row = src[i];
                out[i] = {
                    shipmentId: row[0],
                    fromRegionId: s[row[1]],
                    toRegionId: s[row[2]],
                    amount: row[3],
                    sentTick: row[4],
                    arriveTick: row[5],
                    route: row[6]
                };
            }
            o.energyShipments = out;

            src = o.controlPoints;
            out = new Array(src.length);
            for (i = 0; i < src.length; i++) {
                row = src[i];
                out[i] = {
                    id: s[row[0]],
                    regionId: s[row[1]],
                    x: row[2],
                    y: row[3],
                    radiusMm: row[4],
                    holdTicksRequired: row[5],
                    holdingCivilizationId: row[6] < 0 ? null : s[row[6]],
                    holdTicks: row[7],
                    ownerCivilizationId: row[8] < 0 ? null : s[row[8]]
                };
            }
            o.controlPoints = out;

            if (o.match !== null) {
                o.match = {
                    capturedPointsToWin: o.match[0],
                    winnerCivilizationId: o.match[1] < 0 ? null : s[o.match[1]]
                };
            }

            src = o.treaties;
            out = new Array(src.length);
            for (i = 0; i < src.length; i++) {
                row = src[i];
                out[i] = {
                    treatyId: row[0],
                    kind: kinds[row[1]],
                    otherCivilizationId: s[row[2]],
                    signedTick: row[3]
                };
            }
            o.treaties = out;

            src = o.treatyProposals;
            out = new Array(src.length);
            for (i = 0; i < src.length; i++) {
                row = src[i];
                out[i] = {
                    proposalId: row[0],
                    kind: kinds[row[1]],
                    proposerCivilizationId: s[row[2]],
                    targetCivilizationId: s[row[3]],
                    proposedTick: row[4],
                    expiresTick: row[5]
                };
            }
            o.treatyProposals = out;

            src = o.tradeOffers;
            out = new Array(src.length);
            for (i = 0; i < src.length; i++) {
                row = src[i];
                out[i] = {
                    tradeOfferId: row[0],
                    proposerCivilizationId: s[row[1]],
                    targetCivilizationId: s[row[2]],
                    regionId: s[row[3]],
                    giveMaterial: row[4],
                    giveEnergy: row[5],
                    wantMaterial: row[6],
                    wantEnergy: row[7],
                    proposedTick: row[8],
                    expiresTick: row[9],
                    negotiationId: row[10],
                    round: row[11]
                };
            }
            o.tradeOffers = out;

            src = o.deposits;
            out = new Array(src.length);
            for (i = 0; i < src.length; i++) {
                row = src[i];
                out[i] = {
                    depositId: row[0],
                    regionId: s[row[1]],
                    kind: ores[row[2]],
                    x: row[3],
                    y: row[4],
                    radiusMm: row[5],
                    remaining: row[6],
                    yieldPerTick: row[7]
                };
            }
            o.deposits = out;

            delete o.s;
            return o;
        }
        """;
}
