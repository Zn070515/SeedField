## Summary

<!-- 这次 PR 做了什么，为什么需要它。 -->

## Decision / scope

- 关联决策 ID：<!-- P-01 / T-02 / G-10 ... -->
- 本次范围：
- 明确非目标：

## Changes

-

## Verification

```text
dotnet build SeedField.slnx
dotnet test SeedField.slnx
```

实际结果：

- [ ] 构建通过
- [ ] 测试通过（数量：____）
- [ ] 涉及仿真状态时，固定 Tick 哈希可复现
- [ ] 涉及 Godot 时，附真实截图/录屏路径
- [ ] 涉及存档/蓝图 schema 时，附迁移或兼容性说明

## Risks

<!-- 未验证项、已知缺口、回滚方式。 -->

## Checklist

- [ ] 没有提交密钥、日志、`bin/`、`obj/` 或本地存档
- [ ] Godot 层没有修改 `WorldState` 或自行结算战斗
- [ ] 没有把 `[PROPOSED]` 参数写成已确认规则
- [ ] 文档/证据已更新
