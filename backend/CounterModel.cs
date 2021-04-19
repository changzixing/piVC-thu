using System.IO;
using System.Collections.Generic;

namespace piVC_thu
{
    class CounterModel
    {
        // 一个比较悲哀的事实是：
        // 由于 Z3 对 array 的处理方式是将其看做是一个特殊的 uninterpreted function，
        // 所以其在 model 中可能会是一个比较复杂的表达式。
        // 如果有同学有兴趣了解其含义的话，可以参阅 Z3 的文档：
        // https://www.rise4fun.com/z3/tutorialcontent/guide
        Dictionary<string, string> assignments = default!;

        public CounterModel(Dictionary<string, string> assignments)
        {
            this.assignments = assignments;
        }

        public void Print(TextWriter writer)
        {
            writer.WriteLine("*** COUNTER MODEL");
            foreach ((string name, string assignment) in assignments)
            {
                writer.WriteLine($"{name} := {assignment}");
            }
            writer.WriteLine("*** END COUNTER MODEL");
        }
    }
}