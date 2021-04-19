/*
    我们把所有与结构体相关的 class 都放在这里了。
    在 IR 的生成过程中，它们会暂时性的出现在 IR 中，
    但在最终的 IR 中，我们隐藏了结构体的概念。
*/

using System.IO;
using System.Collections.Generic;

using System.Diagnostics.Contracts;

namespace piVC_thu
{
    // 我们把 struct 相关的放在这里，因为它们不会出现在最后的 IR 里
    class Struct
    {
        public StructType type;
        public string name;

        public SortedDictionary<string, MemberVariable> members;

        public Struct(string name, SortedDictionary<string, MemberVariable> members)
        {
            this.name = name;
            this.members = members;
            this.type = StructType.Get(this);
        }

        public void Print(TextWriter writer)
        {
            writer.WriteLine($"[struct] {name}\n{{");
            foreach (MemberVariable member in members.Values)
                writer.WriteLine($"\t{member.name}: {member.type};");
            writer.WriteLine("}}");
        }
    }

    sealed class StructType : VarType
    {
        public Struct structDefinition;

        private static Dictionary<Struct, StructType> singletons = new Dictionary<Struct, StructType>();

        private StructType(Struct structDefinition)
        {
            this.structDefinition = structDefinition;
        }

        public static StructType Get(Struct structDefinition)
        {
            if (!singletons.ContainsKey(structDefinition))
            {
                singletons.Add(structDefinition, new StructType(structDefinition));
            }
            return singletons[structDefinition];
        }

        public override string ToString()
        {
            return $"struct {structDefinition.name}";
        }
    }

    class StructVariable : LocalVariable
    {
        public SortedDictionary<string, LocalVariable> members = new SortedDictionary<string, LocalVariable>();

        [ContractInvariantMethod]
        void ObjectInvariant()
        {
            Contract.Invariant(this.type is StructType);
        }

        public StructVariable(StructType type, string name)
        {
            this.type = type;
            this.name = name;
            foreach (MemberVariable mv in type.structDefinition.members.Values)
            {
                if (mv.type is ArrayType)
                {
                    members.Add(mv.name, new ArrayVariable
                    {
                        type = mv.type,
                        name = $"{name}${mv.name}",
                        length = new LocalVariable
                        {
                            type = IntType.Get(),
                            name = CFGGenerator.Counter.GetLength(mv.name)
                        }
                    });
                }
                else
                {
                    members.Add(mv.name, new LocalVariable
                    {
                        type = mv.type,
                        name = $"{name}${mv.name}"
                    });
                }
            }
        }
    }

    // MemberVariable 和其他的不太一样，它是放在结构体的定义里的，
    // 并不会实际出现在表达式里。
    sealed class MemberVariable : Variable { }
}