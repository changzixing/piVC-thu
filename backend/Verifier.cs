/*
    TODO: 这是你唯一允许编辑的文件，你可以对此文件作任意的修改，只要整个项目可以正常地跑起来。
*/

using System;
using System.IO;
using System.Collections.Generic;

namespace piVC_thu
{
    class Verifier
    {
        TextWriter writer;
        SMTSolver solver = new SMTSolver();

        public Verifier(TextWriter writer)
        {
            this.writer = writer;
        }

        // “> 0” 表示所有的 specification 都成立
        // “< 0” 表示有一个 specification 不成立
        // “= 0” 表示 unknown
        public int Apply(IRMain cfg)
        {
            //cfg.Print(writer);
            
            foreach(Predicate predicate in cfg.predicates)
            {
                solver.definePredicate(predicate);
            }
            foreach (Function function in cfg.functions)
            {
                PreconditionBlock head = function.preconditionBlock;
                LinkedList<Block> basicPath = new LinkedList<Block>();
                if (find_basic_path(head.successors.First.Value, head.condition, basicPath))
                {
                    return -1;
                }
            }
            return 1;
        }

        public bool find_basic_path(Block block, Expression precondition, LinkedList<Block> basicPath)
        {
            basicPath.AddLast(block);
            foreach(Block succeccor in block.successors)
            {
                if(succeccor is PostconditionBlock)
                {
                    PostconditionBlock postconditionBlock = (PostconditionBlock)succeccor;
                    Expression postcondition = postconditionBlock.condition;
                    if(wlp_check(basicPath, precondition, postcondition))
                    {
                        return true;
                    }
                }
                else if(succeccor is LoopHeadBlock)
                {
                    if(block != succeccor.predecessors.First.Value)
                    {
                        LoopHeadBlock loopHeadBlock = (LoopHeadBlock)succeccor;
                        Expression postcondition = loopHeadBlock.invariant;
                        if(wlp_check(basicPath, precondition, postcondition))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        LoopHeadBlock loopHeadBlock = (LoopHeadBlock)succeccor;
                        Expression postcondition = loopHeadBlock.invariant;
                        if (wlp_check(basicPath, precondition, postcondition))
                        {
                            return true;
                        }
                        LinkedList<Block> newBasicPath = new LinkedList<Block>();
                        if(find_basic_path(succeccor, postcondition, newBasicPath))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    LinkedList<Block> newBasicPath = new LinkedList<Block>(basicPath);
                    if(find_basic_path(succeccor, precondition, newBasicPath))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool wlp_check(LinkedList<Block> basicPath, Expression precondition, Expression postcondition)
        {
            
            Expression expression = postcondition;
            LinkedListNode<Block> block = basicPath.Last;
            while(block!=null)
            {
                LinkedListNode<Statement> statement = block.Value.statements.Last;
                if(statement == null)
                {
                    block = block.Previous;
                    continue;
                }
                while(true)
                {
                    if (statement.Value is VariableAssignStatement)
                    {
                        VariableAssignStatement variableAssignStatement = (VariableAssignStatement)statement.Value;
                        expression = expression.Substitute(variableAssignStatement.variable, variableAssignStatement.rhs);
                    }
                    else if (statement.Value is AssumeStatement)
                    {
                        AssumeStatement assumeStatement = (AssumeStatement)statement.Value;
                        expression = new ImplicationExpression(assumeStatement.condition, expression);
                    }
                    else if (statement.Value is AssertStatement)
                    {
                        AssertStatement assertStatement = (AssertStatement)statement.Value;
                        expression = new AndExpression(assertStatement.annotation, expression);
                    }
                    else if (statement.Value is FunctionCallStatement)
                    {
                        FunctionCallStatement functionCallStatement = (FunctionCallStatement)statement.Value;
                        Expression functionCallPostCondition = functionCallStatement.rhs.function.postconditionBlock.condition;

                        List<LocalVariable> localVariables = functionCallStatement.lhs;
                        List<LocalVariable> rvs = functionCallStatement.rhs.function.rvs;
                        
                        int cnt = 0;
                        foreach (LocalVariable localVariable in localVariables)
                        {
                            functionCallPostCondition = functionCallPostCondition.Substitute(rvs[cnt], new VariableExpression(localVariable));
                            cnt = cnt + 1;
                        }

                        List<LocalVariable> argumentVariables = functionCallStatement.rhs.argumentVariables;
                        List<LocalVariable> parameters = functionCallStatement.rhs.function.parameters;
                        cnt = 0;
                        foreach(LocalVariable argumentVarible in argumentVariables)
                        {
                            functionCallPostCondition = functionCallPostCondition.Substitute(parameters[cnt], new VariableExpression(argumentVarible));
                            cnt = cnt + 1;
                        }

                        expression = new ImplicationExpression(functionCallPostCondition, expression);
                    }
                    else if (statement.Value is SubscriptAssignStatement)
                    {
                        SubscriptAssignStatement subscriptAssignStatement = (SubscriptAssignStatement)statement.Value;
                        Expression array = new VariableExpression(subscriptAssignStatement.array);
                        ArrayUpdateExpression arrayUpdateExpression = new ArrayUpdateExpression(array, subscriptAssignStatement.subscript, subscriptAssignStatement.rhs, new VariableExpression(subscriptAssignStatement.array.length));
                        expression = expression.Substitute(subscriptAssignStatement.array, arrayUpdateExpression);
                    }

                    //statement.Value.Print(writer);
                    //writer.Write('\n');
                    //expression.Print(writer);
                    //writer.Write('\n');

                    if (statement.Previous == null)
                    {
                        break;
                    }                   
                    statement = statement.Previous;
                }
                block = block.Previous;
            }
            ImplicationExpression implicationExpression = new ImplicationExpression(precondition, expression);
            if (solver.CheckValid(implicationExpression) == null)
            {
                return false;
            }
            return true;
        }
    }
}