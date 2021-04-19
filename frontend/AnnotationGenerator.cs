/*
    这里是生成 annotation 的逻辑，需要生成的 annotation 包括：
    * precondition
    * postcondition
    * ranking function
    * assertion
*/

using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using Antlr4.Runtime.Misc;

namespace piVC_thu
{
    partial class CFGGenerator : piBaseVisitor<Expression?>
    {
        public override Expression VisitAnnotationWithLabel([NotNull] piParser.AnnotationWithLabelContext context)
        {
            // 为了让生活简单一点，我们这里直接忽略了 annotation 的 label。
            // 也许你觉得这种忽略很不爽，但我觉得还挺开心的 >_<
            // 因为这种 label 真的超难处理！
            // 如果你不相信的话，让我来问你一个问题：
            // 对于 loop invariant 的 label，我们该把它放在哪个符号域里呢？
            return TypeConfirm(context.expr(), BoolType.Get());
        }

        // 下面的一些方法不再遵循 visitor pattern，
        // 主要是因为我们需要一个不同的返回值。
        List<Expression> CalcRankingFunction([NotNull] piParser.TerminationContext context)
        {
            return new List<Expression>(context.expr().Select(exprContext => TypeConfirm(exprContext, IntType.Get())));
        }

        PreconditionBlock CalcPreconditionBlock([NotNull] piParser.AnnotationPreContext annotationPreContext, piParser.TerminationContext terminationContext)
        {
            Expression condition = NotNullConfirm(annotationPreContext.expr());
            List<Expression> rankingFunction =
                terminationContext != null
                    ? CalcRankingFunction(terminationContext)
                    : new List<Expression>();
            return new PreconditionBlock
            {
                condition = condition,
                rankingFunction = rankingFunction
            };
        }

        LoopHeadBlock CalcLoopHeadBlock([NotNull] piParser.AnnotationWithLabelContext invariantContext, piParser.TerminationContext terminationContext)
        {
            Debug.Assert(currentFunction != null);
            Debug.Assert(currentBlock != null);

            Expression invariant = NotNullConfirm(invariantContext.expr());
            List<Expression> rankingFunction =
                terminationContext != null
                    ? CalcRankingFunction(terminationContext)
                    : new List<Expression>();
            return new LoopHeadBlock(currentFunction, currentBlock)
            {
                invariant = invariant,
                rankingFunction = rankingFunction
            };
        }

        PostconditionBlock CalcPostconditionBlock([NotNull] piParser.AnnotationPostContext context, List<LocalVariable> rvs)
        {
            // 这里我们开一个只有 rv 的假作用域
            var scope = new Dictionary<string, LocalVariable>();
            Debug.Assert(rvs.Count <= 1);
            foreach (LocalVariable rv in rvs)
            {
                scope.Add("rv", rv);
            }
            symbolTables.Push(scope);

            Expression condition = NotNullConfirm(context);

            symbolTables.Pop();

            return new PostconditionBlock
            {
                condition = condition
            };
        }
    }
}