using Godot;
using System;
using System.Collections.Generic;

//Represents the sites/centers of polygons formed by voronoi points.
public partial class VoronoiPolygon : Node
{
    public Vector2 center;
    public List<Vector2> points;
    public List<Vector2> neighbours; //the neighbour's center points
    public int ID;


    public VoronoiPolygon(Vector2 center, List<Vector2> points)
    {
        this.center = center;
        this.points = points;
        neighbours = new List<Vector2>();
    }


    public VoronoiPolygon() { }
}
