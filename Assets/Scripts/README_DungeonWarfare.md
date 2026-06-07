# 地牢战争 / 塔防 — 2D 原型

经典塔防最小可玩原型（红警式布局）：画面锁定 16:9，**右侧 2/16 为侧边栏**（暂留空），
左侧 14:9 为 **28×18 整张网格地图**。敌人（红色圆形）沿网格里的**蛇形道路**行进，
玩家在空地格子上建造炮台（蓝色方形），炮台自动瞄准射程内的敌人并发射子弹。
敌人逃到终点扣生命，生命归零则 Game Over。

布局/比例由 `Core/GameViewportLayout.cs` 控制（16:9 锁定 + 左右分栏，挂在主相机上）。

## 一键生成可玩场景

1. 在 Unity 编辑器里等脚本编译完成（Console 无报错）。
2. 顶部菜单：**Tools → Dungeon Warfare → Build Demo Scene**
3. 自动生成：
   - 占位贴图 `Assets/Art/WhiteSquare.png`、`WhiteCircle.png`
   - 预制体 `Assets/Resources/Enemy.prefab`、`Tower.prefab`、`Projectile.prefab`
   - 场景 `Assets/Scenes/DungeonWarfare.unity`（含相机/地面/网格/路径/系统/HUD，并加入 Build Settings）
4. 打开该场景，点 **Play**。
5. 流程：**主菜单 → 开始游戏 → 第 1 关**。进入关卡后，鼠标移到网格：**绿色幽灵 = 可建造，红色 = 不可（路上/已占/钱不够）**，**左键**建造炮台（每个 30 金币）。
6. 撑过 **5 波**（每波血量 ×1.5）= 胜利；生命归零 = 失败。右侧栏显示当前波次与剩余敌人。

> 预制体放在 `Resources/` 下，运行时会自动加载兜底；`game/path/grid/相机` 也都加了自动查找，
> 所以即使 Inspector 引用没连上也能正常运行，不用手动拖拽。
>
> 输入系统为「新版 Input System only」，代码已用 `Mouse.current`，不要改回旧版 `Input`。

## 形状与尺寸约定

- **敌人 = 正圆**（`CircleCollider2D`），**炮台 = 正方形**（`BoxCollider2D`）。
- 两者用同一常量 `UnitSize` 缩放，且基础贴图都是 1 世界单位，
  因此**正方形边长 == 圆形直径**（几何与碰撞体都相等）。

## 代码结构

```
Assets/Scripts/
├─ Core/
│  ├─ Health.cs            # 通用血量 + IDamageable，死亡/受伤事件
│  ├─ GameManager.cs       # 金币、生命（纯数值，单例）
│  ├─ GameFlow.cs          # 状态机：主菜单/选关/游戏中/胜利/失败（单例）
│  └─ GameViewportLayout.cs# 16:9 锁定 + 左主画面 / 右侧边栏分栏
├─ Level/
│  └─ GridSystem.cs        # 网格 + 动态BFS寻路 + 路障(blocked) + 可达性校验 + 瓦片
├─ Enemies/
│  ├─ PathFollower.cs      # 按网格动态寻路，路障变化时实时重算路线
│  ├─ Enemy.cs             # 死亡给金币、逃脱扣生命、Despawned 事件、占格查询
│  └─ WaveManager.cs       # 5 波，每波血量 ×1.5，清空才进下一波
├─ Towers/
│  ├─ Tower.cs             # 范围内自动选最近敌人、定时开火
│  ├─ Projectile.cs        # 飞向目标、命中造成伤害
│  ├─ Terrain.cs           # 地形：铺在路上挡路，可在其上建炮塔，无攻击，仅造价
│  └─ GridPlacer.cs        # 炮塔/地形两种建造 + 绿/红预览 + 地形校验(不压敌/不堵死)
├─ UI/
│  └─ GameUI.cs            # IMGUI：菜单/选关/HUD/侧边栏波次/胜负界面
└─ Editor/
   └─ DungeonWarfareSceneBuilder.cs  # 一键生成预制体与场景
```

## 关键设计

- **网格放置**：`GridSystem` 负责世界↔格子坐标、边界、占用记录；`GridPlacer` 做吸附、
  合法性校验（在网格内 + 未占用）、绿/红幽灵预览、扣金币、占格。
- **远程攻击**：`Tower` 用 `Physics2D.OverlapCircleAll` 在射程内找最近敌人（按 `Enemy` 组件过滤），
  定时实例化 `Projectile` 飞向目标造成伤害。敌人碰撞体是 Trigger，默认能被 Overlap 查询命中。
- **解耦**：伤害统一走 `Health.TakeDamage`；新炮台/新敌人只需复用这些组件。

## 默认数值（都可在 Inspector 调）

| 项 | 值 |
|----|----|
| 起始金币 / 生命 | 100 / 20 |
| 炮台造价 / 射程 / 射速 / 伤害 | 30 / 4 / 0.6s / 10 |
| 敌人 基础血量 / 速度 / 击杀奖励 | 30 / 2 / 5 |
| 波次数 / 每波数量 / 每波血量倍率 | 5 / 10 / ×1.5 |
| 网格 列×行 / 格子大小 | 28×18 / 0.5 |

波次血量：第 N 波 = 基础血量 × 1.5^(N-1)，即 30 / 45 / 67.5 / 101 / 152。

## 后续可扩展方向

1. **拆除/卖出炮台**：右键点格子，`GridSystem.Free(cell)` 释放并返还部分金币。
2. **多种炮台**：减速塔、范围伤害（AOE）、穿透子弹——用 ScriptableObject 配置数值。
3. **目标优先级**：当前选「最近」，可改为「最靠近终点」（沿路径进度最高）。
4. **波次数据资产**：把 `WaveManager` 内联参数换成可配置的波次资产（数量/间隔/敌人类型）。
5. **血条与命中反馈**：敌人头顶血条、受击闪白、死亡特效、击中音效。
6. **正式 UI**：用 uGUI / UI Toolkit 替换 `GameUI.cs` 的 IMGUI，并把信息排进右侧栏。
7. **侧边栏功能化**：在右侧栏放金币/生命、炮台选择按钮、开始下一波按钮。
```
