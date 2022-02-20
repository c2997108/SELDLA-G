using System;

namespace SELDLA_G
{
    public static class Program
    {
        [STAThread]
        static void Main2()
        {
            using (var game = new Game1())
                game.Run();
        }
    }
}
