using System;
using System.Linq;

namespace SELDLA_G
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine(System.Environment.CurrentDirectory);
            if(args.Length == 0)
            {
                Console.WriteLine("SELDLA-G <mode>");
                Console.WriteLine("  linkage: Linkage analysis mode");
                Console.WriteLine("  hic: Hi-C analysis mode");
            }
            else if(args[0] == "linkage")
            {
                using (var game = new LinkageAnalysis(args.ToList().Where((source, index) => index != 0).ToArray()))
                    game.Run();
            }else if(args[0] == "hic")
            {
                using (var game = new HiCAnalysis(args.ToList().Where((source, index) => index != 0).ToArray()))
                    game.Run();
            }


        }
    }
}
