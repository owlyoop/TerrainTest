using Godot;
using System;
using System.Collections.Generic;

public partial class Plate2D : Node
{
    public Vector2 origin; //The origin of the plate created from voronoi polygons. not the actual center
	public List<Vector2> points; //the points that make up this plate, initially created from points on a grid that were inside the voronoi polygon
    public int density = 5;	//crust density

    public Vector2 velocityDirection;
    public float velocity;

    public float angularVelocity;

    public int ID;

    public Plate2D(Vector2 origin, int ID)
    {
        this.origin = origin;
		points = new List<Vector2>();
        this.ID = ID;
    }

	public void AddPointToPlate()
	{

	}
    public void SetVelocity(Vector2 velocity)
    {

    }
}
