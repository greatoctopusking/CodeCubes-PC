using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>
    /// 对应原项目 Assets/LevelDate/Level 1..12.asset（LevelData ScriptableObject）。
    /// 2D 版直接用代码硬编码，字段语义完全相同，见 LevelLibrary.cs。
    /// </summary>
    public class LevelData
    {
        public int levelNumber;
        public Vector2Int gridSize = new Vector2Int(10, 10);
        public Vector2Int robotStart;
        public RobotDirection robotFacing = RobotDirection.North;

        /// <summary>星格坐标数组；数组下标即原项目 Star.orderIndex。</summary>
        public Vector2Int[] starPositions;

        /// <summary>
        /// 本关墙上可用的块类型。对应原资源的 suggestedBlockNames。
        /// </summary>
        public BlockType[] suggestedBlockNames;

        /// <summary>
        /// 原项目 LevelData.GetSuggestedBlockNames()：
        /// 资源里有值就用资源，空则回退到 LevelBlockHints.GetForLevel(levelNumber)。
        /// </summary>
        public BlockType[] GetSuggestedBlockNames()
        {
            if (suggestedBlockNames != null && suggestedBlockNames.Length > 0)
                return suggestedBlockNames;

            return LevelBlockHints.GetForLevel(levelNumber);
        }
    }
}
