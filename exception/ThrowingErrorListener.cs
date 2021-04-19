/*
    这是出现语法错误时应该报的 exception。
*/

using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace piVC_thu
{
    public class ThrowingErrorListener : BaseErrorListener
    {
        public override void SyntaxError([NotNull] IRecognizer recognizer, [Nullable] IToken offendingSymbol, int line, int charPositionInLine, [NotNull] string msg, [Nullable] RecognitionException e)
        {
            throw new ParseCanceledException($"({line}, {charPositionInLine}): {msg}");
        }
    }
}