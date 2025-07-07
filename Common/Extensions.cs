using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil.Cil;

namespace RightClickFromInventory.Common
{
    public static class Extensions
    {
        // Credits to https://github.com/absoluteAquarian/SerousCommonLib/blob/bc7b9bca6eff4fc12bfa09b54dc4b4d2ef081010/src/API/Helpers/ILHelper.cs ... I hope this is no issue!
        /// <summary>
		/// Updates the instruction offsets within <paramref name="c"/>
		/// </summary>
		/// <param name="c">The cursor</param>
        public static void UpdateInstructionOffsets(this ILCursor c)
        {
            var instrs = c.Instrs;
            int curOffset = 0;

            static Instruction[] ConvertToInstructions(ILLabel[] labels)
            {
                Instruction[] ret = new Instruction[labels.Length];

                for (int i = 0; i < labels.Length; i++)
                    ret[i] = labels[i].Target;

                return ret;
            }

            foreach (var ins in instrs)
            {
                ins.Offset = curOffset;

                if (ins.OpCode != OpCodes.Switch)
                    curOffset += ins.GetSize();
                else
                {
                    //'switch' opcodes don't like having the operand as an ILLabel[] when calling GetSize()
                    //thus, this is required to even let the mod compile

                    Instruction copy = Instruction.Create(ins.OpCode, ConvertToInstructions((ILLabel[])ins.Operand));
                    curOffset += copy.GetSize();
                }
            }
        }

        public static void RedirectBranchOperands(this ILCursor cursor, ILLabel oldLabel, ILLabel newLabel, params ILLabel[] ignoreLabels) => RedirectBranchOperands(cursor, oldLabel.Target, newLabel.Target, ignoreLabels.Select(illabel => illabel.Target).ToArray());
        public static void RedirectBranchOperands(this ILCursor cursor, Instruction oldInstruction, Instruction newInstruction, params Instruction[] ignoreInstructions)
        {
            int cursorIndex = cursor.Index;

            cursor.Goto(newInstruction);
            ILLabel newLabel = cursor.MarkLabel();

            cursor.Index = cursorIndex;

            for (int i = 0; i < cursor.Instrs.Count; i++)
            {
                Instruction ins = cursor.Instrs[i];

                if (ignoreInstructions.Any(ignore => ignore == ins))
                {
                    continue;
                }

                if (ins.Operand is ILLabel label)
                {
                    if (label.Target == oldInstruction)
                    {
                        cursor.Instrs[i].Operand = newLabel;
                    }
                }
            }
        }

        /// <summary>
        /// Finds instruction relative to the current cursor index. <paramref name="direction"/> 0 is same as <see cref="ILCursor.Next"/>, -1 is same as <see cref="ILCursor.Prev"/>.
        /// </summary>
        /// <param name="cursor"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static Instruction Relative(this ILCursor cursor, int direction)
        {
            return cursor.Instrs[cursor.Index + direction];
        }

        public static ILLabel MarkLabel(this ILCursor cursor, int direction)
        {
            return cursor.MarkLabel(cursor.Instrs[cursor.Index + direction]);
        }
    }
}
