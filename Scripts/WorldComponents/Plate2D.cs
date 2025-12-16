using Godot;
using System;
using System.Collections.Generic;



public class PlatePoint
{
	public Vector2 position; //The world position
	public Vector2 localPos;
	public float height;
	public Plate plate;

	public PlatePoint(Vector2 pos, float height)
	{
		this.position = pos;
		this.height = height;
	}

	public PlatePoint(Vector2 pos)
	{
		this.position = pos;
		this.height = 0f;
	}

	public void UpdatePosition(Vector2 newPos)
	{
		this.position = newPos;
	}
}

public partial class Plate2D : Node
{
    public Vector2 origin; //The origin of the plate created from voronoi polygons. not the actual center
	public Vector2 position;
	public Vector2 center;

	public List<PlatePoint> points; //the points that make up this plate, initially created from points on a grid that were inside the voronoi polygon
    public int density = 5;	//crust density

    public Vector2 velocityDirection;
    public float velocity;

    public float angularVelocity;

    public int ID;

	public Plate2D PlateClone;	//the duplicated plate for the tiling world

    public Plate2D(Vector2 origin, int ID)
    {
        this.origin = origin;
		this.position = Vector2.Zero;
		points = new List<PlatePoint>();
        this.ID = ID;
    }

	public void AddPointToPlate(Vector2 pos, float height)
	{
		var p = new PlatePoint(pos, height);
		p.localPos = pos - origin;
		points.Add(p);

	}
    public void SetVelocity(Vector2 velocity)
    {

    }

	public void UpdateCenter()
	{

	}

	public void MovePlate(Vector2 direction)
	{
		foreach(var p in points)
		{
			p.position += direction;
		}
		this.position += direction;
	}

	public void RotatePlate(float degrees)
	{
		/* func rotated_point(_center, _angle, _distance):
    	return _center + Vector2(sin(_angle),cos(_angle)) * _distance*/
		foreach(var p in points)
		{
			var diff = p.position - this.origin;
			var rotated = diff.Rotated(Mathf.DegToRad(degrees)) + this.origin;
			p.position = rotated;

		}
	}
}
