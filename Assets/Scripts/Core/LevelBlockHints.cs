namespace CodeCubesPC
{
    /// <summary>
    /// 逐行移植原项目 Assets/scripts/LevelBlockHints.cs。
    /// 唯一改动：string 块名 → BlockType 枚举（不再需要 BlockIdentity 的模糊字符串匹配）。
    ///
    /// 职责：给定一关的 suggestedBlockNames，决定
    ///   1) 哪些块类型可以出现在墙上（IsAllowed）
    ///   2) 每种类最多几个（GetCopyCap）
    ///   3) 该关是否强制要求程序里出现 IF / While / Else（ShouldRequireControlFlow）
    /// </summary>
    public static class LevelBlockHints
    {
        public static readonly BlockType[] FullToolkit =
        {
            BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft, BlockType.TurnRight,
            BlockType.While, BlockType.WhileEnd, BlockType.If, BlockType.IfEnd, BlockType.Else,
            BlockType.DetectFrontStar, BlockType.DetectLeftStar, BlockType.DetectRightStar,
            BlockType.StarRemain
        };

        /// <summary>原 LevelBlockHints.GetForLevel：suggestedBlockNames 为空时的回退表。</summary>
        public static BlockType[] GetForLevel(int levelNumber)
        {
            switch (levelNumber)
            {
                case 1:
                    return new[] { BlockType.Start, BlockType.MoveForward };
                case 2:
                    return new[] { BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft };
                case 3:
                case 4:
                case 5:
                    return new[]
                    {
                        BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft, BlockType.TurnRight
                    };
                case 6:
                    return new[]
                    {
                        BlockType.Start, BlockType.MoveForward, BlockType.While, BlockType.WhileEnd,
                        BlockType.StarRemain
                    };
                case 7:
                    return new[]
                    {
                        BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft, BlockType.If,
                        BlockType.IfEnd, BlockType.DetectLeftStar
                    };
                case 8:
                    return new[]
                    {
                        BlockType.Start, BlockType.MoveForward, BlockType.While, BlockType.WhileEnd,
                        BlockType.DetectFrontStar
                    };
                case 9:
                    return new[]
                    {
                        BlockType.Start, BlockType.MoveForward, BlockType.TurnLeft, BlockType.While,
                        BlockType.WhileEnd, BlockType.DetectFrontStar
                    };
                case 10:
                case 11:
                case 12:
                    return FullToolkit;
                default:
                    return new[] { BlockType.Start, BlockType.MoveForward };
            }
        }

        public static bool IsAllowed(BlockType type, BlockType[] suggested)
        {
            if (suggested == null || suggested.Length == 0)
                return true;

            // 原实现：Start 永远允许。
            if (type == BlockType.Start)
                return true;

            return HasName(suggested, type);
        }

        /// <summary>
        /// 原 LevelBlockHints.GetCopyCap：本关该类型最多几个。
        /// 教学关把移动块压到 1 个，逼学生用循环/分支，而不是铺一长串 MoveForward。
        /// </summary>
        public static int GetCopyCap(BlockType type, BlockType[] suggested)
        {
            if (suggested == null || suggested.Length == 0)
                return int.MaxValue;

            if (!HasControlFlow(suggested))
                return int.MaxValue;

            bool twoWhileSegments = !IsFullToolkit(suggested)
                && HasName(suggested, BlockType.While)
                && (HasName(suggested, BlockType.TurnLeft) || HasName(suggested, BlockType.TurnRight));

            if (IsMovement(type))
            {
                if (IsFullToolkit(suggested))
                    return 4;
                return twoWhileSegments ? 2 : 1;
            }

            if (IsPairedControl(type))
                return 2;

            if (type == BlockType.Else)
                return 2;

            if (twoWhileSegments && type == BlockType.DetectFrontStar)
                return 2;

            return 1;
        }

        /// <summary>
        /// 原 LevelBlockHints.ShouldRequireControlFlow：
        /// 教学关（给出 IF 或 While，但不是完整工具集）必须真的用上该结构。
        /// Level 7 → 必须含 IF；Level 6/8/9 → 必须含 While。
        /// </summary>
        public static bool ShouldRequireControlFlow(BlockType[] suggested)
        {
            if (suggested == null || suggested.Length == 0)
                return false;

            if (IsFullToolkit(suggested))
                return false;

            return HasName(suggested, BlockType.If) || HasName(suggested, BlockType.While);
        }

        public static bool HasName(BlockType[] names, BlockType name)
        {
            if (names == null)
                return false;

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                    return true;
            }

            return false;
        }

        private static bool HasControlFlow(BlockType[] suggested)
        {
            return HasName(suggested, BlockType.While) || HasName(suggested, BlockType.If);
        }

        private static bool IsFullToolkit(BlockType[] suggested)
        {
            return HasName(suggested, BlockType.If)
                && HasName(suggested, BlockType.While)
                && HasName(suggested, BlockType.Else);
        }

        private static bool IsMovement(BlockType type)
        {
            return type == BlockType.MoveForward
                || type == BlockType.TurnLeft
                || type == BlockType.TurnRight;
        }

        private static bool IsPairedControl(BlockType type)
        {
            return type == BlockType.While
                || type == BlockType.WhileEnd
                || type == BlockType.If
                || type == BlockType.IfEnd;
        }
    }
}
