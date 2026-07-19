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
	public Dictionary<PlatePoint, int> pointIndices;

	public Vector2 Velocity { get; private set; }  //derived from all points

	public Vector2 sumForce;
	public int numForcePts = 0;
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
		pointIndices = new Dictionary<PlatePoint, int>();
        this.ID = ID;
    }


	public Vector2 WorldToLocal(Vector2 worldPos)
	{
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

	public void AddDirect(PlatePoint point)
	{
		pointIndices[point] = points.Count;
		points.Add(point);
		_localCenterSum += point.localPos;
	}

	public PlatePoint AddPointToPlate(Vector2 worldPos, float felsic, float mafic)
	{
		Vector2 local = WorldToLocal(worldPos);
		Vector2 oldWorld = new Vector2(worldPos.X, worldPos.Y);
		var p = new PlatePoint(local, felsic, mafic, this);
		map.worldGrid.AddPoint(p);

		AddDirect(p);
		p.prevWorldPos = oldWorld;


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
		AddDirect(point);
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
		int lastIdx = points.Count - 1;
		var lastPoint = points[lastIdx];

		points[idx] = lastPoint;
		pointIndices[lastPoint] = idx;

		points.RemoveAt(idx);
		pointIndices.Remove(point);

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

	public void UpdateCenterOfMass()
	{
		Vector2 weightedSum = Vector2.Zero;
		float total = 0f;

		foreach (var p in points)
		{
			weightedSum += p.localPos * p.mass;
			total += p.mass;
		}

		Center = LocalToWorld(weightedSum / total);
	}

	
	public void CheckForNewPoints()
	{
		for (int i = points.Count - 1; i >= 0; i--)
		{
			var point = points[i];
			bool valid = point.OnTimestep();
			//if (!valid) continue;
			if (point.IsEdgeBoundary || point.IsBoundary )
				point.UpdateTravelStats(1f, "area");
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

		var pointsArr = points.ToArray();

		for (int i = 0; i < pointsArr.Length; i++)
		{
			var p = pointsArr[i];
			if (!pointIndices.ContainsKey(p)) continue;

			Vector2 newWorldPos = p.WorldPos;
			map.worldGrid.MovePoint(p, newWorldPos);
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
			if (!float.IsFinite(p.mass) || p.mass <= 0.001f) continue;

			Vector2 r = p.WorldPos - Center;
			totalMass += p.mass;
			inertia += p.mass * (r.X * r.X + r.Y * r.Y);
		}
		if (totalMass <= 0f) totalMass = 1f;
		if (inertia <= 0f) inertia = 1f;

		var a = sumForce / numForcePts;
		a /= totalMass;
		var alpha = sumTorque / inertia;

		if (!float.IsFinite(a.X) || !float.IsFinite(a.Y) || !float.IsFinite(alpha))
		{
			sumForce = Vector2.Zero;
			sumTorque = 0f;
			return;
		}
		Velocity += a * 0.96f;
		Velocity = Velocity.LimitLength(1f);
		angularVelocity += alpha * 0.94f;
		angularVelocity = Mathf.Clamp(angularVelocity, -0.2f, 0.2f);

		sumForce = Vector2.Zero;
		sumTorque = 0f;
		numForcePts = 0;
	}
	
	public void InitializeCenter()
	{
		_localCenterSum = Vector2.Zero;

		foreach (var p in points)
			_localCenterSum += p.localPos;
		UpdateCenterOfMass();
	}

}
