using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>
    /// 与原项目 LevelData.cs 中的 RobotDirection 完全一致：0=北 1=东 2=南 3=西。
    /// 网格坐标约定：x 向东、y 向北（对应原项目的 world +X / +Z）。
    /// 朝向角度 (Unity Euler Y)：北 0°、东 90°、南 180°、西 270°，即索引 × 90。
    /// </summary>
    public enum RobotDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public static class RobotDirections
    {
        public static Vector2Int ToVector(RobotDirection direction)
        {
            switch (direction)
            {
                case RobotDirection.North: return new Vector2Int(0, 1);
                case RobotDirection.East: return new Vector2Int(1, 0);
                case RobotDirection.South: return new Vector2Int(0, -1);
                case RobotDirection.West: return new Vector2Int(-1, 0);
            }

            return new Vector2Int(0, 1);
        }

        /// <summary>
        /// 原项目 TurnLeftCode 执行 RobotTarget.Rotate(0, -90, 0)：
        /// 东 → 北、北 → 西、西 → 南、南 → 东（在「北为上、东为右」的图上是逆时针）。
        /// </summary>
        public static RobotDirection TurnLeft(RobotDirection direction)
        {
            return (RobotDirection)(((int)direction + 3) % 4);
        }

        /// <summary>原项目 TurnRightCode 执行 RobotTarget.Rotate(0, 90, 0)。</summary>
        public static RobotDirection TurnRight(RobotDirection direction)
        {
            return (RobotDirection)(((int)direction + 1) % 4);
        }

        /// <summary>
        /// 原项目用 eulerAngles.y 反推朝向索引：RoundToInt(Repeat(y,360)/90) % 4。
        /// 这里等价地直接取枚举值。
        /// </summary>
        public static int ToFacingIndex(RobotDirection direction)
        {
            return (int)direction;
        }

        /// <summary>UI 上让箭头指向该朝向所需的角度（箭头 sprite 默认指向 +X）。</summary>
        public static float ToUiAngle(RobotDirection direction)
        {
            return 90f - 90f * (int)direction;
        }
    }
}
