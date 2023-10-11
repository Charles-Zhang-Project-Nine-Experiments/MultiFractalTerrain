using System.Drawing;

namespace TerrainGenerator
{
    #region Types
    public enum RenderType
    {
        Height,
        Relief,
        Contour,
        HeightWithContour
    }
    public class RenderConfiguration
    {
        public double SeaLevelRatio { get; set; } = 0.2;
        public bool ShowSea { get; set; } = true;
        public double ContourLineDensity { get; set; } = 25;
    }
    public record Resolution(int Rows, int Columns);
    public record Mesh(Resolution Resolution, Vector[] Vertices, Edge[] Edges, Face[] Faces)
    {
        public Vector GetVertex(int row, int col)
            => Vertices[row * Resolution.Columns + col];
    }
    public record Vector(double X, double Y, double Z);
    public record Edge(int V1, int V2);
    public record Face(int V1, int V2, int V3);
    #endregion

    #region Generator
    public sealed class Generator
    {
        #region Construciton
        public Generator(Resolution resolution)
        {
            Resolution = resolution;

            Noise = new FastNoiseLite();
            Noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);

            Flatten();
        }
        #endregion

        #region Properties
        public Resolution Resolution { get; }
        public double[][] GridVertices { get; private set; }
        private FastNoiseLite Noise { get; }
        #endregion

        #region Accessor Properties
        public double HeightRange
            => MaxHeight - MinHeight;
        public double MaxHeight
            => GridVertices.Max(v => v.Max());
        public double MinHeight
            => GridVertices.Min(v => v.Min());
        public double GetHeight(int row, int col)
            => GridVertices[row][col];
        #endregion

        #region Fluent APIs
        public Generator Flatten()
        {
            GridVertices = InitializeGrid(Resolution);
            return this;
        }
        public Generator Pertubate((float Frequency, double MaxHeight)[] levels)
        {
            GridVertices = MultiLayerPertubate(
                Noise,
                Resolution,
                levels
            );
            return this;
        }
        public Generator CutOff((float Frequency, double MaxHeight)[] levels)
        {
            double[][] cutOffGrid = MultiLayerPertubate(
                Noise,
                Resolution,
                levels
            );

            CutoffInPlace(GridVertices, cutOffGrid, Resolution);
            return this;
        }
        #endregion

        #region Mesh Creation
        public Generator CreateMesh(out Mesh result)
        {
            // Initialize mesh
            Vector[][] mesh = new Vector[Resolution.Rows][];
            Parallel.For(0, Resolution.Rows, row =>
            {
                mesh[row] = new Vector[Resolution.Columns];
                for (int col = 0; col < Resolution.Columns; col++)
                    mesh[row][col] = new Vector(row, col, GridVertices[row][col]);
            });

            // Convert 2D vertices into 1D array
            Vector[] vertices = new Vector[Resolution.Rows * Resolution.Columns];
            Parallel.For(0, Resolution.Rows * Resolution.Columns, i =>
            {
                vertices[i] = mesh[i / Resolution.Columns][i % Resolution.Columns];
            }); 

            // Generate triangulated faces
            int faceCount = (Resolution.Rows - 1) * (Resolution.Columns - 1) * 2;
            Face[] faces = new Face[faceCount];
            Parallel.For(0, faceCount / 2, i =>
            {
                int colOffset = i % (Resolution.Columns - 1);
                int rowOffset = i / (Resolution.Columns - 1);
                int totalOffset = colOffset + rowOffset * Resolution.Columns;
                faces[i * 2] = new Face(0 + totalOffset, 1 + totalOffset, Resolution.Columns + totalOffset);
                faces[i * 2 + 1] = new Face(1 + totalOffset, Resolution.Columns + 1 + totalOffset, Resolution.Columns + totalOffset);
            });

            result = new Mesh(Resolution, vertices, Array.Empty<Edge>(), faces);
            return this;
        }
        #endregion

        #region Bitmap Rendering
        public Generator Render(string filePath, RenderType renderType, RenderConfiguration configurations)
        {
            switch (renderType)
            {
                case RenderType.Height:
                    RenderHeightMap(this, filePath, configurations);
                    break;
                case RenderType.Relief:
                    RenderRelief(this, filePath, configurations);
                    break;
                case RenderType.Contour:
                    RenderContour(this, filePath, configurations);
                    break;
                case RenderType.HeightWithContour:
                    RenderHeightWithContour(this, filePath, configurations);
                    break;
            }
            return this;
        }
        #endregion

        #region Static Routines
        public static double[][] InitializeGrid(Resolution resolution, double initialValue = 0)
        {
            // Initialize grid
            double[][] grid = new double[resolution.Rows][];
            Parallel.For(0, resolution.Rows, row =>
            {
                grid[row] = new double[resolution.Columns];
                for (int col = 0; col < resolution.Columns; col++)
                    grid[row][col] = initialValue;
            });
            return grid;
        }
        public static double[][] MultiLayerPertubate(FastNoiseLite noise, Resolution resolution, (float Frequency, double MaxHeight)[] levels)
        {
            // Initialize grid
            double[][] grid = InitializeGrid(resolution, initialValue: 0);

            // Pertubate
            foreach (var level in levels)
            {
                Parallel.For(0, resolution.Rows, row =>
                {
                    for (int col = 0; col < resolution.Columns; col++)
                    {

                        noise.SetFrequency(level.Frequency);
                        grid[row][col] += noise.GetNoise(row, col) * level.MaxHeight * 2 - level.MaxHeight;
                    }
                });
            }

            return grid;
        }
        public static void CutoffInPlace(double[][] grid, double[][] cutOffGrid, Resolution resolution)
        {
            Parallel.For(0, resolution.Rows, row =>
            {
                for (int col = 0; col < resolution.Columns; col++)
                {
                    double cutOffHeight = cutOffGrid[row][col];
                    grid[row][col] = grid[row][col] > cutOffHeight ? grid[row][col] : cutOffHeight;
                }
            });
        }
        public static void RenderHeightWithContour(Generator generation, string filePath, RenderConfiguration configurations)
        {
            double heightRange = generation.HeightRange;
            double minHeight = generation.MinHeight;
            double contourSpan = heightRange / configurations.ContourLineDensity;
            double seaLevel = configurations.SeaLevelRatio * heightRange + minHeight;

            Color counterLineMainColor = Color.FromArgb(36, 30, 29);
            Color seaInterpColor = Color.FromArgb(61, 168, 204);
            double spill = 0.1;

            Bitmap bmp = new(generation.Resolution.Columns, generation.Resolution.Rows);
            for (int row = 0; row < generation.Resolution.Rows; row++)
            {
                for (int col = 0; col < generation.Resolution.Columns; col++)
                {
                    double height = generation.GetHeight(row, col);
                    double remainder = (height - minHeight) % contourSpan;
                    if (configurations.ShowSea && height < seaLevel)
                    {
                        if (Math.Abs(remainder - contourSpan) < contourSpan * spill)
                            bmp.SetPixel(col, row, Interpolate(seaInterpColor, counterLineMainColor, Math.Abs(remainder - contourSpan) / (contourSpan * spill)));
                        else
                            bmp.SetPixel(col, row, seaInterpColor);
                    }
                    else
                    {
                        double ratio = (height - minHeight) / heightRange;
                        int value = (int)(ratio * 255);
                        Color landInterpColor = Color.FromArgb(value, value, value);

                        if (Math.Abs(remainder - contourSpan) < contourSpan * spill)
                            bmp.SetPixel(col, row, Interpolate(landInterpColor, counterLineMainColor, Math.Abs(remainder - contourSpan) / (contourSpan * spill)));
                        else
                        {
                            bmp.SetPixel(col, row, landInterpColor);
                        }
                    }
                }
            }
            bmp.Save(Path.GetFullPath(filePath));
        }
        public static void RenderContour(Generator generation, string filePath, RenderConfiguration configurations)
        {
            double heightRange = generation.HeightRange;
            double minHeight = generation.MinHeight;
            double contourSpan = heightRange / configurations.ContourLineDensity;

            Bitmap bmp = new(generation.Resolution.Columns, generation.Resolution.Rows);
            Parallel.For(0, generation.Resolution.Rows, row =>
            {
                for (int col = 0; col < generation.Resolution.Columns; col++)
                {
                    double height = generation.GetHeight(row, col);
                    double remainder = (height - minHeight) % contourSpan;
                    if (Math.Abs(remainder - contourSpan) < contourSpan * 0.05)
                        bmp.SetPixel(col, row, Interpolate(Color.FromArgb(36, 30, 29), Color.FromArgb(12, 12, 12), Math.Abs(remainder - contourSpan) / (contourSpan * 0.05)));
                    else
                        bmp.SetPixel(col, row, Color.FromArgb(224, 222, 222));
                }
            });
            bmp.Save(Path.GetFullPath(filePath));
        }
        public static void RenderHeightMap(Generator generation, string filePath, RenderConfiguration configurations)
        {
            double heightRange = generation.HeightRange;
            double minHeight = generation.MinHeight;
            double seaLevel = configurations.SeaLevelRatio * heightRange + minHeight;

            Bitmap bmp = new(generation.Resolution.Columns, generation.Resolution.Rows);
            Parallel.For(0, generation.Resolution.Rows, row =>
            {
                for (int col = 0; col < generation.Resolution.Columns; col++)
                {
                    double height = generation.GetHeight(row, col);
                    if (configurations.ShowSea && height < seaLevel)
                        bmp.SetPixel(col, row, Color.FromArgb(61, 168, 204));
                    else
                    {
                        double ratio = (height - minHeight) / heightRange;
                        int value = (int)(ratio * 255);
                        Color color = Color.FromArgb(value, value, value);
                        bmp.SetPixel(col, row, color);
                    }
                }
            });
            bmp.Save(Path.GetFullPath(filePath));
        }
        public static void RenderRelief(Generator generation, string filePath, RenderConfiguration configurations)
        {
            double heightRange = generation.HeightRange;
            double minHeight = generation.MinHeight;
            double maxHeight = generation.MaxHeight;
            double seaLevel = configurations.SeaLevelRatio * heightRange + minHeight;
            double buffer = ((double)3 / 255) * heightRange;

            Color lowLandColor = Color.FromArgb(110, 108, 81);
            Color highLandColor = Color.FromArgb(245, 243, 218);
            Color lightSeaColor = Color.FromArgb(61, 168, 204);
            Color darkSeaColor = Color.FromArgb(13, 35, 56);
            double landRange = maxHeight - seaLevel;
            double seaRange = seaLevel - minHeight;

            Bitmap bmp = new(generation.Resolution.Columns, generation.Resolution.Rows);
            Parallel.For(0, generation.Resolution.Rows, row =>
            {
                for (int col = 0; col < generation.Resolution.Columns; col++)
                {
                    double height = generation.GetHeight(row, col);
                    if (Math.Abs(height - seaLevel) <= buffer)
                        bmp.SetPixel(col, row, Color.FromArgb(46, 46, 44)); // Continent line
                    else if (height > seaLevel)
                        bmp.SetPixel(col, row, Interpolate(lowLandColor, highLandColor, (height - seaLevel) / landRange));  // Land
                    else if (height < seaLevel)
                        bmp.SetPixel(col, row, Interpolate(lightSeaColor, darkSeaColor, (seaLevel - height) / seaRange)); // Sea
                }
            });
            bmp.Save(Path.GetFullPath(filePath));
        }
        public static Color Interpolate(Color color1, Color color2, double ratio)
        {
            return Color.FromArgb(
                (int)((double)color1.R + ratio * (color2.R - color1.R)),
                (int)((double)color1.G + ratio * (color2.G - color1.G)),
                (int)((double)color1.B + ratio * (color2.B - color1.B))
            );
        }
        #endregion
    }
    #endregion
}