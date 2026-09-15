using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>
    /// 逐行移植原项目 Assets/scripts/CodeManager.cs 的解释器部分
    /// （PlayCoroutine / FindMatching* / IsInfiniteLoop / ToggleCodeExecution / StopExecution）。
    ///
    /// 已按用户确认的改动：
    ///   * 删除「机器人 4.8 秒开机动画」的等待 —— 按 Run 立即执行第一步
    ///   * 左扳机 → 由 GameController 的 Run 按钮调用 ToggleRun()
    ///   * 音效调用全部去掉（本版不做音频）
    ///
    /// 其余语义（含无限循环判定与全部报错文案）保持不变。
    /// </summary>
    public class CodeExecutor : MonoBehaviour
    {
        public static CodeExecutor Instance { get; private set; }

        private const int MaxExecutionSteps = 8000;

        private Coroutine playRoutine;
        private readonly Stack<CodeBlock> loopStack = new Stack<CodeBlock>();
        private string runAbortReason;

        private ConnectionManager connectionManager;

        public bool IsExecuting { get { return playRoutine != null; } }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Init(ConnectionManager connections)
        {
            connectionManager = connections;
        }

        // ------------------------------------------------------------------
        // 入口
        // ------------------------------------------------------------------

        /// <summary>对应原 ToggleCodeExecution（由 Run 按钮调用）。</summary>
        public void ToggleRun()
        {
            bool inLevel = LevelManager.Instance != null && LevelManager.Instance.IsLevelActive;

            if (playRoutine != null)
            {
                // 关卡内运行中不允许停止（原实现如此）。
                if (inLevel)
                    return;

                StopExecution();
                ResetAllBlocks(FindProgramStart());
                return;
            }

            // 每关只有一次机会：已经开始过或已有结果就不允许再跑。
            if (inLevel
                && (LevelManager.Instance.HasStartedThisAttempt || LevelManager.Instance.IsLevelResolved))
                return;

            CodeBlock start = FindProgramStart();
            if (start == null)
            {
                SetStatus("Connect your program to the Start block on the ground.");
                return;
            }

            BlockType[] suggested = null;
            if (inLevel && LevelManager.Instance.CurrentLevel != null)
                suggested = LevelManager.Instance.CurrentLevel.GetSuggestedBlockNames();

            List<CodeValidator.Error> errors = CodeValidator.Validate(start, suggested);
            if (errors.Count > 0)
            {
                var messages = new List<string>();
                for (int i = 0; i < errors.Count; i++)
                    messages.Add(errors[i].message);

                SetStatus(string.Join("\n", messages.ToArray()));
                return;
            }

            ClearStatus();

            if (GameController.Instance != null)
            {
                GameController.Instance.HideFacingIndicator();
                GameController.Instance.HideHints();
            }

            if (inLevel)
                LevelManager.Instance.NotifyRunStarted();

            loopStack.Clear();
            ResetAllBlocks(start);

            playRoutine = StartCoroutine(PlayCoroutine(start));
        }

        public void StopExecution()
        {
            if (playRoutine == null)
                return;

            StopAllCoroutines();
            playRoutine = null;
            loopStack.Clear();
            runAbortReason = null;
        }

        public void ClearAllHighlights()
        {
            if (connectionManager == null)
                return;

            List<CodeBlock> blocks = connectionManager.RegisteredBlocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] != null)
                    blocks[i].SetHighlight(false);
            }
        }

        // ------------------------------------------------------------------
        // 解释循环
        // ------------------------------------------------------------------

        private IEnumerator PlayCoroutine(CodeBlock startBlock)
        {
            CodeBlock cur = startBlock;
            if (cur == null)
            {
                FinishPlayRoutine();
                yield break;
            }

            runAbortReason = null;
            var seenStates = new HashSet<string>();
            int steps = 0;

            while (cur != null)
            {
                if (IsInfiniteLoop(cur, seenStates, ref steps))
                {
                    runAbortReason = "Infinite loop detected. The robot is not making progress.";
                    SetStatus(runAbortReason);
                    break;
                }

                bool completed = false;
                Action handler = () => completed = true;

                cur.OnComplete += handler;
                cur.SetHighlight(true);
                cur.work();
                yield return new WaitUntil(() => completed);
                cur.SetHighlight(false);
                cur.OnComplete -= handler;

                // ---- While：判断条件，真则进入循环体，假则跳到配对的 WhileEnd 之后 ----
                if (cur.type == BlockType.While)
                {
                    CodeBlock whileBlock = cur;
                    if (whileBlock.judger != null)
                        whileBlock.judger.work();

                    if (whileBlock.judger != null && whileBlock.judger.judge)
                    {
                        loopStack.Push(whileBlock);
                    }
                    else
                    {
                        CodeBlock whileEnd = FindMatchingWhileEnd(whileBlock);
                        cur = whileEnd != null ? whileEnd.next : null;
                        if (cur == null) break;
                        continue;
                    }
                }

                // ---- WhileEnd：条件仍为真则跳回循环体开头，否则出栈并继续 ----
                if (cur.type == BlockType.WhileEnd)
                {
                    if (loopStack.Count == 0)
                        break;

                    CodeBlock loopStart = loopStack.Peek();
                    if (loopStart.judger != null)
                        loopStart.judger.work();

                    if (loopStart.judger != null && loopStart.judger.judge)
                    {
                        cur = loopStart.next;
                        continue;
                    }

                    loopStack.Pop();
                    cur = cur.next;
                    continue;
                }

                // ---- If：条件真则走 body，假则走 Else（没有 Else 就跳到 IfEnd 之后）----
                if (cur.type == BlockType.If)
                {
                    CodeBlock ifBlock = cur;
                    if (ifBlock.judger != null)
                        ifBlock.judger.work();

                    if (ifBlock.judger != null && ifBlock.judger.judge)
                    {
                        cur = ifBlock.next;
                        if (cur == null) break;
                        continue;
                    }

                    CodeBlock elseBlock = FindMatchingElse(ifBlock);
                    if (elseBlock != null)
                    {
                        cur = elseBlock.next;
                    }
                    else
                    {
                        CodeBlock ifEnd = FindMatchingIfEnd(ifBlock);
                        cur = ifEnd != null ? ifEnd.next : null;
                    }

                    if (cur == null) break;
                    continue;
                }

                // ---- Else：命中 Else 说明前一个分支已经执行完，跳到配对的 IfEnd 之后 ----
                if (cur.type == BlockType.Else)
                {
                    CodeBlock ifEnd = FindMatchingIfEnd(cur);
                    cur = ifEnd != null ? ifEnd.next : null;
                    if (cur == null) break;
                    continue;
                }

                // ---- IfEnd：直接往后 ----
                if (cur.type == BlockType.IfEnd)
                {
                    cur = cur.next;
                    continue;
                }

                // ---- 链尾且在循环里：回到循环条件 ----
                if (cur.next == null && loopStack.Count > 0)
                {
                    CodeBlock loopStart = loopStack.Peek();
                    if (loopStart.judger != null)
                        loopStart.judger.work();

                    if (loopStart.judger != null && loopStart.judger.judge)
                    {
                        cur = loopStart.next;
                        continue;
                    }

                    loopStack.Pop();
                    CodeBlock whileEnd = FindMatchingWhileEnd(loopStart);
                    cur = whileEnd != null ? whileEnd.next : null;
                    if (cur == null) break;
                    continue;
                }

                cur = cur.next;
            }

            FinishPlayRoutine();
        }

        private void FinishPlayRoutine()
        {
            playRoutine = null;

            string reason = runAbortReason;
            runAbortReason = null;

            if (LevelManager.Instance != null && LevelManager.Instance.IsLevelActive)
                LevelManager.Instance.OnRunFinished(reason);
        }

        // ------------------------------------------------------------------
        // 无限循环检测（与原实现完全一致）
        // ------------------------------------------------------------------

        private static bool IsInfiniteLoop(CodeBlock block, HashSet<string> seenStates, ref int steps)
        {
            steps++;
            if (steps > MaxExecutionSteps)
                return true;

            return !seenStates.Add(ExecutionStateKey(block));
        }

        /// <summary>
        /// 原 ExecutionStateKey：块 ID | 位置(×20 取整) | 朝向 | 已收集星数。
        /// 同一个块在同一种状态下被访问两次 = 程序没有推进 → 判定死循环。
        /// </summary>
        private static string ExecutionStateKey(CodeBlock block)
        {
            int qx = 0;
            int qy = 0;
            int facing = 0;
            int stars = 0;

            if (LevelManager.Instance != null)
            {
                Vector2 pos = LevelManager.Instance.RobotPos;
                qx = Mathf.RoundToInt(pos.x * 20f);
                qy = Mathf.RoundToInt(pos.y * 20f);
                facing = RobotDirections.ToFacingIndex(LevelManager.Instance.RobotFacing);
                stars = LevelManager.Instance.nextStarIndex;
            }

            int id = block != null ? block.GetInstanceID() : 0;
            return string.Format("{0}|{1}|{2}|{3}|{4}", id, qx, qy, facing, stars);
        }

        // ------------------------------------------------------------------
        // 结构查找（与原实现一致）
        // ------------------------------------------------------------------

        private static CodeBlock FindMatchingWhileEnd(CodeBlock whileBlock)
        {
            int depth = 0;
            CodeBlock cur = whileBlock.next;

            while (cur != null)
            {
                if (cur.type == BlockType.While) depth++;
                else if (cur.type == BlockType.WhileEnd)
                {
                    if (depth == 0) return cur;
                    depth--;
                }
                cur = cur.next;
            }

            return null;
        }

        private static CodeBlock FindMatchingElse(CodeBlock ifBlock)
        {
            int depth = 0;
            CodeBlock cur = ifBlock.next;

            while (cur != null)
            {
                if (cur.type == BlockType.If) depth++;
                else if (cur.type == BlockType.Else)
                {
                    if (depth == 0) return cur;
                    depth--;
                }
                cur = cur.next;
            }

            return null;
        }

        private static CodeBlock FindMatchingIfEnd(CodeBlock fromBlock)
        {
            int depth = 0;
            CodeBlock cur = fromBlock.next;

            while (cur != null)
            {
                if (cur.type == BlockType.If) depth++;
                else if (cur.type == BlockType.IfEnd)
                {
                    if (depth == 0) return cur;
                    depth--;
                }
                cur = cur.next;
            }

            return null;
        }

        // ------------------------------------------------------------------
        // 辅助
        // ------------------------------------------------------------------

        /// <summary>
        /// 原 FindProgramStart：在工作区里找一个 Start（优先已有出线的）。
        /// 2D 版每关只有一个固定 Start，故直接取它。
        /// </summary>
        private static CodeBlock FindProgramStart()
        {
            if (GameController.Instance == null)
                return null;

            return GameController.Instance.StartBlock;
        }

        private void ResetAllBlocks(CodeBlock start)
        {
            CodeBlock cur = start;
            while (cur != null)
            {
                cur.ResetState();
                cur = cur.next;
            }
        }

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
