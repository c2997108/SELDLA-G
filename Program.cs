using System;

namespace SELDLA_G
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine(System.Environment.CurrentDirectory);
            if(args.Length == 0 || args[0] == "linkage")
            {
                using (var game = new LinkageAnalysis())
                    game.Run();
            }else if(args[0] == "hic")
            {
                using (var game = new HiCAnalysis())
                    game.Run();
            }


        }
    }
}
