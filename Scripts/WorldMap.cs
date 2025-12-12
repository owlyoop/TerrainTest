using Godot;
using System;
using System.Collections.Generic;

public partial class WorldMap : Node
{
    //Equirectangular projection. dont feel like going down a rabbithole of learning how to map hexagons on a sphere or something. maybe for a future project.
    [Export] public int worldWidth = 200;
    [Export] public int worldHeight = 100;

    [Export] public float scale = 1.0f;

    [Export] public FastNoiseLite noiseGen;
    [Export] public NoiseTexture2D noiseTex;
    [Export] public MeshInstance2D mapDisplay;
    [Export] public VoronoiWorld voronoi;

    Cell2D[,] cells;
    List<Plate2D> plates;
    Image img;


    int timestep = 1;

    public override void _Ready()
    {
        plates = new List<Plate2D>();
        int ID = plates.Count;
        foreach (var s in voronoi.basePolygons)
        {
            
            Plate2D plate = new Plate2D(s.Key, ID);
            plates.Add(plate);
            ID++;
        }

        GenerateCells(worldWidth, worldHeight);

        CreateMesh();
        
    }

    //Main Tectonic Plate Loop
    public void Timestep()
    {
        //move plates

        //update cell ownership

        //redraw map
    }

    void AddTectonicPlate(Plate2D plate)
    {
        plates.Add(plate);
    }

    void GenerateCells(int width, int height)
    {
        cells = new Cell2D[width, height];
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                cells[i,j] = new Cell2D(i, j);
                cells[i,j].SetHeight(noiseGen.GetNoise2D(i,j));
            }
        }
    }

    void CreateMesh()
	{
        mapDisplay.Scale = new Vector2(worldWidth, worldHeight);
        mapDisplay.Position = new Vector2(worldWidth / 2f, worldHeight / 2f);

        img = CreateImageFromCells();
        AssignSiteIDsOnCells();
        //FillVoronoiCells();
        RasterizeVoronoiEdges();
        //FloodFill();
        //MovePlateTest();
        var texture = ImageTexture.CreateFromImage(img);
        mapDisplay.Texture = texture;
    }

    public Cell2D GetCellFromPosition(Vector2 pos)
    {
        if ((int)pos.X >= 0 && (int)pos.X < worldWidth &&
            (int)pos.Y >= 0 && (int)pos.Y < worldHeight)
        {
            return cells[(int)pos.X, (int)pos.Y];
        }
        else return null;
    }

    //Image uses 0,0 as the topleft but 2d arrays use 0,0 as bottomleff
    void SetPixelWorld(int x, int y, Color color)
    {
        img.SetPixel(x, img.GetHeight() - 1 - y, color);
    }

    Image CreateImageFromCells()
    {
        int width = cells.GetLength(0);
        int height = cells.GetLength(1);

        img = Image.CreateEmpty(width, height, false, Image.Format.Rgb8);
       
        for (int i = 0; i < cells.GetLength(0); i++)
        {
            for (int j = 0; j < cells.GetLength(1); j++)
            {
                Color color;
                
                var h = Math.Abs(cells[i, j].height);
                var c = 1 - h;
                if (cells[i, j].height >= 0f)
                    color = new Color(Mathf.Lerp(0f, 0.4f, h),
                                            Mathf.Lerp(0.25f, 1f, h),
                                            Mathf.Lerp(0f, 0.4f, h));  //land

                else color = new Color(Mathf.Lerp(0f, 0.25f, c),
                                            Mathf.Lerp(0f, 0.25f, c),
                                            Mathf.Lerp(0.1f, 1f, c));  //water

                //The image is created flipped because the Image uses 0,0 at the topleft but 2d arrays use 0,0 as bottomleft
                SetPixelWorld(i, j, color);
            }
        }

        return img;
    }

    //this is slow. whatever.
    void FillVoronoiCells()
    {
        for (int i = 0; i < cells.GetLength(0); i++)
        {
            for (int j = 0; j < cells.GetLength(1); j++)
            {
                int iter = 0;
                
                float min = float.MaxValue;
                foreach (var c in voronoi.polygons)
                {
                    Color col = new Color((float)iter / voronoi.polygons.Count, (float)iter / voronoi.polygons.Count, (float)iter / voronoi.polygons.Count);
                    float dist = c.Value.center.DistanceTo(new Vector2(i + 0.5f,j + 0.5f));
                    if (dist < min)
                    {
                        min = dist;
                        SetPixelWorld(i, j, col);
                    }
                    iter++;
                }
            }
        }
    }


    void AssignSiteIDsOnCells()
    {
        for (int i = 0; i < cells.GetLength(0); i++)
        {
            for (int j = 0; j < cells.GetLength(1); j++)
            {
                float min = float.MaxValue;
                Plate2D closestPlate = plates[0];
                foreach(var p in plates)
                {
                    float dist = p.origin.DistanceTo(new Vector2(i + 0.5f, j + 0.5f));
                    if (dist < min)
                    {
                        min = dist;
                        closestPlate = p;
                    }
                }
                cells[i, j].owner = closestPlate;
                closestPlate.cells.Add(cells[i, j]);
            }
        }
    }

    void MovePlateTest()
    {
        
        var newcells = cells;
        int[,] counts = new int[cells.GetLength(0), cells.GetLength(1)];
        for (int i = 0; i < counts.GetLength(0); i++)
        {
            for (int j = 0; j < counts.GetLength(1); j++)
            {
                counts[i, j] = 0;
            }
        }

        foreach(var p in plates)
        {
            foreach(var c in p.cells)
            {
                var h = cells[(c.x + 4) % cells.GetLength(0),
                    (c.y + 8) % cells.GetLength(1)].height;


                newcells[c.x, c.y].height += h;
                newcells[c.x, c.y].SetColor();
                counts[c.x, c.y] += 1;
            }
        }

        for (int i = 0; i < cells.GetLength(0); i++)
        {
            for (int j = 0; j < cells.GetLength(1); j++)
            {
                if (counts[i,j] > 0)
                {
                    newcells[i, j].height /= counts[i, j];
                }
                else
                {
                    newcells[i,j].height = 0;
                }
                cells[i, j].height = newcells[i, j].height;
                cells[i, j].SetColor();

            }
        }
    }

    void RasterizeVoronoiEdges()
    {
        int c = 0;
        int m = voronoi.polygons.Count;
;       foreach (var s in voronoi.polygons)
        {
            float col = Mathf.Lerp(0, 1, (float)c/m);
            Color tect = new Color(col,col,col);
            for (int i = 0; i < s.Value.points.Count; i++)
            {
                var p1 = s.Value.points[i];
                var p2 = s.Value.points[0];
                if (i+1 < s.Value.points.Count)
                    p2 = s.Value.points[i+1];
                
                var num = Mathf.CeilToInt(p1.DistanceTo(p2));
                if (num < 0)
                    GD.Print("negative");
                for (int j = 0; j < num; j++)
                {
                    var p = p1.MoveToward(p2, j);
                    if ((int)p.X >= 0 && (int)p.X < img.GetWidth() &&
                        (int)p.Y >= 0 && (int)p.Y < img.GetHeight())
                        SetPixelWorld((int)p.X, (int)p.Y , Colors.HotPink);
                }
            }
            c++;
        }
    }


}
