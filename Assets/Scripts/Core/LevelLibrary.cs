using System.Collections.Generic;
using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>
    /// 12 关的关卡配置，逐字段抄自原项目 Assets/LevelDate/Level 1.asset ~ Level 12.asset。
    /// 不要凭设计文档修改本文件：文档（Assets/UpdatePlan/关卡设计与编程学习路径.md）与
    /// 实际资源在个别细节上不一致，本文件以 .asset 资源为准。
    ///
    /// 已核对（与 .asset 完全一致）：
    ///   所有关卡 gridSize = 10×10；robotFacing 除 Level 7 为 North 外均为 East；
    ///   starPositions 数组顺序即原项目 Star.orderIndex 顺序。
    /// </summary>
    public static class LevelLibrary
    {
        /// <summary>关卡数量（原 LevelManager.levels 列表为 12 项，顺序即 1→12）。</summary>
        public const int LevelCount = 12;

        public static LevelData[] CreateAll()
        {
            return new[]
            {
                // Level 1 — 第一次前进。Start；MoveForward
                Make(1, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(1, 0),
                    BlockType.Start, BlockType.MoveForward),

                // Level 2 — 第一次转向。
                Make(2, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(0, 1),
                    BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft),

                // Level 3 — 前进与转向组合。
                Make(3, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(2, 1),
                    BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft, BlockType.TurnRight),

                // Level 4 — 多星 U 形。参考解需要 6 个 MoveForward、2 次左转。
                Make(4, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(2, 0, 2, 2, 0, 2),
                    BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft, BlockType.TurnRight),

                // Level 5 — 折线加一阶。参考解需要 7 个 MoveForward。
                Make(5, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(1, 0, 1, 2, 3, 2, 4, 3),
                    BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft, BlockType.TurnRight),

                // Level 6 — 第一次循环：While + StarRemain（正东 6 星直线）。只给 1 个 MoveForward。
                Make(6, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0),
                    BlockType.Start, BlockType.MoveForward, BlockType.While, BlockType.WhileEnd,
                    BlockType.StarRemain),

                // Level 7 — 第一次分支：朝北，星在左。校验要求程序必须含 IF。只给 1 个 MoveForward。
                Make(7, new Vector2Int(2, 0), RobotDirection.North,
                    Cells(1, 0),
                    BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft, BlockType.If,
                    BlockType.IfEnd, BlockType.DetectLeftStar),

                // Level 8 — 与 Level 6 同图，条件换成 DetectFrontStar。只给 1 个 MoveForward。
                Make(8, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0),
                    BlockType.Start, BlockType.MoveForward, BlockType.While, BlockType.WhileEnd,
                    BlockType.DetectFrontStar),

                // Level 9 — 循环里转弯（连续 L 形 10 星）。两段循环各要一块 MoveForward 与探测块。
                Make(9, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 5, 1, 5, 2, 5, 3, 5, 4, 5, 5),
                    BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft, BlockType.While,
                    BlockType.WhileEnd, BlockType.DetectFrontStar),

                // Level 10 — 阶梯：完整工具集，每种移动块恰好 4 个。
                Make(10, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(1, 0, 2, 0, 2, 1, 3, 1, 3, 2, 4, 2, 4, 3, 5, 3),
                    LevelBlockHints.FullToolkit),

                // Level 11 — 稀疏矩形：定长重复，每边 4 个 MoveForward。
                Make(11, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(4, 0, 4, 4, 0, 4, 0, 1),
                    LevelBlockHints.FullToolkit),

                // Level 12 — 毕业：向内螺旋 23 星。
                Make(12, new Vector2Int(0, 0), RobotDirection.East,
                    Cells(1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0,
                          6, 1, 6, 2, 6, 3, 6, 4, 6, 5,
                          5, 5, 4, 5, 3, 5, 2, 5, 1, 5,
                          1, 4, 1, 3, 1, 2,
                          2, 2, 3, 2, 4, 2,
                          4, 3),
                    LevelBlockHints.FullToolkit)
            };
        }

        private static LevelData Make(int levelNumber, Vector2Int robotStart, RobotDirection facing,
            Vector2Int[] stars, params BlockType[] suggested)
        {
            var data = new LevelData();
            data.levelNumber = levelNumber;
            data.gridSize = new Vector2Int(10, 10);
            data.robotStart = robotStart;
            data.robotFacing = facing;
            data.starPositions = stars;
            data.suggestedBlockNames = suggested;
            return data;
        }

        /// <summary>把「x1,y1, x2,y2, ...」压平成 Vector2Int[]，减少手写坐标的笔误。</summary>
        private static Vector2Int[] Cells(params int[] xyPairs)
        {
            if (xyPairs == null || xyPairs.Length == 0)
                return new Vector2Int[0];

            int count = xyPairs.Length / 2;
            var result = new Vector2Int[count];
            for (int i = 0; i < count; i++)
                result[i] = new Vector2Int(xyPairs[i * 2], xyPairs[i * 2 + 1]);

            return result;
        }

        /// <summary>
        /// 启动时自检。返回问题描述列表，空列表表示全部通过。
        /// 覆盖三类问题：
        ///   1) 关卡数据本身（越界、星星重复、起点压在星星上）
        ///   2) 可用块计算（LevelPolicy 是否给出非空结果）
        ///   3) 设计意图断言（各阶段该被限制成几个块）—— 这一项专门用来防住
        ///      Catalog maxCount / GetCopyCap 被改动后导致的关卡无解
        /// </summary>
        public static List<string> ValidateAll()
        {
            var problems = new List<string>();
            LevelData[] levels = CreateAll();

            if (levels.Length != LevelCount)
                problems.Add(string.Format("关卡数量为 {0}，应为 {1}", levels.Length, LevelCount));

            for (int i = 0; i < levels.Length; i++)
            {
                LevelData level = levels[i];
                string tag = string.Format("Level {0}", level.levelNumber);

                if (level.levelNumber != i + 1)
                    problems.Add(string.Format("{0}: levelNumber 应为 {1}", tag, i + 1));

                if (!InBounds(level, level.robotStart))
                    problems.Add(string.Format("{0}: 机器人起点 {1} 越界", tag, level.robotStart));

                if (level.starPositions == null || level.starPositions.Length == 0)
                {
                    problems.Add(string.Format("{0}: 没有任何星星", tag));
                    continue;
                }

                var seen = new HashSet<Vector2Int>();
                for (int s = 0; s < level.starPositions.Length; s++)
                {
                    Vector2Int cell = level.starPositions[s];
                    if (!InBounds(level, cell))
                        problems.Add(string.Format("{0}: 星星 {1} 越界", tag, cell));
                    if (!seen.Add(cell))
                        problems.Add(string.Format("{0}: 星星 {1} 重复", tag, cell));
                    if (cell == level.robotStart)
                        problems.Add(string.Format("{0}: 星星 {1} 与机器人起点重合", tag, cell));
                }

                BlockType[] suggested = level.GetSuggestedBlockNames();
                if (suggested == null || suggested.Length == 0)
                {
                    problems.Add(string.Format("{0}: suggestedBlockNames 为空", tag));
                    continue;
                }

                Dictionary<BlockType, int> supply = LevelPolicy.BuildSupply(suggested);
                if (supply.Count == 0)
                {
                    problems.Add(string.Format("{0}: 按 LevelPolicy 计算出的可用块为 0", tag));
                    continue;
                }

                CheckSupplyIntent(level, supply, problems, tag);
            }

            return problems;
        }

        /// <summary>
        /// 设计意图断言。这些数量是「逼学生用循环 / 分支」的关键，
        /// 也是各关参考解能否实现的下限，因此必须固定。
        /// </summary>
        private static void CheckSupplyIntent(LevelData level, Dictionary<BlockType, int> supply,
            List<string> problems, string tag)
        {
            int moveForward = SupplyOf(supply, BlockType.MoveForward);
            int turnLeft = SupplyOf(supply, BlockType.TurnLeft);
            int turnRight = SupplyOf(supply, BlockType.TurnRight);

            // 顺序关（1–5）：参考解最长的是 Level 5，需要 7 个 MoveForward。
            if (level.levelNumber <= 5 && moveForward < 7)
            {
                problems.Add(string.Format(
                    "{0}: 顺序关至少需要 7 个 MoveForward（Level 5 的参考解），当前只有 {1} 个",
                    tag, moveForward));
            }

            // 结构教学关（6–8）：一律只给 1 个移动块，逼出循环 / 分支。
            if (level.levelNumber >= 6 && level.levelNumber <= 8 && moveForward != 1)
            {
                problems.Add(string.Format(
                    "{0}: 教学关的 MoveForward 应恰好为 1，当前为 {1} 个", tag, moveForward));
            }

            // 综合关（10–12）：每种移动块恰好 4 个（8 步阶梯 / 23 步螺旋都铺不完）。
            if (level.levelNumber >= 10)
            {
                if (moveForward != 4 || turnLeft != 4 || turnRight != 4)
                {
                    problems.Add(string.Format(
                        "{0}: 综合关的移动块应各为 4（当前 MoveForward={1} TurnLeft={2} TurnRight={3}）",
                        tag, moveForward, turnLeft, turnRight));
                }
            }

            // 循环/分支结构块：教学关与综合关都应给出 2 套，保证能写两段循环。
            if (level.levelNumber >= 6)
            {
                int whileCount = SupplyOf(supply, BlockType.While);
                int ifCount = SupplyOf(supply, BlockType.If);
                if (LevelBlockHints.HasName(level.GetSuggestedBlockNames(), BlockType.While) && whileCount < 2)
                    problems.Add(string.Format("{0}: While 应至少有 2 个，当前 {1} 个", tag, whileCount));
                if (LevelBlockHints.HasName(level.GetSuggestedBlockNames(), BlockType.If) && ifCount < 2)
                    problems.Add(string.Format("{0}: IF 应至少有 2 个，当前 {1} 个", tag, ifCount));
            }
        }

        private static int SupplyOf(Dictionary<BlockType, int> supply, BlockType type)
        {
            int value;
            return supply.TryGetValue(type, out value) ? value : 0;
        }

        public static bool InBounds(LevelData level, Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < level.gridSize.x
                && cell.y >= 0 && cell.y < level.gridSize.y;
        }
    }
}
