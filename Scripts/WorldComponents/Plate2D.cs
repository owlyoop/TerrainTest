using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;



public class PlatePoint
{
	public Vector2 localPos;
	public Vector2 WorldPos => plate.LocalToWorld(localPos);    //The world position

	public Vector2 cachedWorldPos;

	public float height;
	public Plate2D plate;
	public Vector2I gridIndex; //Index for the hashgrid

	//public List<PlatePoint> neighbours;

	public bool isActive = false;

	public bool IsColliding { get; private set; }   //If the point is 'colliding' with another point on a different plate
	public bool IsBoundary { get; private set; }    //if egde of plate. if moves enough without colliding, spawn a new platepoint behind it.

	float distTravelAsBoundary = 0f;
	float distTravelNoCollision = 0f;
	Vector2 lastBoundaryPos; //The last position this point became a boundary.
							 //When the plate moves a certain dist away, a new platepoint is created

	public PlatePoint(Vector2 localPos, float height, Plate2D plate)
	{
		this.localPos = localPos;
		this.height = height;
		this.plate = plate;
		this.isActive = false;
		this.IsBoundary = false;
		this.IsColliding = false;
		//neighbours = new List<PlatePoint>();
	}

	public void OnEndTimestep()
	{

	}

	public void MarkPointAsBoundary(bool choice)
	{
		IsBoundary = choice;
		if (choice)
		{
			IsColliding = false;
			isActive = true;
		}

			
	}

	public void MarkPointAsColliding(bool choice)
	{
		IsColliding = choice;
		if (choice)
		{
			distTravelNoCollision = 0f;
			distTravelAsBoundary = 0f;
			IsBoundary = false;
			isActive = true;
		}
	}

	public void UpdateTravelStats()
	{
		float dist = plate.map.WrappedDistance(WorldPos, cachedWorldPos - (plate.MovementDirection.Normalized() * plate.MovementSpeed));
		if (IsBoundary)
			distTravelAsBoundary += dist;
		else distTravelAsBoundary = 0f;
		if (!IsColliding)
			distTravelNoCollision += dist;

		//spawn new platepoints
		//todo: sim eventually slows down to a halt. i think cause too many points spawn.
		//need to consolidate points if theres more than 2 of the same plate in a cell
		if (distTravelAsBoundary > 0.9f)
		{
			var newpt = WorldPos - (plate.MovementDirection.Normalized() * 0.9f);
			var p = plate.TryAddPointToPlate(newpt, (height *= 0.95f) - 0.01f, 4);
			if (p != null)
			{
				p.MarkPointAsBoundary(true);
				MarkPointAsBoundary(false);
				distTravelAsBoundary = 0f;
				plate.map.worldGrid.grid[gridIndex.X, gridIndex.Y].MarkAllPointsAsBoundary(false);
			}
		}
		
	}
}

public partial class Plate2D
{
	public WorldMap map;
    public Vector2 origin; //The origin of the plate created from voronoi polygons. not the actual center
	public Vector2 offset;
	public Vector2 center;
	public float rotation; //in degrees

	public List<PlatePoint> points; //the points that make up this plate, initially created from points on a grid that were inside the voronoi polygon
    public int density = 5; //crust density

    public Vector2 MovementDirection;
    public float MovementSpeed;

    public float angularVelocity;

    public int ID;

	public Plate2D PlateClone;  //the duplicated plate for the tiling world

	List<Plate2D> collidingPlates;	//other plates that are colliding with this one.

    public Plate2D(WorldMap map, Vector2 origin, int ID)
    {
		this.map = map;
        this.origin = origin;
		this.offset = Vector2.Zero;
		this.center = origin;
		points = new List<PlatePoint>();
        this.ID = ID;
    }

	Vector2 WorldToLocal(Vector2 worldPos)
	{
		float dx = worldPos.X - origin.X - offset.X;
		
		float halfW = map.worldWidth * 0.5f;
		if (dx > halfW) dx -= map.worldWidth;
		if (dx < -halfW) dx += map.worldWidth;


		float dy = worldPos.Y - origin.Y - offset.Y;

		halfW = map.worldHeight * 0.5f;
		if (dy > halfW) dy -= map.worldHeight;
		if (dy < -halfW) dy += map.worldHeight;
		return new Vector2(dx, dy).Rotated(-Mathf.DegToRad(rotation));
	}


	public Vector2 LocalToWorld(Vector2 local)
	{
		Vector2 rot = local.Rotated(Mathf.DegToRad(rotation));

		Vector2 world = origin + offset + rot;
		world.X = Mathf.PosMod(world.X, map.worldWidth);
		world.Y = Mathf.PosMod(world.Y, map.worldHeight);
		return world;
	}

	public void UpdatePointInHashGrid(PlatePoint p)
	{
		Vector2 oldWorld = new Vector2(p.cachedWorldPos.X, p.cachedWorldPos.Y);
		map.worldGrid.MovePoint(p);
		p.cachedWorldPos = oldWorld;
	}

	public PlatePoint AddPointToPlate(Vector2 worldPos, float height)
	{
		Vector2 local = WorldToLocal(worldPos);
		Vector2 oldWorld = new Vector2(worldPos.X, worldPos.Y);
		var p = new PlatePoint(local, height, this);
		map.worldGrid.AddPoint(p);
		points.Add(p);
		p.cachedWorldPos = oldWorld;
		return p;
	}

	/// <summary>
	/// Adds a platepoint to this plate if the number of platepoints in the worldgrid is under the threshold
	/// </summary>
	/// <param name="worldPos"></param>
	/// <param name="height"></param>
	/// <param name="threshold"></param>
	/// <returns></returns>
	public PlatePoint TryAddPointToPlate(Vector2 worldPos, float height, int threshold)
	{
		Vector2 local = WorldToLocal(worldPos);
		Vector2 oldWorld = new Vector2(worldPos.X, worldPos.Y);

		//TODO: dont add point to plate if >2 points already exist in the same cell

		var p = new PlatePoint(local, height, this);
		if (map.worldGrid.TryAddPoint(p, threshold))
		{
			points.Add(p);
			p.cachedWorldPos = oldWorld;
			return p;
		}
		else return null;
	}

	public void CheckForNewPoints()
	{
		for (int i = 0; i < points.Count; i++)
		{
			if (points[i].IsBoundary)
				points[i].UpdateTravelStats();
		}
	}

	public void AddExistingPointToPlate(PlatePoint point)
	{
		//need to find new localpos
		//var newpos = this.WorldToLocal(point.WorldPos);
		Vector2 world = point.WorldPos;
		point.plate.points.Remove(point);
		point.plate = this;
		point.localPos = WorldToLocal(world);
		points.Add(point);
	}

	public void MovePlate()
	{
		foreach (var p in points)
		{
			p.cachedWorldPos = new Vector2(p.WorldPos.X, p.WorldPos.Y);
		}
		offset += (MovementDirection * MovementSpeed);
		offset.X = offset.X % map.worldWidth;
		offset.Y = offset.Y % map.worldHeight;
		rotation += MovementSpeed * 0.1f;
		foreach (var p in points)
		{
			UpdatePointInHashGrid(p);

			var x = p.gridIndex.X;
			var y = p.gridIndex.Y;
			var cell = map.worldGrid.grid[x, y];

			//moving every platepoint is a bottleneck, esp if i want 1.71 platepoint density.
			//this isnt improving performance like i thought it would. oh well.
			//if (!cell.IsEmpty())
			//{
			//	UpdatePointInHashGrid(p);
			//}
		}
	}
}
