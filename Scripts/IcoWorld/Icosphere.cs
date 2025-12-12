using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

public partial class Icosphere : Node
{
    /*
    1² = a² + c²                          // Pythagoras' Theorem
    1 = a² + (a* goldenRatio)²           // c = a * goldenRatio, 1² = 1
    1 = a² + a² * goldenRation²           // Exponent distribution (power rule)
    1 = a² * (1 + goldenRation²)          // "a" is a common factor
    a² = 1 / (1 + goldenRation²)          // Divide both sides by a² and flip
    a = √(1 / (1 + goldenRatio²))         // Apply square root on both sides
    a = √(1 / (1 + 2.618033988749895))    // Replace golden ratio value
    a = 0.525731112119134                 // Solve the square root

    c = a * goldenRatio
    c = 0.525731112119134 * 1.618033988749  // Replace values
    c = 0.85065080835157
    */

    private static readonly Vector3[] IcosphereVertices =
    {
        new(-0.8506508f,           0f,         0.5257311f),     //v0
        new(0.8506508f,           0f,         0.5257311f),      //v1
        new(0.8506508f,           0f,         -0.5257311f),     //v2
        new(-0.8506508f,           0f,         -0.5257311f),    //v3

        new(0f,           0.5257311f,         0.85065067f),     //v4
        new(0f,           0.5257311f,         -0.85065067f),    //v5
        new(0f,           -0.5257311f,         -0.85065067f),   //v6
        new(0f,           -0.5257311f,         0.85065067f),    //v7

        new(-0.5257311f,           0.8506508f,         0f),     //v8
        new(0.5257311f,           0.8506508f,         0f),      //v9
        new(0.5257311f,           -0.8506508f,         0f),     //v10
        new(-0.5257311f,           -0.8506508f,         0f),    //v11
	};

    private static readonly int[] IcosphereIndices =
    {
        0, 8, 4,
        0, 4, 7,
        0, 3, 8,
        0, 11, 3,
        0, 7, 11,
        1, 4, 9,
        1, 7, 4,
        1, 9, 2,
        1, 10, 7,
        2, 9, 5,
        2, 5, 6,
        2, 6, 10,
        2, 10, 1,
        3, 6, 5,
        3, 11, 6,
        3, 5, 8,
        4, 8, 9,
        5, 9, 8,
        6, 11, 10,
        7, 10, 11
    };

    private struct TriangleIndices
    {
        public int v1;
        public int v2;
        public int v3;

        public TriangleIndices(int v1, int v2, int v3)
        {
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
        }
    }

    [Export]
    public float scale = 1f;
    [Export(PropertyHint.Range, "0,16,")]
    public int subdivisions = 4;

    [Export(PropertyHint.Range, "1,8")]
    public int NumStartingPlates = 4;

    [Export]
    public StandardMaterial3D mat;

    private int index;
    Dictionary<(int, int), int> midpointCache = new Dictionary<(int, int), int>();

    private List<Vector3> points = new List<Vector3>();
    private List<TriangleIndices> faces = new List<TriangleIndices>();
    private List<int> indices = new List<int>();

    private Dictionary<int, IcoCell> cells = new Dictionary<int, IcoCell>();
    Color[] plateColors = { Colors.Red, Colors.Yellow, Colors.Green, Colors.DeepSkyBlue, Colors.SlateBlue, Colors.Orange, Colors.Blue, Colors.SeaGreen };

    int GetMiddlePoint(int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        if (midpointCache.TryGetValue(key, out int cachedIndex))
        {
            return cachedIndex;
        }

        var midpoint = points[a].Slerp(points[b], 0.5f);
        int i = AddVertex(midpoint);

        midpointCache[key] = i;
        return i;
    }

    int AddVertex(Vector3 p)
    {
        points.Add(p);
        return points.Count - 1;
    }

    void Subdivide()
    {
        for (int i = 0; i < IcosphereVertices.Length; i++)
        {
            points.Add(IcosphereVertices[i]);
        }

        for (int i = 0; i < IcosphereIndices.Length; i = i + 3)
        {
            faces.Add(new TriangleIndices(  IcosphereIndices[i], 
                                            IcosphereIndices[i + 1], 
                                            IcosphereIndices[i + 2]));

        }


        for (int i = 1; i <= subdivisions; i++)
        {
            var faces2 = new List<TriangleIndices>();
            foreach (var tri in faces)
            {
                // replace triangle by 4 triangles
                int v4 = GetMiddlePoint(tri.v1, tri.v2);
                int v5 = GetMiddlePoint(tri.v2, tri.v3);
                int v6 = GetMiddlePoint(tri.v3, tri.v1);

                faces2.Add(new TriangleIndices(tri.v1, v4, v6));
                faces2.Add(new TriangleIndices(tri.v2, v5, v4));
                faces2.Add(new TriangleIndices(tri.v3, v6, v5));
                faces2.Add(new TriangleIndices(v4, v5, v6));
            }
            faces = faces2;
        }

        foreach (var tri in faces)
        {
            indices.Add(tri.v1);
            indices.Add(tri.v2);
            indices.Add(tri.v3);
        }

        WriteNeighborData();
        BuildMesh();
    }

    void WriteNeighborData()
    {
        Dictionary<int, List<int>> neighbours = new Dictionary<int, List<int>>();

        for (int i = 0; i < indices.Count; i = i + 3)
        {
            Vector3 pos = GetVec3FromIdx(i);
            if (cells.TryGetValue(indices[i], out IcoCell cell0))
            {
                cell0.AddNeighbour(indices[i + 1]);
                cell0.AddNeighbour(indices[i + 2]);
            }
            else
            {
                IcoCell c = new IcoCell(indices[i], pos);
                c.AddNeighbour(indices[i + 1]);
                c.AddNeighbour(indices[i + 2]);
                cells[indices[i]] = c;
            }

            //i + 1
            if (cells.TryGetValue(indices[i + 1], out IcoCell cell1))
            {
                cell1.AddNeighbour(indices[i]);
                cell1.AddNeighbour(indices[i + 2]);
            }
            else
            {
                IcoCell c = new IcoCell(indices[i + 1], pos);
                c.AddNeighbour(indices[i]);
                c.AddNeighbour(indices[i + 2]);
                cells[indices[i + 1]] = c;
            }

            //i + 2
            if (cells.TryGetValue(indices[i + 2], out IcoCell cell2))
            {
                cell2.AddNeighbour(indices[i]);
                cell2.AddNeighbour(indices[i + 1]);
            }
            else
            {
                IcoCell c = new IcoCell(indices[i + 2], pos);
                c.AddNeighbour(indices[i]);
                c.AddNeighbour(indices[i + 1]);
                cells[indices[i + 2]] = c;
            }

        }

        GD.Print("cells count: ", cells.Count);

        /*foreach(var c in cells)
        {
            var strlist = "";
            foreach (var i in c.Value.neighbours)
            {
                strlist += i.ToString() + ", ";
            }
            GD.Print(c.Key, ": ", strlist);
        }*/
    }

    void BuildMesh()
    {
        AssignPlatesToIcocells();

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetColor(Colors.Black);


        int iter = 0;
        float max = (float)points.Count;
        foreach (var p in points)
        {
            float u = (Mathf.Atan2(p.Z, p.X) / (2f * Mathf.Pi));
            float v = (Mathf.Asin(p.Y) / Mathf.Pi) + 0.5f;

            if (cells[iter].plate != -1)
                st.SetColor(plateColors[cells[iter].plate]);
            else
                st.SetColor(new Color(iter / max, iter / max, iter / max));

            st.SetUV(new Vector2(u, v));
            st.AddVertex(p);
            iter++;
            
        }
        foreach (var i in indices)
        {
            st.AddIndex(i);
        }



        var mesh = st.Commit();
        var m = new MeshInstance3D();
        m.Mesh = mesh;
        m.MaterialOverride = mat;
        this.AddChild(m);
    }

    public Vector3 GetVec3FromIdx(int index)
    {
        return points[indices[index]];
    }

    public override void _Ready()
    {
        
        Subdivide();

        GD.Print("points count: ", points.Count);
        GD.Print("indices count: ", indices.Count);
        GD.Print("faces count: ", faces.Count);

    }

    public override void _Process(double delta)
    {
        for (int i = 0; i < points.Count; i++)
        {
            //DebugDraw3D.DrawText(points[i], i.ToString(), 12, Colors.BlueViolet);
        }
    }

    public void AssignPlatesToIcocells()
    {
        //cells and points index matches

        //icocells contain list of adj indexes

        //assign 1 random cell for each starting plates
        Random rand = new Random();
        Queue<int> queue = new Queue<int>();
        
        for (int i = 0; i < NumStartingPlates; i++)
        {
            bool added = false;
            while (!added)
            {
                int pix = rand.Next(points.Count - 1);
                if (!queue.Contains(pix))
                {
                    queue.Enqueue(pix);
                    added = true;
                    GD.Print("Plate ", i, " added to index: ", pix, ", pos is: ", cells[pix].localPos);
                    cells[pix].plate = i;
                    cells[pix].isDirty = false;
                }
            }
        }

        //expand out from the starting cells
        while (queue.Count > 0)
        {
            int curr = queue.Dequeue();
            
            foreach(var n in cells[curr].neighbours)
            {
                if (cells[n].isDirty)
                {
                    cells[n].plate = cells[curr].plate;
                    cells[n].isDirty = false;
                    queue.Enqueue(n);
                }
            }
        }

    }
}
