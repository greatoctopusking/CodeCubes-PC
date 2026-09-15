using System.Collections.Generic;
using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>
    /// 把「Catalog 物理上限」与「本关 GetCopyCap」合成左下面板每种块的可用数量。
    ///
    /// 对应原项目 CodeBlockBoard.ApplyAvailableBlocks：
    ///   typeAllowed = LevelBlockHints.IsAllowed(slot.displayName, allowedNames)
    ///   cap         = LevelBlockHints.GetCopyCap(slot.displayName, allowedNames)
    ///   按槽位顺序启用，直到 used == cap —— 而每个类型的槽位数就是 Catalog 的 maxCount(=3)。
    /// 因此某一类真正可用的数量恒为 min(Catalog.MaxCount, GetCopyCap)。
    ///
    /// 实际结果（已在代码中按关卡核实）：
    ///   L1–L5  MoveForward 3、TurnLeft 3、TurnRight 3（无控制流 → cap 无限，取 Catalog 的 3）
    ///   L6     MoveForward 1、While 2、WhileEnd 2、StarRemain 1
    ///   L7     MoveForward 1、TurnLeft 1、IF 2、IfEnd 2、DetectLeftStar 1
    ///   L8     MoveForward 1、While 2、WhileEnd 2、DetectFrontStar 1
    ///   L9     MoveForward 2、TurnLeft 2、While 2、WhileEnd 2、DetectFrontStar 2
    ///   L10–12 MoveForward 3、TurnLeft 3、TurnRight 3（cap 4 被 Catalog 的 3 限制）、
    ///          While/WhileEnd/IF/IfEnd/Else 各 2、四个探测块各 1
    ///   True / False 不在任何一关的 suggestedBlockNames 中，因此任何关卡都拿不到
    ///   （与原项目一致：避免 While True 一类投机解）。
    /// </summary>
    public static class LevelPolicy
    {
        /// <summary>
        /// 返回本关每种可用块的上限。Start 不在此表内 —— 它是工作区里唯一的固定块，
        /// 对应原项目 CodeBlockBoard.PlaceUniqueStartOnGround（拿走不补货）。
        /// </summary>
        public static Dictionary<BlockType, int> BuildSupply(BlockType[] suggested)
        {
            var supply = new Dictionary<BlockType, int>();

            for (int i = 0; i < BlockTypes.All.Length; i++)
            {
                BlockType type = BlockTypes.All[i];

                if (type == BlockType.Start)
                    continue;

                if (!LevelBlockHints.IsAllowed(type, suggested))
                    continue;

                int cap = LevelBlockHints.GetCopyCap(type, suggested);
                int limit = Mathf.Min(BlockCatalog.MaxCount(type), cap);

                if (limit > 0)
                    supply[type] = limit;
            }

            return supply;
        }

        /// <summary>本关墙上的块总数（对应原 CodeBlockCatalog.TotalBlockCount 在关卡内的实际值）。</summary>
        public static int TotalSupply(Dictionary<BlockType, int> supply)
        {
            if (supply == null)
                return 0;

            int total = 0;
            foreach (KeyValuePair<BlockType, int> pair in supply)
                total += pair.Value;

            return total;
        }
    }
}
