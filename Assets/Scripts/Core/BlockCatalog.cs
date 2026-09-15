namespace CodeCubesPC
{
    /// <summary>
    /// 对应原项目 Assets/Resources/CodeBlockCatalog.asset —— 已逐条读取实际资产核对：
    ///
    ///   Start          maxCount = 1
    ///   其余 14 种块    maxCount = 15
    ///   合计           1 + 14 × 15 = 211
    ///
    /// 注意：原项目 PLAN.md 里写的「15 种各 3 个、合计 45」是**过期信息**，实际资产是 15。
    /// 这一点非常关键：Level 4 的参考解需要 6 个 MoveForward、Level 5 需要 7 个，
    /// 如果按 3 计算，这两关直接无解；Level 11 每边要 4 个 MoveForward 也会差一个。
    /// 因此 LevelLibrary.ValidateAll() 里加了对应断言，改动本文件会立刻自检报错。
    ///
    /// 另外注意：本 Catalog 的数量只是「物理池上限」，每关真正可用的数量还要被
    /// LevelBlockHints.GetCopyCap 收紧，取两者较小值 —— 见 LevelPolicy.BuildSupply。
    /// </summary>
    public static class BlockCatalog
    {
        /// <summary>Start 全项目只有 1 个（原 CodeBlockBoard.PlaceUniqueStartOnGround 保证唯一）。</summary>
        public const int StartMaxCount = 1;

        /// <summary>其余 14 种块在 Catalog 里的 maxCount。</summary>
        public const int DefaultMaxCount = 15;

        public static int MaxCount(BlockType type)
        {
            return type == BlockType.Start ? StartMaxCount : DefaultMaxCount;
        }

        /// <summary>原 CodeBlockCatalog.TotalBlockCount：1 + 14 × 15 = 211。</summary>
        public static int TotalBlockCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < BlockTypes.All.Length; i++)
                    total += MaxCount(BlockTypes.All[i]);

                return total;
            }
        }
    }
}
