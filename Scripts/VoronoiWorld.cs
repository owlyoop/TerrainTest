using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

public partial class VoronoiWorld : Node
{
	[Export] public WorldMap map;
	[Export] int dimensions = 8;
	[Export] public int numSites = 50;
	[Export] float lineWidth = 1.0f;

    public Dictionary<Vector2, VoronoiPolygon> polygons;
    public Dictionary<Vector2, VoronoiPolygon> basePolygons;
	List<Vector2> basePoints; //The points on the world map excluding the duplicated points (the duplicated points are so the world tiles)
    List<Plate2D> plates;

	GodotObject del;
    RandomNumberGenerator random;
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
		plates = new List<Plate2D>();
		polygons = new Dictionary<Vector2, VoronoiPolygon>();
		basePolygons = new Dictionary<Vector2, VoronoiPolygon>();
		basePoints = new List<Vector2>();
		random = new RandomNumberGenerator();

        GDScript delaunayScript = GD.Load<GDScript>("res://Scripts/Delaunay/Delaunay.gd");

		del = (GodotObject)delaunayScript.New();


		int dim = dimensions;
		float w = (map.worldWidth / dim);
		float h = (map.worldHeight / dim);

		float mx = map.worldWidth;
		float my = map.worldHeight;
		/*
        for (int i = 1; i <= dim; i++)
		{
			for (int j = 1; j <= dim; j++)
			{
				float x = (i * w) - (w/2) + random.RandiRange(-15, 15);
                float y = (j * h) - (h/2) + random.RandiRange(-15, 15);
                del.Call("add_point", new Vector2(x,y));
                //del.Call("add_point", new Vector2(x		, y + my));	//down
                //del.Call("add_point", new Vector2(x + mx, y + my));	//downright
                del.Call("add_point", new Vector2(x - mx, y		)); //left
                del.Call("add_point", new Vector2(x + mx, y		));	//right
                //del.Call("add_point", new Vector2(x + mx, y - my));	//upright
                //del.Call("add_point", new Vector2(x		, y - my));	//up
                //del.Call("add_point", new Vector2(x - mx, y - my));	//upleft
                //del.Call("add_point", new Vector2(x - mx, y + my));	//downleft
                //del.Call("add_point", new Vector2(50 + i * 100 + random.RandiRange(-15, 15), 50 + j * 50 + random.RandiRange(-15, 15)));
            }
		}*/
		PopulateWithPoints(numSites);

		var tris = del.Call("triangulate").As<Godot.Collections.Array>();
		foreach(var t in tris)
		{
			if (del.Call("is_border_triangle", t).As<Boolean>() == false)
			{
				//render triangle here using tri
				var tri = t.As<GodotObject>();
				ShowTriangle(tri);
            }
		}

		//Rect2 rect = new Rect2(0, 0, worldWidth, worldHeight);
		//del.Call("set_rectangle", rect);
		var voronoi = del.Call("make_voronoi", tris).As<Godot.Collections.Array>();
        //del.Call("remove_border_sites", voronoi);
        int iter = 0;
		foreach(var s in voronoi)
		{
            var site = s.As<GodotObject>();
            var neighbours = site.Get("neighbours").As<Godot.Collections.Array>();
            var sourceTris = site.Get("source_triangles").As<Godot.Collections.Array>();

            ShowSite(site, ref iter);

            if (neighbours.Count == sourceTris.Count)
            {
                foreach (var ne in neighbours)
                {
                    //render neighbour edge using neighbourEdge
                    var neighbourEdge = ne.As<GodotObject>();
                    ShowNeighbour(neighbourEdge);
                }
            }
        }

		int i = 0;
		foreach(var s in polygons)
		{
			s.Value.ID = i;
			i++;
		}


		DisplaySiteData();
    }

	void DuplicatePoints()
	{

	}

	void PopulateWithPoints(int count)
	{

		for (int c = 0; c < count; c++)
		{
			var x = random.RandiRange(0, map.worldWidth - 1);
            var y = random.RandiRange(0, map.worldHeight - 1);

			Vector2 point = new Vector2(x, y);
			basePoints.Add(point);

            del.Call("add_point", new Vector2(x, y));

            del.Call("add_point", new Vector2(x - map.worldWidth, y)); //left
            del.Call("add_point", new Vector2(x + map.worldWidth, y));	//right
        }
	}

	void DisplaySiteData()
	{
		foreach(var s in polygons)
		{
			var txt = new Godot.Label();
			txt.Text = s.Value.ID.ToString();
			txt.Position = s.Key;
			txt.Scale = new Vector2(0.4f, 0.4f);
			txt.SetSize(new Vector2(2000, 2000));

            AddChild(txt);

			foreach(var n in s.Value.neighbours)
			{
				//GD.Print(s.Value.index, " neighbours ", sites[n].index);
			}
		}
	}

	void ShowSite(GodotObject site, ref int iter)
	{
		var line = new Line2D();
        var p = site.Get("polygon").As<Vector2[]>();
        p.Append(p[0]);
		line.Points = p;
		line.Width = lineWidth;
		line.DefaultColor = Colors.Yellow;
		AddChild(line);

		List<Vector2> pl = new List<Vector2>();

        foreach (var point in p)
		{
            pl.Add(point);
        }


		var center = site.Get("center").As<Vector2>();
        VoronoiPolygon s = new VoronoiPolygon(center, pl);
		s.ID = iter;

		polygons.Add(center, s);
		if (basePoints.Contains(center))
			basePolygons.Add(center, s);

		iter++;

        /*var polygon = new Polygon2D();
		var p = site.Get("polygon").As<Vector2[]>();
		p.Append(p[0]);
		polygon.Polygon = p;
		polygon.Color = new Color(random.RandiRange(0, 1), random.RandiRange(0, 1), random.RandiRange(0, 1), 0.5f);
		polygon.ZIndex = -1;
		AddChild(polygon);*/
    }

	void ShowNeighbour(GodotObject edge)
	{
		//var edge = neighbourEdge.Get();
        var line = new Line2D();
		var points = new Vector2[2];
		var l = 2;
		var s = Lerp((Vector2)edge.Get("a"), (Vector2)edge.Get("b"), 0.6f);
		var a = edge.Get("a").As<Vector2>();
        var b = edge.Get("b").As<Vector2>();
        var dir = a.DirectionTo(b).Orthogonal();
        points[0] = s + dir * l;
        points[1] = s - dir * l;
		line.Points = points;
		line.Width = lineWidth;
		line.DefaultColor = Colors.Cyan;
		AddChild(line);

        var key = edge.Get("this").As<GodotObject>().Get("center").As<Vector2>();
		var other = edge.Get("other").As<GodotObject>().Get("center").As<Vector2>();
		if (polygons.ContainsKey(key))
		{
			polygons[key].neighbours.Add(other);
		}
		else
		{
			GD.PrintErr("Trying to access key that doesn't exist in Sites");
		}
		//sites[key].neighbours.Add(other);
    }

	void ShowTriangle(GodotObject triangle)
	{
		var line = new Line2D();
		var points = new Vector2[4];
		points[0] = (Vector2)triangle.Get("a");
        points[1] = (Vector2)triangle.Get("b");
        points[2] = (Vector2)triangle.Get("c");
        points[3] = (Vector2)triangle.Get("a");
		line.Points = points;
		line.Width = lineWidth;
		line.DefaultColor = Colors.ForestGreen;
		AddChild(line);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
	}

    float Lerp(float firstFloat, float secondFloat, float by)
    {
        return firstFloat * (1 - by) + secondFloat * by;
    }

    Vector2 Lerp(Vector2 firstVector, Vector2 secondVector, float by)
    {
        float retX = Lerp(firstVector.X, secondVector.X, by);
        float retY = Lerp(firstVector.Y, secondVector.Y, by);
        return new Vector2(retX, retY);
    }
}
