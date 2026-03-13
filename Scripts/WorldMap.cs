using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using static Godot.Projection;
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
	[Export] public MapViewer mapViewer;
	[Export] public VoronoiWorld voronoi;

	[Export] public Timer timer;

	[Export] public float PlatePointDensity = 1.75f;

    public List<Plate2D> Plates;

	[ExportCategory("World Simulation")]
	[Export] public float Timescale = 0.1f; // how much is added to age per timestep;

	int timestep = 1;
	public event Action OnTimestepCompleted;
    //X and Y are image dimensions. Used for collision detecting between platepoints of differing plates
    public WorldGrid worldGrid;

	public override void _Ready()
    {
		//Init
        Plates = new List<Plate2D>();
        int ID = Plates.Count;
        foreach (var s in voronoi.basePolygons)
        {
            
            Plate2D plate = new Plate2D(this, s.Key, ID);
            Plates.Add(plate);
            ID++;
        }
        worldGrid = new WorldGrid(worldWidth, worldHeight, this);
		

		mapViewer.Initialize(worldWidth, worldHeight);
		AssignSiteIDs();
		
		
		GD.Randomize();
		foreach(var p in Plates)
		{
			float rx = (float)GD.RandRange(-1f, 1f);
			float ry = (float)GD.RandRange(-1f, 1f);
			float speed = (float)GD.RandRange(0f, 1f);
			p.InitializePlateVelocity(new Vector2(rx, ry) * (speed * 0.05f));
			p.InitializeCenter();
		}
		worldGrid.InitializeBoundaries();
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
		for (int i = 0; i < Plates.Count; i++)
		{
			Plates[i].MovePlate();
		}
		var end = Time.GetTicksUsec();
		var workertime = (end - start) / 1000f;
		GD.Print("Work time for moveplate: ", workertime); 

		start = Time.GetTicksUsec();
		//worldGrid.UpdatePointCategories();
		worldGrid.UpdatePointCategoriesParallel();
		end = Time.GetTicksUsec();
		workertime = (end - start) / 1000f;
		GD.Print("Work time for updatepoints: ", workertime);


		//check for collisions
		start = Time.GetTicksUsec();
		worldGrid.CollideWithForces();
		end = Time.GetTicksUsec();
		workertime = (end - start) / 1000f;
		GD.Print("Work time for collide: ", workertime);



		for (int i = 0; i < Plates.Count; i++)
		{
			Plates[i].CheckForNewPoints();
			Plates[i].UpdateVelocity();
		}


		mapViewer.DisplayMap();

		GD.Print("-----------");
		OnTimestepCompleted.Invoke();
	}


    public Plate2D GetPlateByIndex(int index)
    {
		index = index % Plates.Count;
        return Plates[index];
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

				foreach (var p in Plates)
				{
					var px = Mathf.PosMod(p.origin.X, worldWidth);
					var py = Mathf.PosMod(p.origin.Y, worldHeight);
					float dist = WrappedDistance(cellPos, p.origin);
					if (dist <= min)
					{
						min = dist;
						closestPlate = p;
					}
				}

				float a = noiseGen.GetNoise2D(cellPos.X, cellPos.Y);
				float b = noiseGen.GetNoise2D((cellPos.X * 0.5f) + 100f, (cellPos.Y * 0.5f) + 100f);

				float fw = Mathf.Clamp(b * 3f, 0f, 1f);
				float mw = 1f - fw;
				float mag = Mathf.Abs(a);

				//float felsic = Mathf.Clamp(a, 0.0001f, 1f);
				//float mafic = Mathf.Abs(Mathf.Clamp(a, -1f, 0.0001f));
				//felsic *= 5000f;
				//mafic *= 5000f;

				float felsic = fw * mag * 10000f;
				float mafic = mw * mag * 10000f;


				var pt = closestPlate.AddPointToPlate(new Vector2(x, y), felsic, mafic);
				pt.age = MathF.Abs(mw) * 100f;
			}
		}
	}

	float WrappedDimension(float a, float b, float dimension)
	{
		float dx = Mathf.Abs(a - b);
		return Mathf.Min(dx, dimension - dx);
	}

	public float WrappedDistance(Vector2 a, Vector2 b)
	{
		float dx = WrappedDimension(a.X, b.X, worldWidth);
		float dy = WrappedDimension(a.Y, b.Y, worldHeight);
		//return dx * dx + dy * dy;
		return Mathf.Sqrt(dx * dx + dy * dy);
	}


}
