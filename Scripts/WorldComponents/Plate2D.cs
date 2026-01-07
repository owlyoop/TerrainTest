using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;



public class PlatePoint
{
	public Vector2 localPos;
	public Vector2 worldPos;    //The world position
	public float height;
	public Plate2D plate;
	public Vector2I gridIndex; //Index for the hashgrid

	public List<PlatePoint> neighbours;

	public bool isColliding = false;    //If the point is 'colliding' with another point on a different plate
	public bool isBoundary = false;		//if egde of plate. if moves enough without colliding, spawn a new platepoint behind it.

	float age;
	float crustThickness;
	float distNoCollision;	//distance travelled without a collision.

	public PlatePoint(Vector2 localPos, float height, Plate2D plate)
	{
		this.localPos = localPos;
		this.height = height;
		this.plate = plate;
		neighbours = new List<PlatePoint>();
	}
}

public partial class Plate2D
{
	public WorldMap map;
    public Vector2 origin; //The origin of the plate created from voronoi polygons. not the actual center
	public Vector2 position;
	public Vector2 center;
	public float rotation; //in degrees

	public List<PlatePoint> points; //the points that make up this plate, initially created from points on a grid that were inside the voronoi polygon
    public int density = 5;	//crust density

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
		this.position = Vector2.Zero;
		points = new List<PlatePoint>();
        this.ID = ID;
    }

	Vector2 WorldToLocal(Vector2 worldPos)
	{
		float dx = worldPos.X - origin.X;

		// wrap X into [-width/2, width/2]
		float halfW = map.worldWidth * 0.5f;
		if (dx > halfW) dx -= map.worldWidth;
		if (dx < -halfW) dx += map.worldWidth;

		float dy = worldPos.Y - origin.Y;

		return new Vector2(dx, dy);
	}

	void UpdatePointWorldPosition(PlatePoint p)
	{
		Vector2 world = origin + position + p.localPos.Rotated(Mathf.DegToRad(rotation));

		world.X = Mathf.PosMod(world.X, map.worldWidth);

		map.hashgrid.MovePoint(p, world);
	}

	public PlatePoint AddPointToPlate(Vector2 worldPos, float height)
	{
		Vector2 local = WorldToLocal(worldPos);
		var p = new PlatePoint(local, height, this);
		points.Add(p);
		UpdatePointWorldPosition(p);
		return p;

	}

	public void MovePlate()
	{
		position += (MovementDirection * MovementSpeed);
		//position.X = Mathf.PosMod(position.X, map.worldWidth);


		foreach (var p in points)
		{
			UpdatePointWorldPosition(p);
		}
	}

	public void RotatePlate(float degrees)
	{
		/* func rotated_point(_center, _angle, _distance):
    	return _center + Vector2(sin(_angle),cos(_angle)) * _distance*/
		rotation += degrees;
		foreach(var p in points)
		{
			//var diff = p.worldPos - this.origin;
			//var rotated = diff.Rotated(Mathf.DegToRad(degrees)) + this.origin;
			UpdatePointWorldPosition(p);

		}
	}
}
