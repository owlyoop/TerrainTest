using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Plate2D
{
	public WorldMap map;
    public Vector2 origin; //The origin of the plate created from voronoi polygons. not the actual center
	public Vector2 offset;
	public Vector2 center;
	public float rotation; //in degrees

	public List<PlatePoint> points; //the points that make up this plate, initially created from points on a grid that were inside the voronoi polygon
    public int density = 5; //crust density

    public Vector2 Velocity { get; private set; }  //derived from all points

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

	public Vector2 WorldToLocal(Vector2 worldPos)
	{
		//todo: this is ugly. shameful.
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
		offset += (Velocity);
		offset.X = offset.X % map.worldWidth;
		offset.Y = offset.Y % map.worldHeight;
		rotation += 0.0f;	//TODO: angular velocity
		
		foreach (var p in points)
		{
			UpdatePointInHashGrid(p);
		}
	}

	//Initializes all of the platepoints velocity
	public void InitializePlateVelocity(Vector2 velocity)
	{
		foreach(var p in points)
		{
			p.Velocity = velocity;
		}
		Velocity = new Vector2(0, 0);
		float count = 0f;
		foreach (var p in points)
		{
			Velocity += p.Velocity;
			count = count + 1f;
		}
		Velocity /= count;
	}

	/// <summary>
	/// Updates the velocity of this plate, which is an average of all the platepoint's velocities
	/// </summary>
	public void UpdateVelocity()
	{
		Velocity = new Vector2(0, 0);
		float count = 0f;
		foreach (var p in points)
		{
			if (p.isActive)
			{
				Velocity += p.Velocity;
				count = count + 1f;
			}
		}
		Velocity /= count;
	}
}
