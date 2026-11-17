import type { Intent, Observation } from "./seedfield-controller";

// Type-checked example for editor completion. The runtime executes JavaScript;
// this file exists so mistakes such as a misspelled observation field fail here.
export function decide(obs: Observation): Intent[] {
  const me = obs.entities.find((entity) => entity.entityId === obs.viewerEntityId);
  if (!me) {
    return [];
  }

  const enemy = obs.entities.find((entity) => !entity.isSelf);
  if (!enemy) {
    return [{ type: "move", entityId: me.entityId, target: { x: 10_000, y: 10_000 } }];
  }

  if (me.hasWeapon && obs.tick % 2 === 0) {
    return [{ type: "attack", entityId: me.entityId, targetId: enemy.entityId }];
  }

  return [{ type: "move", entityId: me.entityId, target: { x: enemy.x, y: enemy.y } }];
}
