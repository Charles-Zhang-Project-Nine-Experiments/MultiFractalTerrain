using TerrainGenerator;

namespace DefaultGeneration
{
    internal class Program
    {
        static void Main(string[] args)
        {
            new Generator(new Resolution(350, 500))
                .Pertubate(new (float Frequency, double MaxHeight)[] {
                    (0.001f, 30.0),
                    (0.005f, 30.0),
                    (0.01f, 20.0),
                    (0.07f, 0.8),
                    (0.2f, 0.5),
                })
                .CutOff(new (float Frequency, double MaxHeight)[] {
                    (0.001f, 30.0),
                    (0.005f, 30.0),
                    (0.01f, 20.0),
                    (0.07f, 0.8)
                })
                .Render("Output.png", RenderType.Height, new RenderConfiguration()
                {
                    SeaLevelRatio = 0.2
                });
        }
    }
}