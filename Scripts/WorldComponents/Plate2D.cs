using Godot;
using System;
using System.Collections.Generic;

public partial class Plate2D : Node
{
    public Vector2 origin;
    public List<Cell2D> cells;
    public int density = 5;	//crust density

    public Vector2 velocityDirection;
    public float velocity;

    public float angularVelocity;

    public int ID;

    public Plate2D(Vector2 origin, int ID)
    {
        this.origin = origin;
        cells = new List<Cell2D>();
        this.ID = ID;
    }


    public void SetVelocity(Vector2 velocity)
    {

    }
}
