/*
    TODO: 这是你唯一允许编辑的文件，你可以对此文件作任意的修改，只要整个项目可以正常地跑起来。
*/

using System;
using System.IO;

namespace piVC_thu
{
    class Verifier
    {
        TextWriter writer;

        public Verifier(TextWriter writer)
        {
            this.writer = writer;
        }

        // “> 0” 表示所有的 specification 都成立
        // “< 0” 表示有一个 specification 不成立
        // “= 0” 表示 unknown
        public int Apply(IRMain cfg)
        {
            throw new NotImplementedException("The deductive verification algorithm is not implemented yet.");
        }
    }
}