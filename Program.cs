﻿/*
    主要的入口及驱动代码。
*/

using System;
using System.IO;
using System.Collections.Generic;

using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

using CommandLine;
using CommandLine.Text;

namespace piVC_thu
{
    // 整个 verifying compiler 的主类
    class Program
    {
        class Options
        {
            [Option("source",
                Required = true,
                HelpText = "The source file of pi language (recommended with filename extension '.pi').")]
            public string sourcePath { get; set; } = default!;

            [Option("printCFG",
                Required = false,
                HelpText = "The file (or 'console') to which the control flow graph is printed.")]
            public string? CFGFile { get; set; } = null;

            [Usage(ApplicationAlias = "piVC-thu")]
            public static IEnumerable<Example> Examples
            {
                get
                {
                    return new List<Example>()
                    {
                        new Example("The simplest usage to compile and verify a pi source file", new Options
                        {
                            sourcePath = "<path>"
                        })
                    };
                }
            }
        }

        // 整个 verifying compiler 的主函数
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed(RunOptions);
        }

        static void RunOptions(Options opts)
        {
            try
            {
                // 首先，我们要生成 cfg！

                StreamReader reader = File.OpenText(opts.sourcePath);

                AntlrInputStream stream = new AntlrInputStream(reader);

                ITokenSource lexer = new piLexer(stream);

                ITokenStream tokens = new CommonTokenStream(lexer);

                piParser parser = new piParser(tokens);

                // 由于现有的 error listener 或者 handler，
                // 要么不会终止 parse，要么连行号都不会打印出来……
                // 所以我们需要写一个新的 listener！
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new ThrowingErrorListener());

                piParser.MainContext tree = parser.main();
                CFGGenerator generator = new CFGGenerator();
                IRMain cfg = generator.Apply(tree);

                if (opts.CFGFile != null)
                {
                    // 输出 cfg
                    using (TextWriter writer = opts.CFGFile == "console"
                        ? Console.Out
                        : new StreamWriter(opts.CFGFile))
                    {
                        cfg.Print(writer);
                    }
                }

                Verifier verifier = new Verifier(Console.Out);
                int result = verifier.Apply(cfg);
                if (result > 0) Console.WriteLine("VERIFIED");
                else if (result == 0) Console.WriteLine("UNKNOWN");
                else if (result < 0) Console.WriteLine("UNVERIFIED");
                Environment.Exit(0);
            }
            catch (ParsingException e)
            {
                Console.Error.WriteLine($"semantic error: {e.Message}");
                Environment.Exit(1);
            }
            catch (ParseCanceledException e)
            {
                Console.Error.WriteLine($"syntax error: {e.Message}");
                Environment.Exit(1);
            }
        }
    }
}