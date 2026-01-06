using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace RtsSandbox;

public static class Program
{
    public static void Main()
    {
        var opts = WindowOptions.Default;
        opts.Size = new Vector2D<int>(1280, 720);
        opts.Title = "RTS Sandbox (Split Files)";
        opts.VSync = true;

        var window = Window.Create(opts);
        var game = new Game(window);

        window.Load += game.Load;
        window.Update += game.Update;
        window.Render += game.Render;
        window.Closing += game.Closing;

        window.Run();
    }
}
