using System;

namespace CatchIfYouCan.Tools
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "test";
            switch (command)
            {
                case "test":
                    return DeterminismSuite.RunAll() ? 0 : 1;
                case "golden":
                    DeterminismSuite.PrintGoldenSeeds();
                    return 0;
                case "report":
                    DeterminismSuite.PrintReport(args.Length > 1 ? int.Parse(args[1]) : 184726392);
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown command '{command}'. Use: test | golden | report [seed]");
                    return 2;
            }
        }
    }
}
