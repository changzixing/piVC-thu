/*
    这里是生成最顶层的 CFG 的代码。

    CFG 的生成过程是在 ANTLR 生成的 CST 上做单遍的遍历，这是一个 vistor pattern。
    但为了让代码结构更清晰，这里我们通过 C# 的 partial class 特性，
    把 CFGGenerator 的实现拆分进了五个文件中：
        CFGGenerator.cs：最顶层的生成逻辑
        AnnotationGenerator.cs：生成 annotation 的逻辑
        ExprGenerator.cs：生成表达式的逻辑
        StmtGenerator.cs：生成 statement 的逻辑
        Utils.cs：一些工具函数和工具类
    
    注意：在生成的过程中，我们是有 struct 相关的东西的，但在最终的 IR 中我们会隐藏 struct
*/

using System.Collections.Generic;

using System.Diagnostics;

using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace piVC_thu
{
    // 整个 frontend 其实都是 CFGGenerator 这一个 class，
    // 由于 class 里的实现代码太大，为了方便组织，利用 C# 的 partial class 我们将其划分成了几个文件。
    //
    // 我们的语言设计有一个绝妙的地方在于：它是没有 side effect 的。
    // 这样的话，不少 C/C++ 里的 UB 在我们这里是不存在的，比如 `a[++i] = ++i;` 这种。
    partial class CFGGenerator : piBaseVisitor<Expression?>
    {
        // 最终计算出来的 IR 主体
        IRMain main = default!;

        // 当前正在计算的 function
        Function? currentFunction;
        // block
        Block? currentBlock = null;
        // post block of loop (for break statement)
        BasicBlock? postLoopBlock = null;

        // 符号表
        Dictionary<string, Function> functionTable = new Dictionary<string, Function>();
        Dictionary<string, Struct> structTable = new Dictionary<string, Struct>();
        Dictionary<string, Predicate> predicateTable = new Dictionary<string, Predicate>();
        Stack<Dictionary<string, LocalVariable>> symbolTables = new Stack<Dictionary<string, LocalVariable>>();

        // 用来作 alpha renaming，以及用来生成临时变量
        Counter counter = new Counter();

        // 主要是用来帮助表达式知道自己现在是否在一个 annotation 里
        bool? annotated = null;

        // 真正的主函数
        public IRMain Apply([NotNull] piParser.MainContext context)
        {
            main = new IRMain();
            Visit(context);

            // 把函数和谓词的参数、返回值和类型“拍扁”
            // 也就是说把 struct 消解成几个普通的变量
            foreach (Function function in functionTable.Values)
            {
                // 拍扁参数及其类型
                List<LocalVariable> flattenedParameters = new List<LocalVariable>();
                List<VarType> flattenedParaTypes = new List<VarType>();
                for (int i = 0; i < function.parameters.Count; ++i)
                {
                    if (function.parameters[i] is StructVariable structParameter)
                    {
                        foreach (LocalVariable member in structParameter.members.Values)
                        {
                            flattenedParameters.Add(member);
                            flattenedParaTypes.Add(member.type);
                        }
                    }
                    else
                    {
                        flattenedParameters.Add(function.parameters[i]);
                        flattenedParaTypes.Add(function.parameters[i].type);
                    }
                }
                function.parameters = flattenedParameters;

                if (function.rvs.Count > 0 && function.rvs[0] is StructVariable structRV)
                {
                    // 拍扁返回值
                    Debug.Assert(function.rvs.Count == 1);

                    List<LocalVariable> flattenedRV = new List<LocalVariable>();
                    foreach (LocalVariable member in structRV.members.Values)
                    {
                        flattenedRV.Add(member);
                    }
                    function.rvs = flattenedRV;
                }

                List<VarType> flattenedReturnTypes = new List<VarType>();
                foreach (LocalVariable rv in function.rvs)
                {
                    flattenedReturnTypes.Add(rv.type);
                }
                function.type = FunType.Get(flattenedReturnTypes, flattenedParaTypes);
            }
            foreach (Predicate predicate in predicateTable.Values)
            {
                // 拍扁参数及其类型
                List<LocalVariable> flattenedParameters = new List<LocalVariable>();
                List<VarType> flattenedParaTypes = new List<VarType>();
                for (int i = 0; i < predicate.parameters.Count; ++i)
                {
                    if (predicate.parameters[i] is StructVariable structParameter)
                    {
                        foreach (LocalVariable member in structParameter.members.Values)
                        {
                            flattenedParameters.Add(member);
                            flattenedParaTypes.Add(member.type);
                        }
                    }
                    else
                    {
                        flattenedParameters.Add(predicate.parameters[i]);
                        flattenedParaTypes.Add(predicate.parameters[i].type);
                    }
                }
                predicate.type = PredType.Get(flattenedParaTypes);
                predicate.parameters = flattenedParameters;
            }

            return main;
        }

        public override Expression? VisitMain([NotNull] piParser.MainContext context)
        {
            foreach (var def in context.def())
                Visit(def);
            return null;
        }

        public override Expression? VisitFuncDef([NotNull] piParser.FuncDefContext context)
        {
            string name = CalcDefName(context, context.IDENT());

            symbolTables.Push(new Dictionary<string, LocalVariable>());

            // 把所有的参数加到符号表里
            int paraNum = context.var().Length;
            List<VarType> paraTypes = new List<VarType>();
            List<LocalVariable> parameters = new List<LocalVariable>();
            HashSet<string> paraNames = new HashSet<string>();
            for (int i = 0; i < paraNum; ++i)
            {
                var ctx = context.var()[i];
                paraTypes.Add(CalcType(ctx.type()));
                string paraName = ctx.IDENT().GetText();
                if (paraName == "rv")
                    throw new ParsingException(ctx, "'rv' is not permitted as parameter name, as it's used to indicate the return value in postcondition.");
                if (!paraNames.Contains(paraName))
                    paraNames.Add(paraName);
                else
                    throw new ParsingException(ctx, $"duplicate function parameter '{paraName}'");

                LocalVariable paraVariable;
                string paraVarName = counter.GetVariable(paraName);
                if (paraTypes[i] is StructType sv)
                {
                    paraVariable = new StructVariable(sv, paraVarName);
                }
                else if (paraTypes[i] is ArrayType av)
                {
                    paraVariable = new ArrayVariable
                    {
                        type = paraTypes[i],
                        name = paraVarName,
                        length = new LocalVariable
                        {
                            type = IntType.Get(),
                            name = Counter.GetLength(paraVarName)
                        }
                    };
                }
                else
                {
                    paraVariable = new LocalVariable
                    {
                        type = paraTypes[i],
                        name = paraVarName
                    };
                }

                parameters.Add(paraVariable);
                symbolTables.Peek().Add(paraName, paraVariable);
            }

            // 算出 returnType，如果其不是 void，那么就搞出一个 rv 来
            List<VarType> returnTypes = new List<VarType>();
            List<LocalVariable> rvs = new List<LocalVariable>();
            if (context.type() != null)
            {
                VarType returnType = CalcType(context.type());
                returnTypes.Add(returnType);

                if (returnType is VarType varType)
                {
                    string rvName = counter.GetVariable("rv");
                    if (varType is StructType sv)
                    {
                        rvs.Add(new StructVariable(sv, rvName));
                    }
                    else if (varType is ArrayType av)
                    {
                        rvs.Add(new ArrayVariable
                        {
                            type = varType,
                            name = rvName,
                            length = new LocalVariable
                            {
                                type = IntType.Get(),
                                name = Counter.GetLength(rvName)
                            }
                        });
                    }
                    else
                    {
                        rvs.Add(new LocalVariable
                        {
                            type = varType,
                            name = rvName
                        });
                    }
                }
            }

            annotated = true;
            PreconditionBlock preconditionBlock = CalcPreconditionBlock(context.beforeFunc().annotationPre(), context.beforeFunc().termination());
            PostconditionBlock postconditionBlock = CalcPostconditionBlock(context.beforeFunc().annotationPost(), rvs);
            annotated = null;

            currentFunction = new Function
            {
                type = FunType.Get(returnTypes, paraTypes),
                name = name,
                parameters = parameters,
                preconditionBlock = preconditionBlock,
                postconditionBlock = postconditionBlock,
                rvs = rvs
            };
            main.functions.AddLast(currentFunction);
            functionTable.Add(name, currentFunction);

            // visit function body
            currentBlock = new BasicBlock(currentFunction, preconditionBlock);

            // 逐次访问函数中的每一条语句
            annotated = false;
            foreach (var stmt in context.stmt())
                Visit(stmt);
            annotated = null;

            // 理想情况下，currentBasicBlock 应该是空，这表示所有的 path 都已经被 return 了
            if (currentBlock != null)
            {
                if (returnTypes.Count == 0)
                { // 如果函数的返回值类型是 void 的话，我们是允许隐式的 return 的
                    Block.AddEdge(currentBlock, postconditionBlock);
                }
                else
                    throw new ParsingException(context, $"function '{name}' does not return in all paths.");
            }

            // 搞定这个函数啦~
            symbolTables.Pop();
            currentFunction = null;

            return null;
        }

        public override Expression? VisitStructDef([NotNull] piParser.StructDefContext context)
        {
            string name = CalcDefName(context, context.IDENT());

            // parse member variables
            var members = new SortedDictionary<string, MemberVariable>();
            foreach (var member in context.var())
            {
                string memberName = member.IDENT().GetText();
                MemberVariable memberVariable = new MemberVariable
                {
                    type = CalcType(member.type()),
                    name = memberName
                };
                if (!members.ContainsKey(name))
                    members.Add(memberName, memberVariable);
                else
                    throw new ParsingException(member, $"duplicate struct member '{memberName}'");
            }
            Struct s = new Struct(name, members);
            structTable.Add(name, s);
            return null;
        }

        public override Expression? VisitPredDef([NotNull] piParser.PredDefContext context)
        {
            string name = CalcDefName(context, context.IDENT());

            symbolTables.Push(new Dictionary<string, LocalVariable>());

            // calculate parameters
            int paraNum = context.var().Length;
            List<LocalVariable> parameters = new List<LocalVariable>();
            List<VarType> paraTypes = new List<VarType>();
            HashSet<string> paraNames = new HashSet<string>();
            for (int i = 0; i < paraNum; ++i)
            {
                var ctx = context.var()[i];
                paraTypes.Add(CalcType(ctx.type()));
                string paraName = ctx.IDENT().GetText();
                if (!paraNames.Contains(paraName))
                    paraNames.Add(paraName);
                else
                    throw new ParsingException(ctx, $"duplicate predicate parameter '{paraName}'");

                LocalVariable paraVariable;
                string paraVarName = counter.GetVariable(paraName);
                if (paraTypes[i] is StructType sv)
                {
                    paraVariable = new StructVariable(sv, paraVarName);
                }
                else if (paraTypes[i] is ArrayType av)
                {
                    paraVariable = new ArrayVariable
                    {
                        type = paraTypes[i],
                        name = paraVarName,
                        length = new LocalVariable
                        {
                            type = IntType.Get(),
                            name = Counter.GetLength(paraVarName)
                        }
                    };
                }
                else
                {
                    paraVariable = new LocalVariable
                    {
                        type = paraTypes[i],
                        name = paraVarName
                    };
                }
                parameters.Add(paraVariable);

                symbolTables.Peek().Add(paraName, paraVariable);
            }

            annotated = true;
            Expression expression = NotNullConfirm(context.expr());
            annotated = null;

            // 最后再把这个加到谓词表里，避免其表达式中有对自身的引用。

            Predicate predicate = new Predicate
            {
                type = PredType.Get(paraTypes),
                name = name,
                parameters = parameters,
                expression = expression
            };
            // 这里我们需要在表达式算完之后再将谓词名放到表里，
            // 因为函数可以递归调用自身，但是谓词是不行的
            predicateTable.Add(name, predicate);
            main.predicates.AddLast(predicate);

            symbolTables.Pop();

            return null;
        }

        // ==== utils just for top level definitions ====

        string CalcDefName(ParserRuleContext context, ITerminalNode node)
        {
            string name = node.GetText();
            // check if the name is used by a previous function, struct or predicate
            if (functionTable.ContainsKey(name))
                throw new ParsingException(context, $"a function named '{name}' has already been defined");
            if (structTable.ContainsKey(name))
                throw new ParsingException(context, $"a struct named '{name}' has already been defined");
            if (predicateTable.ContainsKey(name))
                throw new ParsingException(context, $"a predicate named '{name}' has already been defined");
            return name;
        }
    }
}