using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

    //X and Y are image dimensions. Used for collision detecting between platepoints of differing plates
    public HashGrid hashgrid;

    public override void _Ready()
    {
        plates = new List<Plate2D>();
        int ID = plates.Count;
        foreach (var s in voronoi.polygons)
        {
            
            Plate2D plate = new Plate2D(this, s.Key, ID);
            plates.Add(plate);
            ID++;
        }
        hashgrid = new HashGrid(worldWidth, worldHeight);
        GenerateCells(worldWidth, worldHeight);

		CreateMesh();

		DisplayHashgridCounts();
		var texture = ImageTexture.CreateFromImage(img);
		mapDisplay.Texture = texture;
	}

    //Main Tectonic Plate Loop
    public void Timestep()
    {
        //move all tect plates
		for (int i = 0; i < plates.Count; i++)
		{
			plates[i].RotatePlate(i * 6f);
		}


        //check for collisions

        //

        //redraw map
        RedrawMap();
	}


    void RedrawMap()
    {

    }

    void AddTectonicPlate(Plate2D plate)
    {
        plates.Add(plate);
    }

    public Plate2D GetPlateByIndex(int index)
    {
        return plates[index];
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
        //img = InitializeImage();
        AssignSiteIDsOnCells();
        //FillVoronoiCells();
        RasterizeVoronoiEdges();
        //DisplayHashgridCounts();
        var texture = ImageTexture.CreateFromImage(img);
        mapDisplay.Texture = texture;
    }

    Image InitializeImage()
    {
        var img = Image.CreateEmpty(worldWidth, worldHeight, false, Image.Format.Rgb8);
        return img;
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

    void DisplayHashgridCounts()
    {
        for (int i = 0; i < hashgrid.grid.GetLength(0); i++)
        {
            for (int j = 0; j < hashgrid.grid.GetLength(1); j++)
            {
                int num = hashgrid.grid[i, j].Count();

				int d = 0;
				float h = 0;
				if (num >= 1)
                {
                    foreach(var n in hashgrid.grid[i,j])
                    {
                        
                        if (n.plate.density > d)
                        {
                            d = n.plate.density;
							h = n.height;
						}
                            
                    }
                }
                Color color = new Color(0.1f * h, 0.2f * h, 0.3f * h, 1f);
                SetPixelWorld(i, j, color);
            }
        }
    }

    //TODO: it isnt needed to assign plate stuff to image cells so move the needed functionality out of this func
    void AssignSiteIDsOnCells()
    {
        for (int i = 0; i < cells.GetLength(0); i++)
        {
            for (int j = 0; j < cells.GetLength(1); j++)
            {
                float min = float.MaxValue;
                Plate2D closestPlate = plates[0];
                foreach (var p in plates)
                {
                    float dist = p.origin.DistanceTo(new Vector2(i + 0.5f, j + 0.5f));
                    if (dist < min)
                    {
                        min = dist;
                        closestPlate = p;
                    }
                }

				var height = cells[i, j].height;
				var x = cells[i, j].x;
				var y = cells[i, j].y;
				//make the points that should loop over instead not, making the plate continous. later we will duplicate the plates.
				if (closestPlate.origin.X < 0)
				{
					closestPlate = plates[closestPlate.ID - 1];
					x = x + img.GetSize().X;
				}
				else if (closestPlate.origin.X >= img.GetSize().X)
				{
					closestPlate = plates[closestPlate.ID - 2];
					x = x - img.GetSize().X;
				}

				cells[i, j].plate = closestPlate;
				//cells[i, j].localPos = new Vector2I((int)closestPlate.origin.X + (int)cells[i, j].x, 
				//									(int)closestPlate.origin.Y + (int)cells[i, j].y);

				var pt = closestPlate.AddPointToPlate(new Vector2(i, j), height);
                hashgrid.AddPoint(pt);
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
                if (i + 1 < s.Value.points.Count)
                    p2 = s.Value.points[i + 1];

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
