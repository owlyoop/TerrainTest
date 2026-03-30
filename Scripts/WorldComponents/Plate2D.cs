using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

public partial class Plate2D
{
	public WorldMap map;
    public Vector2 origin; //The origin of the plate created from voronoi polygons. not the actual center
	public Vector2 offset;
	public Vector2 Center;
	private Vector2 _localCenterSum;
	public float rotation; //in degrees

	public List<PlatePoint> points; //the points that make up this plate, initially created from points on a grid that were inside the voronoi polygon
    

    public Vector2 Velocity { get; private set; }  //derived from all points

	public Vector2 sumForce;
	public float sumTorque;

	public float angularVelocity;
	public float totalMass;

    public int ID;
	

	public Plate2D(WorldMap map, Vector2 origin, int ID)
    {
		this.map = map;
        this.origin = origin;
		this.offset = Vector2.Zero;
		this.Center = origin;
		points = new List<PlatePoint>();
        this.ID = ID;
    }


	public Vector2 WorldToLocal(Vector2 worldPos)
	{
		//todo: this is ugly. shameful.
		/*float dx = worldPos.X - origin.X - offset.X;
		
		float halfW = map.worldWidth * 0.5f;
		if (dx > halfW) dx -= map.worldWidth;
		if (dx < -halfW) dx += map.worldWidth;


		float dy = worldPos.Y - origin.Y - offset.Y;

		halfW = map.worldHeight * 0.5f;
		if (dy > halfW) dy -= map.worldHeight;
		if (dy < -halfW) dy += map.worldHeight;
		return new Vector2(dx, dy).Rotated(-Mathf.DegToRad(rotation));*/

		float x = origin.X + offset.X;
		float y = origin.Y + offset.Y;

		float hw = map.worldWidth * 0.5f;
		float hh = map.worldHeight * 0.5f;

		float dx = Mathf.PosMod((worldPos.X - x) + hw, map.worldWidth) - hw;
		float dy = Mathf.PosMod((worldPos.Y - y) + hh, map.worldHeight) - hh;

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

	public PlatePoint AddPointToPlate(Vector2 worldPos, float felsic, float mafic)
	{
		Vector2 local = WorldToLocal(worldPos);
		Vector2 oldWorld = new Vector2(worldPos.X, worldPos.Y);
		var p = new PlatePoint(local, felsic, mafic, this);
		map.worldGrid.AddPoint(p);
		points.Add(p);
		p.prevWorldPos = oldWorld;

		_localCenterSum += p.localPos;


		return p;
	}

	public void AddExistingPointToPlate(PlatePoint point)
	{
		//need to find new localpos
		//var newpos = this.WorldToLocal(point.WorldPos);
		Vector2 world = point.WorldPos;
		point.plate.RemovePoint(point);
		point.plate = this;
		point.localPos = WorldToLocal(world);
		points.Add(point);
		_localCenterSum += point.localPos;
	}

	public void RemovePoint(PlatePoint point)
	{
		if (!points.Remove(point))
			return;

		_localCenterSum -= point.localPos;
	}

	public void RemovePoint(int idx)
	{
		var point = points[idx];
		points.RemoveAt(idx);

		_localCenterSum -= point.localPos;
	}

	//this isnt working how id expect, the plate centers slowly get more messed up and drift away faster
	void UpdateCenter()
	{
		if (points.Count == 0) return;
		Center = LocalToWorld(_localCenterSum / points.Count);

	}

	//todo: only run this like once every 5 timesteps or something
	public void UpdateCenterSlow()
	{
		Center = Vector2.Zero;
		foreach(var p in points)
		{
			Center += p.localPos;
		}
		Center = Center / points.Count;
		Center = LocalToWorld(Center);
	}

	
	public void CheckForNewPoints()
	{
		for (int i = points.Count - 1; i >= 0; i--)
		{
			var point = points[i];
			bool valid = point.OnTimestep();
			//if (!valid) continue;
			//if (point.IsEdgeBoundary || point.IsBoundary )
				point.UpdateTravelStats(0.1f, "area");
		}
	}



	public void MovePlate()
	{
		foreach (var p in points)
		{
			p.prevWorldPos = p.WorldPos;
			p.SetWorldPosDirty();
		}

		offset += Velocity;
		offset.X = offset.X % map.worldWidth;
		offset.Y = offset.Y % map.worldHeight;

		Center += Velocity;
		Center.X = Center.X % map.worldWidth;
		Center.Y = Center.Y % map.worldHeight;
		rotation += angularVelocity;

		for (int i = points.Count - 1; i >= 0; i--)
		{
			if (i <= points.Count - 1)
			{
				var p = points[i];
				Vector2 newWorldPos = p.WorldPos;
				map.worldGrid.MovePoint(p, newWorldPos);
			}
				
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

	public void UpdateVelocity()
	{
		//a = sumforce / totalmass
		//alpha = sumtorq / inertia
		//velcity += a
		//angvel += alpha
		if (points.Count == 0) return;

		//todo i think world wrapping fucks this up

		totalMass = 0f;
		var inertia = 0f;
		foreach (var p in points)
		{
			Vector2 r = p.WorldPos - Center;
			totalMass += p.mass;
			inertia += p.mass * (r.X * r.X + r.Y * r.Y);
		}

		var a = sumForce / totalMass;
		var alpha = sumTorque / inertia;
		Velocity += a;
		angularVelocity += alpha;

		sumForce = Vector2.Zero;
		sumTorque = 0f;

	}
	
	public void InitializeCenter()
	{
		_localCenterSum = Vector2.Zero;

		foreach (var p in points)
			_localCenterSum += p.localPos;
		UpdateCenter();
	}
}
