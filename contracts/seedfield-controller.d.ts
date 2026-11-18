// SeedField controller contract.
// This file mirrors the JSON handed to decide(observation) and the intents
// returned from it. It is editor-only; the runtime does not compile TypeScript.

export type TreatyKind = "NonAggression" | "Trade";

export interface ObservedEntity {
  entityId: number;
  civilizationId: string;
  regionId: string;
  x: number;
  y: number;
  health: number;
  maxHealth: number;
  isSelf: boolean;
  hasSensor: boolean;
  sensorRangeMm: number;
  hasWeapon: boolean;
  weaponRangeMm: number;
  hasDrive: boolean;
  hasComm: boolean;
}

export interface ObservedMessage {
  messageId: number;
  fromEntityId: number;
  fromCivilizationId: string;
  targetRegionId: string;
  topic: string;
  payload: string;
  sentTick: number;
  deliveredTick: number;
}

export interface ObservedShipment {
  shipmentId: number;
  fromRegionId: string;
  toRegionId: string;
  amount: number;
  sentTick: number;
  arriveTick: number;
  route: string;
}

export interface ObservedEnergyShipment {
  shipmentId: number;
  fromRegionId: string;
  toRegionId: string;
  amount: number;
  sentTick: number;
  arriveTick: number;
  route: string;
}

export interface ObservedControlPoint {
  id: string;
  regionId: string;
  x: number;
  y: number;
  radiusMm: number;
  holdTicksRequired: number;
  holdingCivilizationId: string | null;
  holdTicks: number;
  ownerCivilizationId: string | null;
}

export interface ObservedMatch {
  capturedPointsToWin: number;
  winnerCivilizationId: string | null;
}

export interface ObservedTreaty {
  treatyId: number;
  kind: TreatyKind;
  otherCivilizationId: string;
  signedTick: number;
}

export interface ObservedTreatyProposal {
  proposalId: number;
  kind: TreatyKind;
  proposerCivilizationId: string;
  targetCivilizationId: string;
  proposedTick: number;
  expiresTick: number;
}

export interface ObservedTradeOffer {
  tradeOfferId: number;
  proposerCivilizationId: string;
  targetCivilizationId: string;
  regionId: string;
  giveMaterial: number;
  giveEnergy: number;
  wantMaterial: number;
  wantEnergy: number;
  proposedTick: number;
  expiresTick: number;
  negotiationId: number;
  round: number;
}

export interface Observation {
  tick: number;
  civilizationId: string;
  entities: ObservedEntity[];
  messages: ObservedMessage[];
  shipments: ObservedShipment[];
  energyShipments: ObservedEnergyShipment[];
  viewerEntityId: number | null;
  controlPoints: ObservedControlPoint[];
  match: ObservedMatch | null;
  treaties: ObservedTreaty[];
  treatyProposals: ObservedTreatyProposal[];
  tradeOffers: ObservedTradeOffer[];
}

export interface MoveIntent {
  type: "move";
  entityId: number;
  target: { x: number; y: number };
}

export interface AttackIntent {
  type: "attack";
  entityId: number;
  targetId: number;
}

export interface SendMessageIntent {
  type: "send";
  entityId: number;
  toCivilizationId: string;
  topic: string;
  payload?: string;
  targetRegionId?: string;
}

export interface WaitIntent {
  type: "wait";
  entityId: number;
}

export interface MineIntent {
  type: "mine";
  entityId: number;
}

export interface TransferMaterialIntent {
  type: "transfer";
  entityId: number;
  targetRegionId: string;
  amount: number;
}

export interface SendEnergyIntent {
  type: "sendEnergy";
  entityId: number;
  amount: number;
  targetRegionId?: string;
}

export interface DrawEnergyIntent {
  type: "drawEnergy";
  entityId: number;
  amount: number;
}

export type Intent =
  | MoveIntent
  | AttackIntent
  | SendMessageIntent
  | WaitIntent
  | MineIntent
  | TransferMaterialIntent
  | SendEnergyIntent
  | DrawEnergyIntent;

export type Decide = (observation: Observation) => Intent[];
