using System.Drawing;

namespace TerrainGenerator
{
    #region Types
    public enum RenderType
    {
        Height,
        Contour
    }
    public struct RenderConfiguration
    {
        public double SeaLevel { get; set; }
    }
    public record Resolution(int Rows, int Columns);
    public record Vector(double X, double Y, double Z);
    public record Edge(int V1, int V2);
    public record Face(int V1, int V2, int V3);
    public record Generation(string ObjectName, string MeshName, Resolution Resolution, Vector[] Vertices, Edge[] Edges, Face[] Faces)
    {
        public double HeightRange
            => MaxHeight - MinHeight;
        public double MaxHeight
            => Vertices.Max(v => v.Z);
        public double MinHeight
            => Vertices.Min(v => v.Z);
        public Vector GetVertex(int row, int col)
            => Vertices[row * Resolution.Columns + col];
    }
    #endregion

    #region Generator
    public sealed class TerrainGenerator
    {
        #region Construciton

        #endregion

        #region Method
        public Generation Generate()
        {
            Resolution resolution = new Resolution(350, 450);
            Vector[][] terrain = GenerateTerrain(resolution);

            // Convert 2D vertices into 1D array
            Vector[] vertices = new Vector[resolution.Rows * resolution.Columns];
            for (int i = 0; i < resolution.Rows * resolution.Columns; i++)
                vertices[i] = terrain[i / resolution.Columns][i % resolution.Columns];

            // Generate triangulated faces
            int faceCount = (resolution.Rows - 1) * (resolution.Columns - 1) * 2;
            Face[] faces = new Face[faceCount];
            for (int i = 0; i < faceCount / 2; i++)
            {
                int colOffset = i % (resolution.Columns - 1);
                int rowOffset = i / (resolution.Columns - 1);
                int totalOffset = colOffset + rowOffset * resolution.Columns;
                faces[i * 2] = new Face(0 + totalOffset, 1 + totalOffset, resolution.Columns + totalOffset);
                faces[i * 2 + 1] = new Face(1 + totalOffset, resolution.Columns + 1 + totalOffset, resolution.Columns + totalOffset);
            }

            return new Generation("Terrain", "Terrain", resolution, vertices, Array.Empty<Edge>(), faces);
        }
        #endregion

        #region Fluent APIs

        #endregion

        #region Helpers
        public static Vector[][] GenerateTerrain(Resolution resolution)
        {
            double[][] grid = GenerateGrid(resolution);

            // Initialize mesh
            Vector[][] mesh = new Vector[resolution.Rows][];
            for (int row = 0; row < resolution.Rows; row++)
            {
                mesh[row] = new Vector[resolution.Columns];
                for (int col = 0; col < resolution.Columns; col++)
                    mesh[row][col] = new Vector(row, col, grid[row][col]);
            }

            return mesh;
        }
        #endregion

        #region Routines
        public static double[][] InitializeGrid(Resolution resolution, double initialValue = 0)
        {
            // Initialize grid
            double[][] grid = new double[resolution.Rows][];
            for (int row = 0; row < resolution.Rows; row++)
            {
                grid[row] = new double[resolution.Columns];
                for (int col = 0; col < resolution.Columns; col++)
                    grid[row][col] = initialValue;
            }
            return grid;
        }
        public static double[][] MultiLayerPertubate(FastNoiseLite noise, Resolution resolution, (float Frequency, double MaxHeight)[] levels)
        {
            // Initialize grid
            double[][] grid = InitializeGrid(resolution, initialValue: 0);

            // Pertubate
            for (int row = 0; row < resolution.Rows; row++)
            {
                for (int col = 0; col < resolution.Columns; col++)
                {
                    foreach (var level in levels)
                    {
                        noise.SetFrequency(level.Frequency);
                        grid[row][col] += noise.GetNoise(row, col) * level.MaxHeight * 2 - level.MaxHeight;
                    }
                }
            }

            return grid;
        }
        public static void Cutoff(double[][] grid, double[][] cutOffGrid, Resolution resolution)
        {
            for (int row = 0; row < resolution.Rows; row++)
            {
                for (int col = 0; col < resolution.Columns; col++)
                {
                    double cutOffHeight = cutOffGrid[row][col];
                    grid[row][col] = grid[row][col] > cutOffHeight ? grid[row][col] : cutOffHeight;
                }
            }
        }
        public static double[][] GenerateGrid(Resolution resolution)
        {
            FastNoiseLite noise = new FastNoiseLite();
            noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);

            double[][] grid = MultiLayerPertubate(
                noise,
                resolution,
                new (float Frequency, double MaxHeight)[] {
            (0.001f, 30.0),
            (0.005f, 30.0),
            (0.01f, 20.0),
            (0.07f, 0.8),
            (0.2f, 0.5),
                }
            );

            double[][] cutOffGrid = MultiLayerPertubate(
                noise,
                resolution,
                new (float Frequency, double MaxHeight)[] {
            (0.001f, 30.0),
            (0.005f, 30.0),
            (0.01f, 20.0),
            (0.07f, 0.8)
                }
            );

            Cutoff(grid, cutOffGrid, resolution);

            return grid;
        }
        #endregion

        #region Bitmap Rendering
        public void RenderHeightMap(Generation generation, string filePath, RenderConfiguration configurations)
        {
            double heightRange = generation.HeightRange;
            double minHeight = generation.MinHeight;

            Bitmap bmp = new Bitmap(generation.Resolution.Columns, generation.Resolution.Rows);
            for (int row = 0; row < generation.Resolution.Rows; row++)
            {
                for (int col = 0; col < generation.Resolution.Columns; col++)
                {
                    double height = generation.GetVertex(row, col).Z;
                    double ratio = (height - minHeight) / heightRange;
                    int value = (int)(ratio * 255);
                    Color color = Color.FromArgb(value, value, value);
                    bmp.SetPixel(col, row, color);
                }
            }
            bmp.Save(Path.GetFullPath(filePath));
        }
        public void RenderContour(Generation generation, string filePath, RenderConfiguration configurations)
        {
            double heightRange = generation.HeightRange;
            double minHeight = generation.MinHeight;
            double maxHeight = generation.MaxHeight;
            double seaLevel = configurations.SeaLevel;
            double buffer = ((double)3 / 255) * heightRange;

            Color lowLandColor = Color.FromArgb(110, 108, 81);
            Color highLandColor = Color.FromArgb(245, 243, 218);
            Color lightSeaColor = Color.FromArgb(61, 168, 204);
            Color darkSeaColor = Color.FromArgb(13, 35, 56);
            double landRange = maxHeight - seaLevel;
            double seaRange = seaLevel - minHeight;

            Bitmap bmp = new Bitmap(generation.Resolution.Columns, generation.Resolution.Rows);
            for (int row = 0; row < generation.Resolution.Rows; row++)
            {
                for (int col = 0; col < generation.Resolution.Columns; col++)
                {
                    double height = generation.GetVertex(row, col).Z;
                    if (Math.Abs(height - seaLevel) <= buffer)
                        bmp.SetPixel(col, row, Color.FromArgb(46, 46, 44)); // Contour line
                    else if (height > seaLevel)
                        bmp.SetPixel(col, row, Interpolate(lowLandColor, highLandColor, (height - seaLevel) / heightRange));  // Land
                    else if (height < seaLevel)
                        bmp.SetPixel(col, row, Interpolate(lightSeaColor, darkSeaColor, (seaLevel - height) / seaRange)); // Sea
                }
            }
            bmp.Save(Path.GetFullPath(filePath));

            Color Interpolate(Color color1, Color color2, double ratio)
            {
                return Color.FromArgb(
                    (int)((double)color1.R + ratio * (color2.R - color1.R)),
                    (int)((double)color1.G + ratio * (color2.G - color1.G)),
                    (int)((double)color1.B + ratio * (color2.B - color1.B))
                );
            }
        }
        public void Render(Generation generation, string filePath, RenderType renderType, RenderConfiguration configurations)
        {
            switch (renderType)
            {
                case RenderType.Height:
                    RenderHeightMap(generation, filePath, configurations);
                    break;
                case RenderType.Contour:
                    RenderContour(generation, filePath, configurations);
                    break;
            }
        }
        #endregion
    }
    #endregion
}