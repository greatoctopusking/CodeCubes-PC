using System;
using UnityEngine;

namespace CodeCubesPC
{
    /// <summary>
    /// 2D 版代码块类型。与 VR 原项目 Assets/scripts 下 15 个 Code 子类一一对应：
    ///   Start          &lt;-&gt; Start.cs
    ///   MoveForward    &lt;-&gt; MoveCode.cs
    ///   TurnLeft       &lt;-&gt; TurnLeftCode.cs
    ///   TurnRight      &lt;-&gt; TurnRightCode.cs
    ///   While          &lt;-&gt; While.cs
    ///   WhileEnd       &lt;-&gt; WhileEnd.cs
    ///   If             &lt;-&gt; If.cs
    ///   IfEnd          &lt;-&gt; IfEnd.cs
    ///   Else           &lt;-&gt; Else.cs
    ///   True           &lt;-&gt; TrueCode.cs
    ///   False          &lt;-&gt; FalseCode.cs
    ///   DetectFrontStar&lt;-&gt; StarFrontCode.cs
    ///   DetectLeftStar &lt;-&gt; StarLeftCode.cs
    ///   DetectRightStar&lt;-&gt; StarRightCode.cs
    ///   StarRemain     &lt;-&gt; StarRemainCode.cs
    ///
    /// 原项目每个块是一个独立 MonoBehaviour 子类；2D 版合并为单一 CodeBlock + 本枚举，
    /// 行为逻辑（work() 的语义）保持完全一致，但只有一处可审计的实现。
    /// </summary>
    public enum BlockType
    {
        Start = 0,
        MoveForward = 1,
        TurnLeft = 2,
        TurnRight = 3,
        While = 4,
        WhileEnd = 5,
        If = 6,
        IfEnd = 7,
        Else = 8,
        True = 9,
        False = 10,
        DetectFrontStar = 11,
        DetectLeftStar = 12,
        DetectRightStar = 13,
        StarRemain = 14
    }

    public enum BlockCategory
    {
        /// <summary>Start：程序入口。</summary>
        Other = 0,
        /// <summary>MoveForward / TurnLeft / TurnRight。</summary>
        Move = 1,
        /// <summary>While / WhileEnd / If / IfEnd / Else。</summary>
        Control = 2,
        /// <summary>True / False / Detect* / StarRemain（原项目 BoolCode 子类）。</summary>
        Bool = 3
    }

    public static class BlockTypes
    {
        /// <summary>15 种块，顺序与 CodeBlockCatalog.asset 的 entries 顺序一致。</summary>
        public static readonly BlockType[] All = (BlockType[])Enum.GetValues(typeof(BlockType));

        public static string DisplayName(BlockType type)
        {
            switch (type)
            {
                case BlockType.Start: return "Start";
                case BlockType.MoveForward: return "MoveForward";
                case BlockType.TurnLeft: return "TurnLeft";
                case BlockType.TurnRight: return "TurnRight";
                case BlockType.While: return "While";
                case BlockType.WhileEnd: return "WhileEnd";
                case BlockType.If: return "IF";
                case BlockType.IfEnd: return "IfEnd";
                case BlockType.Else: return "Else";
                case BlockType.True: return "True";
                case BlockType.False: return "False";
                case BlockType.DetectFrontStar: return "DetectFrontStar";
                case BlockType.DetectLeftStar: return "DetectLeftStar";
                case BlockType.DetectRightStar: return "DetectRightStar";
                case BlockType.StarRemain: return "StarRemain";
            }

            return type.ToString();
        }

        public static BlockCategory Category(BlockType type)
        {
            switch (type)
            {
                case BlockType.MoveForward:
                case BlockType.TurnLeft:
                case BlockType.TurnRight:
                    return BlockCategory.Move;

                case BlockType.While:
                case BlockType.WhileEnd:
                case BlockType.If:
                case BlockType.IfEnd:
                case BlockType.Else:
                    return BlockCategory.Control;

                case BlockType.True:
                case BlockType.False:
                case BlockType.DetectFrontStar:
                case BlockType.DetectLeftStar:
                case BlockType.DetectRightStar:
                case BlockType.StarRemain:
                    return BlockCategory.Bool;

                default:
                    return BlockCategory.Other;
            }
        }

        /// <summary>原项目 BoolCode 子类的判定。</summary>
        public static bool IsBool(BlockType type)
        {
            return Category(type) == BlockCategory.Bool;
        }

        /// <summary>原项目 ConnectionManager.IsConnectable 允许的块类型。</summary>
        public static bool IsConnectable(BlockType type)
        {
            switch (type)
            {
                case BlockType.MoveForward:
                case BlockType.TurnLeft:
                case BlockType.TurnRight:
                case BlockType.While:
                case BlockType.WhileEnd:
                case BlockType.If:
                case BlockType.Else:
                case BlockType.IfEnd:
                case BlockType.Start:
                    return true;
                default:
                    return IsBool(type);
            }
        }

        /// <summary>该类型是否带 Judger 条件槽位（原项目 While.Judger / If.Judger）。</summary>
        public static bool HasJudgerSlot(BlockType type)
        {
            return type == BlockType.While || type == BlockType.If;
        }

        public static Color BaseColor(BlockType type)
        {
            switch (Category(type))
            {
                case BlockCategory.Move: return new Color(0.184f, 0.435f, 0.878f, 1f);   // 蓝
                case BlockCategory.Control: return new Color(0.541f, 0.310f, 0.847f, 1f); // 紫
                case BlockCategory.Bool: return new Color(0.122f, 0.663f, 0.478f, 1f);    // 绿
                default: return new Color(0.937f, 0.937f, 0.937f, 1f);                     // Start 浅灰
            }
        }

        public static Color TextColor(BlockType type)
        {
            return Category(type) == BlockCategory.Other
                ? new Color(0.12f, 0.12f, 0.14f, 1f)
                : Color.white;
        }
    }
}
