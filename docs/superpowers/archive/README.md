# Superpowers 归档

这里保存**已完成、已被 ADR 取代或不再作为执行入口**的 spec / plan。

- 活跃计划仍放在 `docs/superpowers/plans/`，活跃设计放在 `docs/superpowers/specs/`。
- 归档文件只用于追溯当时的范围、验证与回滚记录，**不要按其中的步骤重新执行**。
- 归档计划不参与 `DocumentationTests.PlansHaveVerificationRollbackAndNoPlaceholders` 门禁；
  活跃计划仍必须包含 `Verification` / `Rollback` 且无 TBD/TODO。

## 2026-11-17

| 文件 | 类型 | 结果 | 关联 |
|---|---|---|---|
| [parallel-controller-batch](2026-11-17/plans/2026-11-17-parallel-controller-batch.md) | Plan | 已实现并合并 | ADR-0028 |
| [parallel-controller-hotpath-gates](2026-11-17/plans/2026-11-17-parallel-controller-hotpath-gates.md) | Plan | 已实现并合并 | ADR-0028、性能证据 |
| [phase1-worker-isolation](2026-11-17/plans/2026-11-17-phase1-worker-isolation.md) | Plan | 已实现并合并 | ADR-0021 |
| [phase2-package-trust](2026-11-17/plans/2026-11-17-phase2-package-trust.md) | Plan | 已实现并合并 | ADR-0022 |
| [parallel-controller-batch-design](2026-11-17/specs/2026-11-17-parallel-controller-batch-design.md) | Spec | 已被实现取代 | ADR-0028 |
