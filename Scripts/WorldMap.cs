using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Color = Godot.Color;
using Vector2 = Godot.Vector2;

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

	[Export] public Timer timer;

	[Export] public float PlatePointDensity = 1.75f;

    Cell2D[,] cells;
    List<Plate2D> plates;
    Image img;

	[ExportCategory("World Simulation")]
	[Export] public float Timescale = 0.1f; // how much is added to age per timestep;

	int timestep = 1;
	public event Action OnTimestepCompleted;
    //X and Y are image dimensions. Used for collision detecting between platepoints of differing plates
    public HashGrid hashgrid;

    public override void _Ready()
    {
		//Init
        plates = new List<Plate2D>();
        int ID = plates.Count;
        foreach (var s in voronoi.basePolygons)
        {
            
            Plate2D plate = new Plate2D(this, s.Key, ID);
            plates.Add(plate);
            ID++;
        }
        hashgrid = new HashGrid(worldWidth, worldHeight);
        GenerateCells(worldWidth, worldHeight);
		
		//Processing
		CreateMesh();
		
		
		GD.Randomize();
		foreach(var p in plates)
		{
			float rx = (float)GD.RandRange(-1f, 1f);
			float ry = (float)GD.RandRange(-1f, 1f);
			float speed = (float)GD.RandRange(0f, 1f);
			int d = GD.RandRange(1, 32);
			p.MovementDirection = new Vector2(rx,ry);
			p.MovementSpeed = 0.06f * speed;
			p.density = d;
		}
		hashgrid.InitializeBoundaries();
		timer.Timeout += Timestep;
		
		//timer.Start();
		//Timestep();
		//Lower density crust rises, higher density crust sinks
		//When a boundary platepoint has moved far enough without colliding, spawn a new platepoint behind it.

	}

	//Main Tectonic Plate Loop
	public void Timestep()
    {
		var start = Time.GetTicksUsec();
		//move all tect plates
		for (int i = 0; i < plates.Count; i++)
		{
			//plates[i].RotatePlate(plates[i].MovementSpeed);
			plates[i].MovePlate();
		}
		var end = Time.GetTicksUsec();
		var workertime = (end - start) / 100000f;
		GD.Print("Worker time for moveplate: ", workertime);

		start = Time.GetTicksUsec();
		hashgrid.UpdatePoints();
		end = Time.GetTicksUsec();
		workertime = (end - start) / 100000f;
		GD.Print("Worker time for updatepoints: ", workertime);


		//check for collisions
		start = Time.GetTicksUsec();
		hashgrid.Collide();
		end = Time.GetTicksUsec();
		workertime = (end - start) / 100000f;
		GD.Print("Worker time for collide: ", workertime);

		DisplayPlates();
		//DisplayHashgridCounts();

		//update platepoint heights?
		//todo: rebuild heightmap
		//redraw map
		RedrawMap();
		GD.Print("-----------");
		OnTimestepCompleted.Invoke();
	}


    void RedrawMap()
    {
		var texture = ImageTexture.CreateFromImage(img);
		mapDisplay.Texture = texture;
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
		AssignSiteIDs();
		RasterizeVoronoiEdges();
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
                int num = hashgrid.grid[i, j].points.Count();

				int d = 0;
				float h = 0;
				if (num >= 1)
                {
                    foreach(var n in hashgrid.grid[i,j].points)
                    {
						h++;
                        /*if (n.plate.density > d)
                        {
                            d = n.plate.density;
							h = n.height;
						}*/
                            
                    }
                }
				h = h / 4f;
				
                Color color = new Color(0.1f * h, 0.2f * h, 0.3f * h, 1f);
				if (h == 0)
				{
					color = Colors.DarkRed;
				}
				SetPixelWorld(i, j, color);
            }
        }
    }

	//TODO: right now it only looks at the first point in the list
	void DisplayHashgridPoints()
	{
		Color empty = new Color(0f, 0f, 0f);
		Color collision = new Color(1f, 1f, 0f);
		Color boundary = new Color(1f, 0f, 1f);
		Color both = new Color(1f, 1f, 1f);
		Color error = new Color(0f, 0f, 1f);

		for (int i = 0; i < hashgrid.grid.GetLength(0); i++)
		{
			for (int j = 0; j < hashgrid.grid.GetLength(1); j++)
			{
				if (hashgrid.grid[i, j].points.Count == 0)
					SetPixelWorld(i, j, empty);

				for (int p = 0; p < hashgrid.grid[i, j].points.Count; p++)
				{
					var point = hashgrid.grid[i, j].points[p];
					if (!hashgrid.grid[i, j].points[p].isActive)
					{ 
						//SetPixelWorld(i, j, new Color(0.05f * point.plate.ID, 0.05f * point.plate.ID, 0.05f * point.plate.ID)); 
					}
					else
					{
						
						if (point.isBoundary && point.isColliding)
							SetPixelWorld(i, j, Colors.Cyan);
						else if (point.isBoundary)
							SetPixelWorld(i, j, Colors.DarkSlateBlue);
						else if (point.isColliding)
							SetPixelWorld(i, j, Colors.Red);
						else SetPixelWorld(i, j, new Color(0.05f * point.plate.ID, 0.05f * point.plate.ID, 0.05f * point.plate.ID));
					}
				}
			}
		}
	}

	void DisplayPlates()
	{
		for (int i = 0; i < hashgrid.grid.GetLength(0); i++)
		{
			for (int j = 0; j < hashgrid.grid.GetLength(1); j++)
			{
				SetPixelWorld(i, j, Colors.Black);
			}
		}


		foreach (var plate in plates)
		{
			foreach(var p in plate.points)
			{
				Vector2 wp = p.WorldPos;
				//Vector2I pix = WorldToPixel(wp);

				Color c;

				/*if (p.isBoundary && p.isColliding)
					c = Colors.Cyan;
				else if (p.isBoundary && !p.isColliding)
					c = Colors.Blue;
				else if (!p.isBoundary && p.isColliding)
					c = Colors.Red;
				else
					c = new Color(
						0.8f - (0.02f * plate.density), 
						0.02f * plate.density,
						0.8f - (0.02f * plate.density));*/

				c = new Color(
						0.8f - (0.02f * plate.density),
						0.02f * plate.density,
						0.8f - (0.02f * plate.density));

				if (p.isActive)
					c *= Colors.Green;
				else c *= Colors.Red;

				int x = Mathf.FloorToInt(Mathf.PosMod(wp.X, worldWidth));
				int y = Mathf.FloorToInt(Mathf.PosMod(wp.Y, worldHeight));

				SetPixelWorld(x, y, c);
			}
		}
	}

	void AssignSiteIDs()
	{
		int width = worldWidth;
		int height = worldHeight;
		float spacing = 1f / PlatePointDensity;

		for (float x = 0; x < width; x += spacing)
		{
			for (float y = 0; y < height; y += spacing)
			{
				float min = float.MaxValue;
				Plate2D closestPlate = null;
				Vector2 cellPos = new Vector2(x, y);

				foreach (var p in plates)
				{
					//float px = p.origin.X % worldWidth;
					//float py = p.origin.Y % worldHeight;
					var px = Mathf.PosMod(p.origin.X, worldWidth);
					var py = Mathf.PosMod(p.origin.Y, worldHeight);
					float dist = WrappedDistance(cellPos, p.origin);
					if (dist <= min)
					{
						min = dist;
						closestPlate = p;
					}
				}

				//cells[(int)x, (int)y].plate = closestPlate;
				var pt = closestPlate.AddPointToPlate(new Vector2(x, y), 0f);
			}
		}
	}

	float WrappedDimension(float a, float b, float dimension)
	{
		float dx = Mathf.Abs(a - b);
		return Mathf.Min(dx, dimension - dx);
	}

	float WrappedDistance(Vector2 a, Vector2 b)
	{
		float dx = WrappedDimension(a.X, b.X, worldWidth);
		float dy = WrappedDimension(a.Y, b.Y, worldHeight);
		return dx * dx + dy * dy;
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
