using Godot;
using System;
using System.Collections.Generic;

public partial class IcoCell
{

    public List<int> neighbours; //indexes of bordering cells
    public int index;

    public Vector3 localPos;
    public int plate = -1; // -1 if the plate has not been assigned

    public bool isDirty = true;

    public IcoCell(int index, Vector3 localPos)
    {
        neighbours = new List<int>();
        this.index = index;
        this.localPos = localPos;
    }

    public void AddNeighbour(int adj)
    {
        if (!neighbours.Contains(adj))
            neighbours.Add(adj);
    }

}
