using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>一个星星的运行时状态（原项目 Star.cs 的 orderIndex / collected）。</summary>
    public class StarState
    {
        public Vector2Int cell;
        public int orderIndex;
        public bool collected;
    }

    /// <summary>
    /// 关卡运行时状态：网格尺寸、机器人位置与朝向、星星、每关一次运行的记账。
    /// 对应原项目 LevelManager.cs 加上 MoveCode 的移动部分，**不含 UI**。
    ///
    /// 坐标约定：机器人位置用「格单位」表示，格 (0,0) 的中心是 (0.5, 0.5)，
    /// 格 (i,j) 的中心是 (i+0.5, j+0.5)。这样：
    ///   * IsWithinGrid 与原项目 worldPos 落在 [origin+0.5, origin+size-0.5] 完全同构
    ///   * 探测块的距离阈值 0.4 与原项目（cellSize = 1）同量级
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Motion (原 MoveCode.speed = 2, cellSize = 1，即每格 0.5 秒)")]
        public float moveSpeed = 2f;
        public float cellSize = 1f;

        [Header("Star detection (原 StarFrontCode 使用 0.4)")]
        public float starDetectRadius = 0.4f;

        /// <summary>运行结束时触发：参数为 (是否通关, 失败原因)。</summary>
        public event Action<bool, string> RunResolved;

        /// <summary>星星收集状态变化时触发，供棋盘视图刷新。</summary>
        public event Action StarsChanged;

        private LevelData[] levels;
        private int currentLevelIndex;
        private bool levelActive;

        private Vector2Int gridSize = new Vector2Int(10, 10);
        private Vector2 robotPos;
        private RobotDirection robotFacing = RobotDirection.North;
        private readonly List<StarState> stars = new List<StarState>();

        private bool isMoving;
        private Vector2 moveDirection;
        private Vector2 moveTarget;
        private float movedDistance;
        private float moveTargetDistance;
        private Action moveFinished;

        public int nextStarIndex { get; private set; }
        public bool IsLevelActive { get { return levelActive; } }
        public bool HasStartedThisAttempt { get; private set; }
        public bool IsLevelResolved { get; private set; }
        public int CurrentLevelIndex { get { return currentLevelIndex; } }
        public int LevelCount { get { return levels != null ? levels.Length : 0; } }

        public Vector2 RobotPos { get { return robotPos; } }
        public RobotDirection RobotFacing { get { return robotFacing; } }
        public Vector2Int GridSize { get { return gridSize; } }
        public bool IsRobotMoving { get { return isMoving; } }

        public List<StarState> Stars { get { return stars; } }
        public int StarCount { get { return stars.Count; } }
        public int CollectedStarCount { get { return nextStarIndex; } }

        public bool IsLastLevel
        {
            get { return levels != null && levels.Length > 0 && currentLevelIndex == levels.Length - 1; }
        }

        public LevelData CurrentLevel
        {
            get
            {
                if (levels == null || levels.Length == 0) return null;
                if (currentLevelIndex < 0 || currentLevelIndex >= levels.Length) return null;
                return levels[currentLevelIndex];
            }
        }

        public bool AnyStarRemaining
        {
            get
            {
                for (int i = 0; i < stars.Count; i++)
                {
                    if (!stars[i].collected)
                        return true;
                }
                return false;
            }
        }

        private void Awake()
        {
            Instance = this;
            levels = LevelLibrary.CreateAll();
            ResetToPlayground();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            TickRobotMovement();
        }

        // ------------------------------------------------------------------
        // 关卡装载
        // ------------------------------------------------------------------

        /// <summary>
        /// 装载第 index 关（0 基）。对应原 LevelManager.LoadLevelByIndex 的状态部分；
        /// 库存、工作区与 UI 的处理由 GameController 编排。
        /// </summary>
        public bool TryLoad(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length)
                return false;

            LevelData data = levels[index];
            if (data == null)
                return false;

            CancelRobotMovement();

            currentLevelIndex = index;
            gridSize = (data.gridSize.x > 0 && data.gridSize.y > 0) ? data.gridSize : new Vector2Int(10, 10);
            nextStarIndex = 0;
            HasStartedThisAttempt = false;
            IsLevelResolved = false;

            BuildStars(data);
            PlaceRobot(data);

            levelActive = true;

            RaiseStarsChanged();
            return true;
        }

        public bool Reload()
        {
            return TryLoad(currentLevelIndex);
        }

        /// <summary>对应原 LevelManager.StopLevel：关闭关卡并回到 playground 状态。</summary>
        public void StopLevel()
        {
            levelActive = false;
            HasStartedThisAttempt = false;
            IsLevelResolved = false;
            CancelRobotMovement();
            ResetToPlayground();
            RaiseStarsChanged();
        }

        private void ResetToPlayground()
        {
            gridSize = new Vector2Int(10, 10);
            stars.Clear();
            nextStarIndex = 0;

            // 原 GeneratePlayground：机器人放在格 (4,4) 中心，rotation identity 即朝北。
            robotPos = new Vector2(4.5f, 4.5f);
            robotFacing = RobotDirection.North;
        }

        private void BuildStars(LevelData data)
        {
            stars.Clear();
            if (data.starPositions == null)
                return;

            for (int i = 0; i < data.starPositions.Length; i++)
            {
                stars.Add(new StarState
                {
                    cell = data.starPositions[i],
                    orderIndex = i,
                    collected = false
                });
            }
        }

        private void PlaceRobot(LevelData data)
        {
            robotPos = CellCenter(data.robotStart);
            robotFacing = data.robotFacing;
        }

        // ------------------------------------------------------------------
        // 网格换算
        // ------------------------------------------------------------------

        public static Vector2 CellCenter(Vector2Int cell)
        {
            return new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        }

        public static Vector2Int PosToCell(Vector2 pos)
        {
            return new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
        }

        /// <summary>原 LevelManager.IsWithinGrid 的格单位等价写法。</summary>
        public bool IsWithinGrid(Vector2 pos)
        {
            return pos.x >= 0.5f && pos.x <= gridSize.x - 0.5f
                && pos.y >= 0.5f && pos.y <= gridSize.y - 0.5f;
        }

        public bool IsWithinGrid(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < gridSize.x && cell.y >= 0 && cell.y < gridSize.y;
        }

        // ------------------------------------------------------------------
        // 星星
        // ------------------------------------------------------------------

        /// <summary>原 StarFrontCode.CheckStar：某点附近（小于 0.4）是否存在未收集的星。</summary>
        public bool HasUncollectedStarAt(Vector2 pos)
        {
            float radiusSqr = starDetectRadius * starDetectRadius;
            for (int i = 0; i < stars.Count; i++)
            {
                StarState star = stars[i];
                if (star.collected)
                    continue;

                if ((CellCenter(star.cell) - pos).sqrMagnitude < radiusSqr)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 原项目的触发式收集：机器人走到星星格里即收集。
        /// 2D 版在每次移动结束时调用（没有物理触发器）。
        /// </summary>
        public void TryCollectStarsAt(Vector2 pos)
        {
            if (!levelActive || IsLevelResolved)
                return;

            bool collectedAny = false;
            for (int i = 0; i < stars.Count; i++)
            {
                StarState star = stars[i];
                if (star.collected)
                    continue;

                if ((CellCenter(star.cell) - pos).sqrMagnitude < starDetectRadius * starDetectRadius)
                {
                    star.collected = true;
                    nextStarIndex++;
                    collectedAny = true;
                }
            }

            if (collectedAny)
                RaiseStarsChanged();
        }

        // ------------------------------------------------------------------
        // 机器人动作
        // ------------------------------------------------------------------

        public void TurnRobotLeft()
        {
            robotFacing = RobotDirections.TurnLeft(robotFacing);
        }

        public void TurnRobotRight()
        {
            robotFacing = RobotDirections.TurnRight(robotFacing);
        }

        /// <summary>
        /// 原 MoveCode.work() 与 Update()：
        ///   目标是正前方一格；若目标越界则不移动并直接完成（不判失败）；
        ///   否则以 moveSpeed 的速度插值，到达后吸附到格心并回调。
        /// </summary>
        public void StartRobotMoveForward(Action onDone)
        {
            // 原 work() 开头的防御：正在移动时直接返回（执行器会等待，正常不会发生）。
            // 必须放在赋值 moveFinished 之前，否则会覆盖进行中那一步的回调。
            if (isMoving)
                return;

            moveFinished = onDone;

            Vector2Int direction = RobotDirections.ToVector(robotFacing);
            moveDirection = new Vector2(direction.x, direction.y);
            moveTargetDistance = cellSize;
            moveTarget = robotPos + moveDirection * moveTargetDistance;

            if (!IsWithinGrid(moveTarget))
            {
                // 原实现：越界时 Complete() 但不移动。
                FinishMoveCallback();
                return;
            }

            isMoving = true;
            movedDistance = 0f;
        }

        private void TickRobotMovement()
        {
            if (!isMoving)
                return;

            float step = moveSpeed * Time.deltaTime;
            step = Mathf.Min(step, moveTargetDistance - movedDistance);

            robotPos += moveDirection * step;
            movedDistance += step;

            if (movedDistance >= moveTargetDistance)
            {
                // 原实现：结束位置吸附到目标点，消除累积浮点误差。
                robotPos = moveTarget;
                isMoving = false;
                TryCollectStarsAt(robotPos);
                FinishMoveCallback();
            }
        }

        private void FinishMoveCallback()
        {
            Action callback = moveFinished;
            moveFinished = null;
            if (callback != null)
                callback();
        }

        private void CancelRobotMovement()
        {
            isMoving = false;
            movedDistance = 0f;
            moveFinished = null;
        }

        // ------------------------------------------------------------------
        // 关卡判定
        // ------------------------------------------------------------------

        public void NotifyRunStarted()
        {
            if (!levelActive)
                return;

            HasStartedThisAttempt = true;
        }

        /// <summary>原 LevelManager.OnRunFinished。</summary>
        public void OnRunFinished(string failReason = null)
        {
            if (!levelActive || IsLevelResolved)
                return;

            IsLevelResolved = true;

            bool success = !AnyStarRemaining;
            if (RunResolved != null)
                RunResolved(success, failReason);
        }

        private void RaiseStarsChanged()
        {
            if (StarsChanged != null)
                StarsChanged();
        }
    }
}
