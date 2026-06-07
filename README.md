# Dungeon Warfare — 2D 网格塔防 (Unity)

一个用 Unity 6 (URP) 做的 2D 网格塔防原型：敌人沿 ASCII 定义的地图自动寻路（BFS），
玩家在网格上建造**炮塔**攻击、铺设**地形**改变敌人路线（迷宫塔防）。全程占位图形
（方块/圆形），重点在玩法系统。

## 玩法

- 主菜单 → 选关 → 8 波敌人（每波血量递增）→ 胜利/失败
- **炮塔**：建在空地或地形上，自动瞄准射程内敌人开火
- **地形**：铺在路上，敌人实时绕行；可在地形上再建炮塔
- 点已建建筑可**拆除**（返还 60%）
- **空格**快速暂停，**ESC** 暂停菜单（重新开始 / 回主菜单 / 退出）
- 画面锁定 16:9，右侧为信息/建造栏

## 运行 / 开发

- Unity 版本：**6000.4.10f1**（Unity 6，URP）
- 打开工程后，菜单 **`Tools → Dungeon Warfare → Build Demo Scene`** 一键生成可玩场景，
  打开 `Assets/Scenes/DungeonWarfare.unity`，按 ▶ Play。
- 打包 Windows exe：菜单 **`Tools → Dungeon Warfare → Build Windows EXE`**（输出到 `Build/Windows/`）。
- 改地图：编辑 `Assets/Scripts/Editor/DungeonWarfareSceneBuilder.cs` 里的 `MapRows`
  （`#`=路, `.`=空地, `E`=入口, `X`=出口），重新生成即可。

详细设计见 [`Assets/Scripts/README_DungeonWarfare.md`](Assets/Scripts/README_DungeonWarfare.md)。

## 代码结构

`Assets/Scripts/` 下分 `Core`（流程/经济/血量/视口）、`Level`（网格+寻路）、
`Enemies`（敌人/波次）、`Towers`（炮塔/地形/子弹/放置）、`UI`、`Editor`（一键生成/打包）。
