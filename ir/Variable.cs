/*
    变量。

    在最终的 IR 中，所有变量都会被视作局部变量（LocalVariable）来处理，数组和 bound variable 会被视作特殊的局部变量。

    不过在 IR 的生成过程中，为了方便，我们还会有成员变量和结构体变量，可见于 frontend/Struct.cs。
*/

using System.Diagnostics.Contracts;

namespace piVC_thu
{
    abstract class Variable
    {
        public VarType type = default!;
        public string name = default!;
    }

    // 包括局部变量，函数参数，辅助变量
    // 额外的，成员变量也会被转成 LocalVariable 来处理
    class LocalVariable : Variable { }

    class ArrayVariable : LocalVariable
    {
        // 只有在 new array 的时候需要对 length 作一个非负性的 runtime assertion
        public LocalVariable length = default!;
    }

    sealed class QuantifiedVariable : LocalVariable
    {
        [ContractInvariantMethod]
        void ObjectInvariant()
        {
            Contract.Invariant(this.type is IntType);
        }
    }
}