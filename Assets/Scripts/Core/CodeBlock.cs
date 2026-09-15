using System;
using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>
    /// 代码块实例。一个 MonoBehaviour 承担原项目 15 个 Code 子类的全部行为，
    /// work() 内的每个分支都与原类逐行对应（见各 case 注释）。
    ///
    /// 原项目的关键契约（必须保持）：
    ///   * next   —— 顺序链（黄线），由 ConnectionManager 维护
    ///   * judger —— 条件链（绿线），仅 While / If 使用
    ///   * work() 是异步的：立即完成的块在 work() 内同步调用 Complete()；
    ///     MoveForward 由 LevelManager 的动画驱动，动画结束后才 Complete()
    ///   * CodeExecutor 在调用 work() 前订阅 OnComplete，因此同步 Complete() 也安全
    /// </summary>
    [DisallowMultipleComponent]
    public class CodeBlock : MonoBehaviour
    {
        [Header("Identity")]
        public BlockType type = BlockType.MoveForward;

        [Header("Program graph")]
        public CodeBlock next;
        public CodeBlock judger;
        public bool judge;

        /// <summary>原 Code.OnComplete。</summary>
        public event Action OnComplete;

        private BlockView view;

        public bool IsBool { get { return BlockTypes.IsBool(type); } }
        public bool IsWhile { get { return type == BlockType.While; } }
        public bool IsIf { get { return type == BlockType.If; } }
        public bool HasJudgerSlot { get { return BlockTypes.HasJudgerSlot(type); } }

        public void AttachView(BlockView blockView)
        {
            view = blockView;
        }

        protected void Complete()
        {
            if (OnComplete != null)
                OnComplete();
        }

        /// <summary>原 Code.ResetState() 为空实现，这里保持一致（judge 每次 work() 都会重算）。</summary>
        public void ResetState()
        {
        }

        /// <summary>原 Code.SetHighlight：高亮为黄色。这里映射到 BlockView 的描边/底色。</summary>
        public void SetHighlight(bool active)
        {
            if (view != null)
                view.SetHighlight(active);
        }

        public void work()
        {
            switch (type)
            {
                // ---- 原 Start.cs ----
                case BlockType.Start:
                    Complete();
                    break;

                // ---- 原 While.cs / WhileEnd.cs / If.cs / IfEnd.cs / Else.cs ----
                case BlockType.While:
                case BlockType.WhileEnd:
                case BlockType.If:
                case BlockType.IfEnd:
                case BlockType.Else:
                    Complete();
                    break;

                // ---- 原 MoveCode.cs（动画由 LevelManager 驱动）----
                case BlockType.MoveForward:
                    if (LevelManager.Instance == null)
                    {
                        Complete();
                        break;
                    }
                    LevelManager.Instance.StartRobotMoveForward(Complete);
                    break;

                // ---- 原 TurnLeftCode.cs：Rotate(0, -90, 0) ----
                case BlockType.TurnLeft:
                    if (LevelManager.Instance != null)
                        LevelManager.Instance.TurnRobotLeft();
                    Complete();
                    break;

                // ---- 原 TurnRightCode.cs：Rotate(0, 90, 0) ----
                case BlockType.TurnRight:
                    if (LevelManager.Instance != null)
                        LevelManager.Instance.TurnRobotRight();
                    Complete();
                    break;

                // ---- 原 TrueCode.cs ----
                case BlockType.True:
                    judge = true;
                    Complete();
                    break;

                // ---- 原 FalseCode.cs ----
                case BlockType.False:
                    judge = false;
                    Complete();
                    break;

                // ---- 原 StarFrontCode.cs：正前方一格是否有未收集的星 ----
                case BlockType.DetectFrontStar:
                    judge = ScanStar(RobotDirections.ToVector(CurrentFacing()));
                    Complete();
                    break;

                // ---- 原 StarLeftCode.cs：Quaternion.Euler(0,-90,0) * forward ----
                case BlockType.DetectLeftStar:
                    judge = ScanStar(RobotDirections.ToVector(RobotDirections.TurnLeft(CurrentFacing())));
                    Complete();
                    break;

                // ---- 原 StarRightCode.cs：Quaternion.Euler(0,90,0) * forward ----
                case BlockType.DetectRightStar:
                    judge = ScanStar(RobotDirections.ToVector(RobotDirections.TurnRight(CurrentFacing())));
                    Complete();
                    break;

                // ---- 原 StarRemainCode.cs：场上是否还有未收集的星 ----
                case BlockType.StarRemain:
                    judge = LevelManager.Instance != null && LevelManager.Instance.AnyStarRemaining;
                    Complete();
                    break;

                default:
                    Complete();
                    break;
            }
        }

        private static RobotDirection CurrentFacing()
        {
            return LevelManager.Instance != null ? LevelManager.Instance.RobotFacing : RobotDirection.North;
        }

        /// <summary>
        /// 原 StarFrontCode.CheckStar：从机器人当前位置沿 dir 前进一格，
        /// 若该处存在未收集的星（距离 &lt; 0.4，cellSize=1）则返回 true。
        /// 注意：原实现**不**比较 star.orderIndex —— 只要该格有未收集的星就算命中。
        /// </summary>
        private static bool ScanStar(Vector2Int direction)
        {
            if (LevelManager.Instance == null)
                return false;

            Vector2 checkPos = LevelManager.Instance.RobotPos + new Vector2(direction.x, direction.y);
            return LevelManager.Instance.HasUncollectedStarAt(checkPos);
        }
    }
}
