using System;

namespace SELDLA_G
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            Console.WriteLine(System.Environment.CurrentDirectory);
            using (var game = new Game1())
                game.Run();
            //using (var game = new Game2())
            //    game.Run();
        }
    }
}
