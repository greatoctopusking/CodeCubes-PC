using System;
using System.Collections.Generic;

namespace CodeCubesPC
{
    /// <summary>
    /// 左下角库存（对应原项目的软木板槽位池）。
    ///
    /// 核心规则：**总量守恒**
    ///   库存剩余 + 工作区里的块 = 本关上限（由 LevelPolicy 算出）
    /// 抓出后不补货；归还只回到同类型的空位；没有空位就拒绝归还（原 ReturnBlock 返回 false）。
    /// </summary>
    public class BlockPool
    {
        private readonly Dictionary<BlockType, int> limits = new Dictionary<BlockType, int>();
        private readonly Dictionary<BlockType, int> remaining = new Dictionary<BlockType, int>();

        /// <summary>数量变化时触发，供左侧面板刷新显示。</summary>
        public event Action Changed;

        /// <summary>本关在库存里出现的块类型（按 BlockTypes.All 的固定顺序，UI 列顺序稳定）。</summary>
        public List<BlockType> OrderedTypes()
        {
            var ordered = new List<BlockType>();
            for (int i = 0; i < BlockTypes.All.Length; i++)
            {
                BlockType type = BlockTypes.All[i];
                if (limits.ContainsKey(type))
                    ordered.Add(type);
            }
            return ordered;
        }

        public int Limit(BlockType type)
        {
            int value;
            return limits.TryGetValue(type, out value) ? value : 0;
        }

        public int Remaining(BlockType type)
        {
            int value;
            return remaining.TryGetValue(type, out value) ? value : 0;
        }

        public bool IsAvailable(BlockType type)
        {
            return Remaining(type) > 0;
        }

        /// <summary>本关库存总上限（不含唯一的 Start）。</summary>
        public int TotalLimit()
        {
            int total = 0;
            foreach (KeyValuePair<BlockType, int> pair in limits)
                total += pair.Value;
            return total;
        }

        /// <summary>当前还留在库存里的块数；TotalLimit - TotalRemaining 就是散在工作区的块数。</summary>
        public int TotalRemaining()
        {
            int total = 0;
            foreach (KeyValuePair<BlockType, int> pair in remaining)
                total += pair.Value;
            return total;
        }

        /// <summary>进入关卡：按本关 suggestedBlockNames 重建库存（对应 ApplyAvailableBlocks）。</summary>
        public void BuildForLevel(BlockType[] suggested)
        {
            limits.Clear();
            remaining.Clear();

            Dictionary<BlockType, int> supply = LevelPolicy.BuildSupply(suggested);
            foreach (KeyValuePair<BlockType, int> pair in supply)
            {
                limits[pair.Key] = pair.Value;
                remaining[pair.Key] = pair.Value;
            }

            RaiseChanged();
        }

        public void Clear()
        {
            limits.Clear();
            remaining.Clear();
            RaiseChanged();
        }

        /// <summary>
        /// 从库存取出一个块。返回 false 表示这一类的块已经全在工作区了
        /// （原项目：槽位全空，不留任何补货）。
        /// </summary>
        public bool TryTake(BlockType type)
        {
            int current;
            if (!remaining.TryGetValue(type, out current) || current <= 0)
                return false;

            remaining[type] = current - 1;
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// 归还一个块（垃圾桶 / 换关清空 / Retry(Clear)）。
        /// 返回 false 表示同类型槽位已满 —— 原项目此时**不销毁**该块，这里同样拒绝归还。
        /// </summary>
        public bool TryReturn(BlockType type)
        {
            int current;
            if (!remaining.TryGetValue(type, out current))
                return false;

            if (current >= limits[type])
                return false;

            remaining[type] = current + 1;
            RaiseChanged();
            return true;
        }

        private void RaiseChanged()
        {
            if (Changed != null)
                Changed();
        }
    }
}
