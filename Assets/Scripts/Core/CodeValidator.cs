using System.Collections.Generic;

namespace CodeCubesPC
{
    /// <summary>
    /// 逐行移植原项目 Assets/scripts/CodeValidator.cs，报错文案逐字一致。
    /// 唯一改动：If/While/IfEnd/WhileEnd/Else 的 is 判定 → BlockType 比较。
    /// </summary>
    public static class CodeValidator
    {
        public struct Error
        {
            public string message;
        }

        public static List<Error> Validate(CodeBlock startBlock, BlockType[] suggestedBlockNames = null)
        {
            var errors = new List<Error>();
            if (startBlock == null) return errors;

            DetectCycle(startBlock, errors);
            if (errors.Count > 0) return errors;

            DetectStructureMismatch(startBlock, errors);
            DetectExtraElse(startBlock, errors);
            DetectMissingJudger(startBlock, errors);
            DetectMissingRequiredStructures(startBlock, suggestedBlockNames, errors);

            return errors;
        }

        private static void DetectCycle(CodeBlock start, List<Error> errors)
        {
            var visited = new HashSet<CodeBlock>();
            CodeBlock cur = start;

            while (cur != null)
            {
                if (!visited.Add(cur))
                {
                    errors.Add(new Error { message = string.Format("Cycle detected: '{0}'", cur.name) });
                    return;
                }
                cur = cur.next;
            }
        }

        private static void DetectStructureMismatch(CodeBlock start, List<Error> errors)
        {
            int ifDepth = 0;
            int whileDepth = 0;
            CodeBlock cur = start;

            while (cur != null)
            {
                if (cur.type == BlockType.If) ifDepth++;
                else if (cur.type == BlockType.IfEnd)
                {
                    ifDepth--;
                    if (ifDepth < 0)
                    {
                        errors.Add(new Error { message = string.Format("Extra IfEnd: '{0}'", cur.name) });
                        ifDepth = 0;
                    }
                }
                else if (cur.type == BlockType.While) whileDepth++;
                else if (cur.type == BlockType.WhileEnd)
                {
                    whileDepth--;
                    if (whileDepth < 0)
                    {
                        errors.Add(new Error { message = string.Format("Extra WhileEnd: '{0}'", cur.name) });
                        whileDepth = 0;
                    }
                }
                else if (cur.type == BlockType.Else && ifDepth == 0 && whileDepth == 0)
                {
                    errors.Add(new Error { message = string.Format("Else '{0}' outside any If block", cur.name) });
                }
                cur = cur.next;
            }

            if (ifDepth > 0)
                errors.Add(new Error { message = string.Format("Missing {0} IfEnd(s)", ifDepth) });
            if (whileDepth > 0)
                errors.Add(new Error { message = string.Format("Missing {0} WhileEnd(s)", whileDepth) });
        }

        private static void DetectExtraElse(CodeBlock start, List<Error> errors)
        {
            CodeBlock cur = start;
            while (cur != null)
            {
                if (cur.type == BlockType.If)
                {
                    int elseCount = 0;
                    int depth = 0;
                    CodeBlock scan = cur.next;

                    while (scan != null)
                    {
                        if (scan.type == BlockType.If) depth++;
                        else if (scan.type == BlockType.Else)
                        {
                            if (depth == 0)
                            {
                                elseCount++;
                                if (elseCount > 1)
                                {
                                    errors.Add(new Error
                                    {
                                        message = string.Format("If '{0}' has multiple Else blocks", cur.name)
                                    });
                                    break;
                                }
                            }
                        }
                        else if (scan.type == BlockType.IfEnd && depth == 0) break;
                        scan = scan.next;
                    }
                }
                cur = cur.next;
            }
        }

        private static void DetectMissingJudger(CodeBlock start, List<Error> errors)
        {
            CodeBlock cur = start;
            while (cur != null)
            {
                if (cur.type == BlockType.If && cur.judger == null)
                    errors.Add(new Error { message = string.Format("If '{0}' missing condition", cur.name) });
                else if (cur.type == BlockType.While && cur.judger == null)
                    errors.Add(new Error { message = string.Format("While '{0}' missing condition", cur.name) });
                cur = cur.next;
            }
        }

        private static void DetectMissingRequiredStructures(CodeBlock start, BlockType[] suggested, List<Error> errors)
        {
            if (!LevelBlockHints.ShouldRequireControlFlow(suggested))
                return;

            bool needIf = LevelBlockHints.HasName(suggested, BlockType.If);
            bool needWhile = LevelBlockHints.HasName(suggested, BlockType.While);
            bool needElse = LevelBlockHints.HasName(suggested, BlockType.Else);

            bool hasIf = false;
            bool hasWhile = false;
            bool hasElse = false;
            CodeBlock cur = start;

            while (cur != null)
            {
                if (cur.type == BlockType.If) hasIf = true;
                else if (cur.type == BlockType.While) hasWhile = true;
                else if (cur.type == BlockType.Else) hasElse = true;
                cur = cur.next;
            }

            if (needIf && !hasIf)
                errors.Add(new Error { message = "This level requires an IF block." });
            if (needWhile && !hasWhile)
                errors.Add(new Error { message = "This level requires a While block." });
            if (needElse && !hasElse)
                errors.Add(new Error { message = "This level requires an Else block." });
        }
    }
}
