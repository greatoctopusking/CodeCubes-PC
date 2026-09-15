# CodeCubes-PC — 2D 版

CodeCubes 的**桌面 2D 版**，逻辑上完整复刻 VR 原项目
（`D:\GithubRepositories\CodeCubes`，Unity 2022.3.13f1c1 + XR Interaction Toolkit）。

- **关卡配置不变**：12 关的机器人起点 / 朝向 / 星星坐标 / 每关可用块与数量上限，逐字段抄自原项目
  `Assets/LevelDate/Level 1..12.asset` 与 `Assets/Resources/CodeBlockCatalog.asset`
- **玩法不变**：链表解释器、`While` / `IF` / `Else` 结构、条件块、离网不移动、无限循环检测、
  每关一次运行、`Retry (Keep)` / `Retry (Clear)`、固定总量循环池
- **交互改为鼠标**：左键拖动块、右键选中并连线、滚轮缩放、中键平移
- **零外部依赖**：全部 UI 与图形由运行时代码生成，不使用任何美术资源与音频

---

## 一、怎么运行

1. 用 **Unity 2022.3.13f1c1** 打开 `D:\GithubRepositories\CodeCubes-PC`
2. 打开模板自带的 `Assets/Scenes/SampleScene.unity`（不需要改任何东西）
3. 按 **Play**

就这些。场景里不需要拖任何物体：`GameBootstrap` 用
`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 在场景加载后自动创建 EventSystem、
Canvas 与全部界面。打包时同样不需要配置场景内容。

**建议**：Game 视图用 **16:9**（参考分辨率 1920×1080）。布局按 16:9 设计，
其它比例由 `CanvasScaler` 自适应，但上下分区比例以 16:9 最准。

启动后 Console 会打印一行自检结果：

```
[CodeCubesPC] 关卡自检通过：12 关，坐标 / 星星 / 可用块全部合法。
```

如果出现红色报错，说明关卡数据或数量规则被改坏了。自检覆盖三类问题：
关卡坐标（越界 / 星星重复 / 起点压在星星上）、可用块计算是否为空、
以及**设计意图断言**（各阶段该被限制成几个块，见第四节）。

---

## 二、界面与操作

```
┌──────────────────────────────────────────────────────────────┬────────────────┐
│ Level 3   Stars 1 / 1                        [ Run ] [ Leave ]│                │
│                                                              │  Suggested     │
│                    ┌────────────────────────┐                │  Blocks        │
│                    │   10 × 10 棋盘          │                │                │
│                    │   机器人 = 蓝圆 + 朝向箭头│                │  Start ·       │
│                    │   星星   = 黄色五角星    │                │  MoveForward · │
│                    └────────────────────────┘                │  TurnLeft ·    │
│                                                              │  TurnRight     │
├───────────────────────────┬──────────────────────────────────┴────────────────┤
│ Blocks                    │  ← 程序区（可自由摆放 / 缩放 / 平移）              │
│                           │                                                  │
│ [MoveForward     15 / 15] │   ┌─────────┐    ┌─────────────┐     ┌───────┐    │
│ [TurnLeft        15 / 15] │   │  Start  │───▶│ MoveForward │────▶│ ...   │    │
│ [TurnRight       15 / 15] │   └─────────┘    └─────────────┘     └───────┘    │
│                           │                                          ┌───────┐ │
│                           │  Left-drag… Wheel… Middle-drag…          │ Trash │ │
└───────────────────────────┴──────────────────────────────────────────└───────┘─┘
```

| 操作 | 作用 | 对应 VR 原版 |
|------|------|--------------|
| **左键拖动块** | 移动程序区里的块；从左侧库存行上拖出则生成新块 | 右握柄抓取 / 移动 |
| **右键单击块** | 选中（高亮黄）→ 再右键另一个块 = 连 `next`（黄线）；点自己或空白 = 断线 | 右扳机选中 / 连线 / 断开 |
| **右键单击 While / IF 后再右键条件块** | 连条件（绿线）；点同一个条件块 = 断开 | 同上（Judger） |
| **滚轮** | 程序区缩放（以光标为锚点）；在左下库存上则滚动列表 | — |
| **中键拖动** | 程序区平移 | — |
| **Reset View 按钮** | 缩放 / 平移复位 | — |
| **把块拖进垃圾桶** | 归还到左侧库存（块不被销毁） | 垃圾桶 |
| **Run 按钮** | 运行程序 | 左扳机 |

---

## 三、代码结构

```
Assets/Scripts/
├── Core/                          ← 与原项目一一对应的纯逻辑，不含 UI
│   ├── BlockType.cs               15 种块 + 分类 / 颜色（原 15 个 Code 子类）
│   ├── BlockCatalog.cs            Catalog：Start 1 个、其余各 15 个（原 CodeBlockCatalog.asset）
│   ├── RobotDirection.cs          朝向枚举 + 转向 / 朝向向量（原 RobotDirection + TurnXxxCode）
│   ├── LevelData.cs               关卡数据结构（原 LevelData ScriptableObject）
│   ├── LevelLibrary.cs            12 关硬编码数据 + 启动自检 ValidateAll()
│   ├── LevelBlockHints.cs         逐行移植（可用类型 / 数量上限 / 强制结构）
│   ├── LevelPolicy.cs             min(Catalog, GetCopyCap) → 本关每种可用数量
│   ├── CodeValidator.cs           逐行移植（报错文案逐字一致）
│   ├── CodeBlock.cs               一个类承担原 15 个块的 work()，语义逐分支对应
│   ├── CodeExecutor.cs            逐行移植的解释器（含无限循环检测）
│   ├── LevelManager.cs            关卡状态 / 机器人移动插值 / 星星（原 LevelManager + MoveCode）
│   ├── BlockPool.cs               固定总量库存（原 CodeBlockBoard 槽位池）
│   └── ConnectionManager.cs       逐行移植的连线状态机（原 ConnectionManager）
└── UI/                            ← 全部运行时生成
    ├── Palette.cs                 配色
    ├── SpriteFactory.cs           程序生成圆角矩形 / 圆 / 五角星 / 三角形
    ├── UIFactory.cs               控件工厂 + 内置字体
    ├── UILineGraphic.cs           自绘带箭头的折线（连线用）
    ├── BlockView.cs               单个块的视觉与端口位置
    ├── BoardView.cs               上半屏：顶栏 / 棋盘 / 机器人 / 星星 / 提示面板
    ├── InventoryView.cs           左下：块列表 + 剩余数量
    ├── WorkspaceView.cs           右下：程序区（缩放平移）+ 连线层 + 垃圾桶
    ├── MenuView.cs                标题 / 选关 / 完成 / 失败
    ├── GameController.cs          流程编排 + 鼠标输入路由
    └── GameBootstrap.cs           零配置启动
```

**为什么输入集中在 `GameController`**：块的拖拽、右键命中、垃圾桶、自绘连线都在同一个矩形区域里，
如果各自注册 uGUI 指针事件会互相抢事件（尤其自绘线会挡住块）。因此所有非按钮控件的
`raycastTarget` 一律为 `false`，只有 Button 接收射线，`GameController` 用
`EventSystem.IsPointerOverGameObject()` 判断「鼠标是否压在按钮上」后让行。

---

## 四、与原版的一致性

### 已逐一核对并保持不变

| 项目 | 原版 | 2D 版 |
|------|------|-------|
| 12 关数据 | `Level 1..12.asset` | `LevelLibrary.cs` 逐字段一致 |
| Catalog | Start 1、其余 14 种各 15（合计 211） | `BlockCatalog.cs` 一致 |
| 网格 | 10×10，`x` 东 `y` 北 | 同（UI 里 y 向上，天然对应） |
| 朝向 | 0=北 1=东 2=南 3=西 | 同 |
| 左转 / 右转 | `Rotate(0,∓90,0)` | 枚举 ±1（已推导验证等价） |
| 前进 | 每格 0.5 秒（speed 2 / cellSize 1） | 同（`moveSpeed` 可调） |
| 越界前进 | 不移动，直接完成，**不判失败** | 同 |
| 星星收集 | 走进格子即收集 | 移动结束时按 0.4 半径判定 |
| 探测块 | 前方一格 0.4 内有无未收集星，**不比较 orderIndex** | 同 |
| `StarRemain` | 场上是否还有星 | 同 |
| 解释器 | `next` 链 + `Judger` + WhileEnd/IfEnd/Else 配对 | 逐行移植，分支顺序一致 |
| 无限循环 | 8000 步 + `块ID\|x\|y\|朝向\|星数` 去重 | 同 |
| 校验器 | 6 项检查，文案逐字一致 | 同 |
| 教学关强制结构 | L7 必须含 IF；L6/8/9 必须含 While | 同 |
| 每关一次运行 | Run 后锁定 | 同（Run 按钮置灰 + `Running...`） |
| 失败重试 | `Retry (Keep)` / `Retry (Clear)` | 同 |
| 总量守恒 | 抓出后不补货、归还到空位、满则拒绝 | 同 |
| 运行中垃圾桶 | 不可用 | 同（垃圾桶变灰 + 拒绝） |
| 运行中连线 | 禁止 | 同 |
| `True` / `False` | 不在任何一关出现 | 同（任何关卡都拿不到） |
| Start | 全关唯一，每关自动放到工作区 | 同（固定锚点，不占库存） |
| 菜单文案 | `Coding Blocks` / `Level N Complete!` / `Congratulations!…` | 同 |

### 有意偏离原版的地方（共 6 处，均不影响规则）

1. **按 Run 不再有 4.8 秒开机动画** —— 按你的决定，立即执行第一步。
2. **朝向箭头改为「变暗」而不是「隐藏」** —— 原版运行时隐藏它，因为 VR 里 3D 机器人模型
   本身能看出朝向；2D 版机器人是一个圆，藏掉箭头就完全看不出朝向，会直接破坏「转向」的教学。
   因此运行时透明度降到 0.25，结算时恢复。
3. **不做音频** —— 原版 `AudioManager` 的全部调用点未移植（你选的是「暂不做音效」）。
4. **占位图形** —— 全部由 `SpriteFactory` 程序生成，无美术资源。
5. **空间模型** —— 原版是 3D 物理世界（刚体、抓取、触发器）；2D 版改为
   「格单位坐标 + 插值动画」，并把物理触发器换成坐标判定。规则等价，手感不同。
6. **不靠 `FindObjectsOfType`** —— 原版到处用 `FindObjectsOfType<Code>()`；2D 版改为在
   工作区登记表里遍历（`ConnectionManager.Register/Unregister`），避免每帧全场景搜索，
   也让「哪些块算程序区里的块」有明确边界。

### 一个重要的数据修正（与 `PLAN.md` 冲突，以实际资产为准）

原项目 `PLAN.md` 写的是「15 种各 3 个、合计 45」，但实际
`Assets/Resources/CodeBlockCatalog.asset` 是：

```
Start         maxCount = 1
其余 14 种块   maxCount = 15
合计          1 + 14 × 15 = 211
```

这个差别**不是细节**：Level 4 的参考解需要 6 个 `MoveForward`、Level 5 需要 7 个，
Level 11 每边需要 4 个。如果按 3 计算，第 4、5 关直接无解。
本项目的 `BlockCatalog.cs` 以**实际资产**为准，并在 `LevelLibrary.ValidateAll()` 里加了断言，
一旦数量规则被改成错的，启动时就会报错而不是等到玩到第 4 关才发现。

### 每关的可用块数量

计算方式：`min(Catalog.maxCount, LevelBlockHints.GetCopyCap(type, suggested))`
（与原 `CodeBlockBoard.ApplyAvailableBlocks` 的槽位启用逻辑等价）。

| 关 | 可用块（数量） | 合计 |
|----|----------------|------|
| 1 | MoveForward 15 | 15 |
| 2 | MoveForward 15、TurnLeft 15 | 30 |
| 3–5 | MoveForward 15、TurnLeft 15、TurnRight 15 | 45 |
| 6 | MoveForward **1**、While 2、WhileEnd 2、StarRemain 1 | 6 |
| 7 | MoveForward **1**、TurnLeft **1**、IF 2、IfEnd 2、DetectLeftStar 1 | 7 |
| 8 | MoveForward **1**、While 2、WhileEnd 2、DetectFrontStar 1 | 6 |
| 9 | MoveForward 2、TurnLeft 2、While 2、WhileEnd 2、DetectFrontStar 2 | 10 |
| 10–12 | MoveForward 4、TurnLeft 4、TurnRight 4、While 2、WhileEnd 2、IF 2、IfEnd 2、Else 2、四个探测块各 1 | 26 |

- 第 1–5 关 Catalog 上限是 15，而参考解最多只要 7 个，所以实际是「管够」（对应设计文档的「数量按 Catalog」）。
- 第 6/7/8 关只给 **1** 个 `MoveForward`，就是为了逼出循环 / 分支，不能铺一长串前进。
- 第 10–12 关每种移动块**恰好 4 个**，而阶梯要 8 步、螺旋要 23 步，因此必须用循环。
- `True` / `False` 不在任何一关的 `suggestedBlockNames` 里，所以任何关卡都拿不到
  （与原版一致，避免 `While True` 一类投机解）。

---

## 五、两个需要你拍板的地方

这两处我**按原版行为实现了**，但 2D 的操作方式会让它们比 VR 里更容易撞上：

1. **空程序 / 未连接的程序按 Run = 直接判失败（且用掉本关唯一一次机会）。**
   原项目的 `FindProgramStart()` 即使 Start 没有任何出线也会返回它，校验器对
   「只有 Start 一块」不报错，于是程序立刻跑完 → 没集齐星星 → 失败面板。
   2D 里点 Run 比 VR 里扣扳机容易得多，误触代价较大。
   **如需改成「Start 没有任何连接时拦截并提示」，说一声即可。**
2. **离网（机器人朝格子外）前进不判失败，只是原地不动。** 这是原版行为，我保留了；
   代价是玩家可能看不出「为什么没动」。可以加一句状态提示，但那是新增行为。

完整验收步骤见 **`VERIFICATION.md`**。
