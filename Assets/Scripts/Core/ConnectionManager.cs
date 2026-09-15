using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>一条连线。isJudger=true 为绿线（条件），false 为黄线（顺序）。</summary>
    public class BlockConnection
    {
        public CodeBlock from;
        public CodeBlock to;
        public bool isJudger;
    }

    /// <summary>
    /// 逐行移植原项目 Assets/scripts/ConnectionManager.cs 的「选中 → 连线」状态机。
    ///
    /// 操作映射（用户确认）：
    ///   VR 右握柄（抓取/移动块）   →  鼠标左键拖动
    ///   VR 右扳机（选中/连线/断开） →  鼠标右键点击
    ///
    /// 因此本类只处理「右键点击命中某个块（或空白）」这一件事，
    /// 与原来的 raycast 命中等价：hitBlock 就是射线击中的那个块。
    ///
    /// 状态机（与原实现逐分支一致）：
    ///   未选中 + 命中非 BoolCode        → 选中并高亮
    ///   已选中 While/If + 命中 BoolCode → 连/断 Judger（绿线）
    ///   已选中 BoolCode + 命中 While/If → 仅当它正是该块的 Judger 时断开（原实现可达性极低）
    ///   其余 + 命中另一个块             → 尝试连 next（黄线），三条拒绝规则
    ///   其余 + 命中自己或空白           → 从该块开始断开（先断 Judger，再断 next）
    ///
    /// 运行中禁止一切连线（原 HandleActivatePress 里的 IsExecuting 检查）。
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        public static ConnectionManager Instance { get; private set; }

        /// <summary>连线集合变化时触发，供工作区重绘线条。</summary>
        public event Action Changed;

        private readonly List<BlockConnection> connections = new List<BlockConnection>();
        private readonly List<CodeBlock> registeredBlocks = new List<CodeBlock>();

        private CodeBlock selectedBlock;
        private CodeExecutor executor;

        public CodeBlock SelectedBlock { get { return selectedBlock; } }
        public List<BlockConnection> Connections { get { return connections; } }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Init(CodeExecutor codeExecutor)
        {
            executor = codeExecutor;
        }

        private void Update()
        {
            // 原 Update()：选中目标变得不可连线时自动取消选中。
            if (selectedBlock != null && !CanConnectBlock(selectedBlock))
                DeselectBlock();
        }

        // ------------------------------------------------------------------
        // 工作区块登记（代替原项目的 FindObjectsOfType<Code>）
        // ------------------------------------------------------------------

        public void Register(CodeBlock block)
        {
            if (block != null && !registeredBlocks.Contains(block))
                registeredBlocks.Add(block);
        }

        public void Unregister(CodeBlock block)
        {
            if (block == null)
                return;
            registeredBlocks.Remove(block);
        }

        public List<CodeBlock> RegisteredBlocks
        {
            get { return registeredBlocks; }
        }

        // ------------------------------------------------------------------
        // 可连线判定
        // ------------------------------------------------------------------

        private static bool IsLevelPlayActive()
        {
            return LevelManager.Instance != null && LevelManager.Instance.IsLevelActive;
        }

        /// <summary>原 IsWorkspaceBlock：2D 版工作区里的块都不是库存块，故只需类型检查。</summary>
        public bool CanConnectBlock(CodeBlock block)
        {
            return IsLevelPlayActive()
                && block != null
                && BlockTypes.IsConnectable(block.type);
        }

        // ------------------------------------------------------------------
        // 入口：右键点击
        // ------------------------------------------------------------------

        /// <summary>右键点击工作区。hitBlock 为 null 表示点在空白处。</summary>
        public void HandleRightClick(CodeBlock hitBlock)
        {
            if (!IsLevelPlayActive())
            {
                DeselectBlock();
                return;
            }

            // 原实现：程序运行中不接受任何连线操作。
            if (executor != null && executor.IsExecuting)
                return;

            if (hitBlock != null && !CanConnectBlock(hitBlock))
                hitBlock = null;

            if (selectedBlock == null)
            {
                if (hitBlock != null && !hitBlock.IsBool)
                    SelectBlock(hitBlock);
                return;
            }

            // 分支 1：已选中 While + 命中 BoolCode → 设置/解除 Judger
            if (selectedBlock.IsWhile && hitBlock != null && hitBlock.IsBool)
            {
                HandleJudgerTarget(selectedBlock, hitBlock);
                return;
            }

            // 分支 2：已选中 If + 命中 BoolCode → 同上
            if (selectedBlock.IsIf && hitBlock != null && hitBlock.IsBool)
            {
                HandleJudgerTarget(selectedBlock, hitBlock);
                return;
            }

            // 分支 3：已选中 BoolCode + 命中 While → 只有它正是该 While 的 Judger 时才断开
            // （原实现保留了这两个分支；由于选中阶段排除 BoolCode，实际不可达，此处仅为一致）
            if (selectedBlock.IsBool && hitBlock != null && hitBlock.IsWhile)
            {
                if (hitBlock.judger == selectedBlock)
                {
                    DisconnectJudger(hitBlock);
                    DeselectBlock();
                }
                return;
            }

            if (selectedBlock.IsBool && hitBlock != null && hitBlock.IsIf)
            {
                if (hitBlock.judger == selectedBlock)
                {
                    DisconnectJudger(hitBlock);
                    DeselectBlock();
                }
                return;
            }

            // 分支 4：连 next / 或断开
            if (hitBlock != null && hitBlock != selectedBlock)
            {
                string reason;
                if (TryConnect(selectedBlock, hitBlock, out reason))
                    Connect(selectedBlock, hitBlock);
                else
                    SetStatus(reason);

                DeselectBlock();
            }
            else
            {
                // 命中自己或空白：从该块开始断开（先 Judger，再 next）
                TryDisconnectFromStart(selectedBlock);
                DeselectBlock();
            }
        }

        private void HandleJudgerTarget(CodeBlock whileOrIf, CodeBlock boolBlock)
        {
            if (whileOrIf.judger == boolBlock)
            {
                DisconnectJudger(whileOrIf);
            }
            else if (!IsAnyonesJudger(boolBlock))
            {
                ConnectJudger(whileOrIf, boolBlock);
                ClearStatus();
            }
            else
            {
                SetStatus("That condition block is already connected.");
            }

            DeselectBlock();
        }

        // ------------------------------------------------------------------
        // 选中
        // ------------------------------------------------------------------

        private void SelectBlock(CodeBlock block)
        {
            selectedBlock = block;
            block.SetHighlight(true);
        }

        /// <summary>原 ConnectionManager.CancelSelection。</summary>
        public void CancelSelection()
        {
            DeselectBlock();
        }

        private void DeselectBlock()
        {
            if (selectedBlock != null)
            {
                selectedBlock.SetHighlight(false);
                selectedBlock = null;
            }
        }

        // ------------------------------------------------------------------
        // next 连线
        // ------------------------------------------------------------------

        /// <summary>原 TryConnect 的三条拒绝规则，文案逐字一致。</summary>
        private bool TryConnect(CodeBlock from, CodeBlock to, out string reason)
        {
            reason = null;

            if (from == to)
            {
                reason = "Cannot connect a block to itself.";
                return false;
            }

            if (WouldCreateCycle(from, to))
            {
                reason = "That connection would create a cycle.";
                return false;
            }

            if (IsAnyonesNext(to, from))
            {
                reason = "That block already has an incoming connection.";
                return false;
            }

            return true;
        }

        private bool WouldCreateCycle(CodeBlock from, CodeBlock to)
        {
            CodeBlock current = to;
            int maxIterations = 1000;
            int iterations = 0;

            while (current != null && iterations < maxIterations)
            {
                if (current == from) return true;
                current = current.next;
                iterations++;
            }

            return false;
        }

        private bool IsAnyonesNext(CodeBlock target, CodeBlock exceptFrom)
        {
            for (int i = 0; i < registeredBlocks.Count; i++)
            {
                CodeBlock block = registeredBlocks[i];
                if (block == null) continue;
                if (block.next == target && block != exceptFrom)
                    return true;
            }
            return false;
        }

        private void Connect(CodeBlock from, CodeBlock to)
        {
            // 重复连线 = 先断旧的再连新的（原 Connect 内部先调 Disconnect(from)）。
            Disconnect(from);

            from.next = to;
            connections.Add(new BlockConnection { from = from, to = to, isJudger = false });
            RaiseChanged();
        }

        private void Disconnect(CodeBlock from)
        {
            if (from == null)
                return;

            CodeBlock oldNext = from.next;
            if (oldNext != null)
                RemoveConnectionRecord(from, oldNext);

            from.next = null;
        }

        // ------------------------------------------------------------------
        // Judger 连线
        // ------------------------------------------------------------------

        private bool IsAnyonesJudger(CodeBlock target)
        {
            for (int i = 0; i < registeredBlocks.Count; i++)
            {
                CodeBlock block = registeredBlocks[i];
                if (block == null) continue;
                if (block.HasJudgerSlot && block.judger == target)
                    return true;
            }
            return false;
        }

        private void ConnectJudger(CodeBlock whileOrIf, CodeBlock boolBlock)
        {
            DisconnectJudger(whileOrIf);

            whileOrIf.judger = boolBlock;
            connections.Add(new BlockConnection { from = whileOrIf, to = boolBlock, isJudger = true });
            RaiseChanged();
        }

        private void DisconnectJudger(CodeBlock whileOrIf)
        {
            if (whileOrIf == null)
                return;

            CodeBlock old = whileOrIf.judger;
            whileOrIf.judger = null;

            if (old != null)
            {
                RemoveConnectionRecord(whileOrIf, old);
                RaiseChanged();
            }
        }

        /// <summary>原 TryDisconnectFromStart：返回是否真的断开了一条线。</summary>
        private bool TryDisconnectFromStart(CodeBlock block)
        {
            if (block == null)
                return false;

            if (block.judger != null)
            {
                DisconnectJudger(block);
                return true;
            }

            if (block.next != null)
            {
                Disconnect(block);
                RaiseChanged();
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------
        // 清理
        // ------------------------------------------------------------------

        /// <summary>
        /// 原 ConnectionManager.CleanupBlock：块被归还/销毁前，断开它身上以及指向它的所有线。
        /// </summary>
        public void CleanupBlock(CodeBlock block)
        {
            if (block == null)
                return;

            if (selectedBlock == block)
                DeselectBlock();

            Disconnect(block);

            for (int i = 0; i < registeredBlocks.Count; i++)
            {
                CodeBlock other = registeredBlocks[i];
                if (other == null) continue;
                if (other.next == block)
                    Disconnect(other);
            }

            for (int i = 0; i < registeredBlocks.Count; i++)
            {
                CodeBlock other = registeredBlocks[i];
                if (other == null) continue;
                if (other.HasJudgerSlot && other.judger == block)
                    DisconnectJudger(other);
            }

            RemoveConnectionsInvolving(block);
        }

        /// <summary>清空全部连线（换关 / Retry(Clear) 时用）。</summary>
        public void ClearAll()
        {
            DeselectBlock();

            for (int i = 0; i < registeredBlocks.Count; i++)
            {
                CodeBlock block = registeredBlocks[i];
                if (block == null) continue;
                block.next = null;
                block.judger = null;
            }

            connections.Clear();
            RaiseChanged();
        }

        private void RemoveConnectionRecord(CodeBlock from, CodeBlock to)
        {
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                if (connections[i].from == from && connections[i].to == to)
                {
                    connections.RemoveAt(i);
                    return;
                }
            }
        }

        private void RemoveConnectionsInvolving(CodeBlock block)
        {
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                if (connections[i].from == block || connections[i].to == block)
                    connections.RemoveAt(i);
            }
        }

        private void RaiseChanged()
        {
            if (Changed != null)
                Changed();
        }

        // ------------------------------------------------------------------

        private static void SetStatus(string message)
        {
            if (GameController.Instance != null)
                GameController.Instance.SetStatus(message);
        }

        private static void ClearStatus()
        {
            if (GameController.Instance != null)
                GameController.Instance.ClearStatus();
        }
    }
}
