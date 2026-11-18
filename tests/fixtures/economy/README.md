# Economy golden fixtures

Implementation-independent business examples used by `EconomyGoldenFixtureTests`.

## `smelting-recipes.v1.json`

- `schemaVersion`: fixture schema, independent of the world save schema.
- `smelterOrePerTick`: the smelter module rate used by the case.
- `cases[]`: one recipe per ore kind:
  - rule fields (`orePerBatch`, `materialPerBatch`, `byproduct`, `byproductPerBatch`)
    must match `SmeltingRecipes.For(kind)`;
  - `inputOre` is the regional stock before one tick;
  - `expectedBatches` / `expectedMaterial` / `expectedByproduct` are the observable
    results after one tick.

## Change discipline

- If a fixture fails and the **rules** changed intentionally, update the JSON in the
  same commit and explain why in the commit message/ADR.
- If only the **implementation** changed, the fixture must keep passing; do not
  rewrite expected values to hide a regression.
- Add `v2` files rather than editing `v1` silently when the semantics change.
