using Godot;
using System;
using System.Drawing;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Color = Godot.Color;

public partial class MapViewer : Node2D
{
	public enum MapMode
	{
		Elevation,
		Age,
		Density,
		Buoyancy,

	}
	public MapMode mapMode;
	#region Map Mode Flags
	bool _showSeaLevel;
	bool _showCollisions;

	#endregion

	[ExportCategory("Overlay Settings")]
	[Export] public bool OverlayPlatePt;
	[Export] public bool OverlayPlatePtVelocity;
	[Export] public bool OverlayPlateVelocity;

	[ExportCategory("Overlay Colors")]
	[Export] public Color PlatePtColor;
	[Export] public Color PlateVelocityColor;
	[Export] public Color PlatePtVelColor;
	[Export] public Color DebugCollisionTransformColor;
	[Export] public Color DebugCollisionDivergentColor;
	[Export] public Color DebugCollisionSubductionColor;
	[Export] public Color DebugCollisionOrogenicColor;
	[Export] public Color ContinentalColor;
	[Export] public Color OceanicColor;
	[Export] public Color DefaultColor;

	[ExportCategory("Map Colors")]
	[Export] public Gradient OceanGradient;
	[Export] public Gradient ElevationGradient;
	[Export] public Gradient AgeGradient;
	[Export] public Gradient DensityGradient;
	[Export] public Gradient BuoyancyGradient;

	[ExportCategory("References")]
	[Export] public UIController ui;
	[Export] public Camera2D cam1;
	[Export] public WorldMap map;
	[Export] public Node2D LineOverlay;
	[Export] public float camSpeed;
	[Export] public float zoomSpeed;
	

	[Export] public MeshInstance2D mapDisplay;

	Cell2D _selectedCell;
	#region Multimesh Instances
	MultiMeshInstance2D mmiPlatePts;
	MultiMesh mmPlatePts;

	MultiMeshInstance2D mmiPlatePtVels;
	MultiMesh mmPlatePtVels;

	MultiMeshInstance2D mmiPlateVels;
	MultiMesh mmPlateVels;

	MultiMeshInstance2D mmiPlateCenters;
	MultiMesh mmPlateCenters;

	int _plateIndex = 0;

	#endregion



	Cell2D[,] cells;

	Image img;

	float[,,] values;
	float[,,] weights;
	float[,,] final;

	[Signal]
	public delegate void CellSelectedEventHandler(Cell2D cell);

	#region Wire Mesh Vertices
	Vector2[] wmBox =
	{
		new(-0.1f, -0.1f), new(0.1f, -0.1f),
		new(0.1f, -0.1f), new(0.1f, 0.1f),
		new(0.1f, 0.1f), new(-0.1f, 0.1f),
		new(-0.1f, 0.1f), new(-0.1f, -0.1f)
	};

	Vector2[] wmSimpleArrow =
	{
		new(0f, 0f), new(0, 0.7f),
		new(0, 0.7f), new(0.1f, 0.6f),
		new(0, 0.7f), new(-0.1f, 0.6f)
	};

	Vector2[] wmArrow =
	{
		new(0.1f, 0f), new(-0.1f, 0f),
		new(-0.1f, 0f), new(-0.1f, 0.7f),
		new(-0.1f, 0.7f), new(-0.3f, 0.7f),
		new(-0.3f, 0.7f), new(0f, 1f),
		new(0f, 1f), new(0.3f, 0.7f),
		new(0.3f, 0.7f), new(0.1f, 0.7f),
		new(0.1f, 0.7f), new(0.1f, 0f)
	};

	Vector2[] wmPlateCenter =
	{
		new(0.5f, 0f), new(0f, -0.5f),
		new(0f, -0.5f), new(-0.5f, 0f),
		new(-0.5f, 0f), new(0f, 0.5f),
		new(0f, 0.5f), new(0.5f, 0f),
	};
	#endregion

	public override void _Ready()
	{
		cam1.Position = new Vector2(map.worldWidth / 2f, map.worldHeight / 2f);
		cam1.Zoom *= 2.5f;
		OnCameraZoom();
		


		map.OnTimestepCompleted += DrawSelectedPlateOverlay;
		ui.PlateSelectionChanged += OnPlateSelectionChanged;



		mapMode = MapMode.Elevation;
		
	}

	public override void _ExitTree()
	{
		ui.PlateSelectionChanged -= OnPlateSelectionChanged;
		map.OnTimestepCompleted -= DrawSelectedPlateOverlay;
	}

	public void Initialize(int width, int height)
	{
		var numPlates = map.Plates.Count;

		values = new float[numPlates, width, height];
		weights = new float[numPlates, width, height];
		final = new float[numPlates, width, height];

		GenerateCells(width, height);
		CreateMesh();
	}

	public void DisplayMap()
	{
		
		DisplayPlatesSmoothed();
		RedrawMap();
	}

	void OnPlateSelectionChanged(int index)
	{
		_plateIndex = index;
		DrawSelectedPlateOverlay(0);
	}


	#region Input
	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("Select"))
		{
			var viewToWorld = cam1.GetCanvasTransform().AffineInverse();
			var worldPos = viewToWorld * GetViewport().GetMousePosition();
			OnCellSelected(GetCellFromPosition(worldPos));

		}
		if (@event.IsActionPressed("Cam_Zoom_In"))
		{
			cam1.Zoom *= zoomSpeed;
			OnCameraZoom();
		}
		if (@event.IsActionPressed("Cam_Zoom_Out"))
		{
			cam1.Zoom *= (1.0f / zoomSpeed);
			OnCameraZoom();
		}
	}

	public void GetInput()
	{
		Vector2 inputDirection = Input.GetVector("Cam_Move_Left", "Cam_Move_Right", "Cam_Move_Up", "Cam_Move_Down");
		cam1.Translate(inputDirection * camSpeed * (1f / (cam1.Zoom.Y + cam1.Zoom.X)));
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		GetInput();
	}

	void OnCameraZoom()
	{
		cam1.Scale = new Vector2(1 / cam1.Zoom.X, 1 / cam1.Zoom.Y);
	}

	#endregion


	#region UI


	

	public void OnSpinBoxValueChanged(float value)
	{
		foreach (var n in LineOverlay.GetChildren())
		{
			LineOverlay.RemoveChild(n);
			n.QueueFree();
		}

		_plateIndex = (int)value;

		DrawSelectedPlateOverlay(0);
	}

	void OnCellSelected(Cell2D cell)
	{
		var grid = map.worldGrid.grid;
		
		_selectedCell = cell;
		if (cell != null)
		{
			EmitSignal(SignalName.CellSelected, cell);
			HighlightSelectedCell(cell);
		}
	}

	void HighlightSelectedCell(Cell2D cell)
	{
		foreach (var n in LineOverlay.GetChildren())
		{
			LineOverlay.RemoveChild(n);
			n.QueueFree();
		}
		if (cell != null)
		{
			var nw = new Vector2I(cell.x, cell.y);
			var ne = new Vector2I(cell.x + 1, cell.y);
			var se = new Vector2I(cell.x + 1, cell.y + 1);
			var sw = new Vector2I(cell.x, cell.y + 1);
			var selectLine = new Line2D();
			selectLine.Width = 0.2f;
			selectLine.DefaultColor = new Color(1, 1, 1, 0.5f);
			var p = new Vector2[4];
			p[0] = nw;
			p[1] = ne;
			p[2] = se;
			p[3] = sw;
			selectLine.Points = p;
			selectLine.Closed = true;
			LineOverlay.AddChild(selectLine);
		}

	}

	#endregion


	#region Overlay Rendering

	void DrawSelectedPlateOverlay(int timestep)
	{
		var plate = map.GetPlateByIndex(_plateIndex);

		CreateOverlay(ref mmPlatePts, ref mmiPlatePts,
			CreateWireMesh(wmBox, 2f),
			plate.points.Count(),
			PlatePtColor);

		CreateOverlay(ref mmPlateVels, ref mmiPlateVels,
			CreateWireMesh(wmArrow, 12f),
			map.Plates.Count(),
			PlateVelocityColor);

		CreateOverlay(ref mmPlatePtVels, ref mmiPlatePtVels,
			CreateWireMesh(wmSimpleArrow, 1f),
			plate.points.Count(),
			PlatePtVelColor);

		CreateOverlay(ref mmPlateCenters, ref mmiPlateCenters,
			CreateWireMesh(wmPlateCenter, 1f),
			map.Plates.Count(),
			Colors.Cyan);

		if (OverlayPlateVelocity)
		{
			for (int i = 0; i < map.Plates.Count; i++)
			{
				var transform = new Transform2D(map.Plates[i].Velocity.Angle() - (MathF.PI / 2),
					map.Plates[i].Center);
				mmPlateVels.SetInstanceTransform2D(i, transform);

				var transform2 = new Transform2D(0f,
					map.Plates[i].Center);
				mmPlateCenters.SetInstanceTransform2D(i, transform2);
			}
		}

		for (int i = 0; i < plate.points.Count(); i++)
		{
			var p = plate.points[i];

			if (OverlayPlatePt && p.isActive)
			{
				var ptsTransform = new Transform2D(Mathf.DegToRad(plate.rotation), p.WorldPos + new Vector2(0.0f, 0.0f));
				mmPlatePts.SetInstanceTransform2D(i, ptsTransform);
			}

			if (OverlayPlatePtVelocity && p.isActive)
			{
				
				var velTransform = new Transform2D(p.boundaryNormal.Angle() ,
				new Vector2(1f, Mathf.Clamp(p.Velocity.Length() * 10f, 1f, 5f)),
				0f,
				p.WorldPos + new Vector2(0.0f, 0.0f));
				mmPlatePtVels.SetInstanceTransform2D(i, velTransform);
			}
		}
	}

	void CreateOverlay(ref MultiMesh mm, ref MultiMeshInstance2D mmi,  ArrayMesh mesh, int instanceCount, Color modulate)
	{
		if (mmi != null && IsInstanceValid(mmi))
		{
			RemoveChild(mmi);
			mmi.QueueFree();
		}

		mm = new MultiMesh();
		mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform2D;
		mm.InstanceCount = instanceCount;

		mm.Mesh = mesh;

		mmi = new MultiMeshInstance2D();
		mmi.Multimesh = mm;
		mmi.Modulate = modulate;

		AddChild(mmi);
	}

	ArrayMesh CreateWireMesh(Vector2[] vertices, float scale)
	{
		Vector2[] scaled = new Vector2[vertices.Count()];
		for (int i = 0; i < vertices.Count(); i++)
			scaled[i] = vertices[i] * scale;

		var mesh = new ArrayMesh();

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = scaled;
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);

		return mesh;
	}
	#endregion


	#region Map Rendering
	Image InitializeImage()
	{
		var img = Image.CreateEmpty(map.worldWidth, map.worldHeight, false, Image.Format.Rgb8);
		return img;
	}

	//Image uses 0,0 as the topleft but 2d arrays use 0,0 as bottomleff


	void GenerateCells(int width, int height)
	{
		cells = new Cell2D[width, height];
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				cells[i, j] = new Cell2D(i, j);
				cells[i, j].SetHeight(map.noiseGen.GetNoise2D(i, j));
			}
		}
	}

	public Cell2D GetCellFromPosition(Vector2 pos)
	{
		if ((int)pos.X >= 0 && (int)pos.X < map.worldWidth &&
			(int)pos.Y >= 0 && (int)pos.Y < map.worldHeight)
		{
			return cells[(int)pos.X, (int)pos.Y];
		}
		else return null;
	}

	void CreateMesh()
	{
		mapDisplay.Scale = new Vector2(map.worldWidth, map.worldHeight);
		mapDisplay.Position = new Vector2(map.worldWidth / 2f, map.worldHeight / 2f);

		img = CreateImageFromCells();

		var texture = ImageTexture.CreateFromImage(img);
		mapDisplay.Texture = texture;
	}

	void RedrawMap()
	{
		var texture = ImageTexture.CreateFromImage(img);
		mapDisplay.Texture = texture;
	}


	Image CreateImageFromCells()
	{
		int width = cells.GetLength(0);
		int height = cells.GetLength(1);

		img = Image.CreateEmpty(width, height, false, Image.Format.Rgb8);

		for (int i = 0; i < cells.GetLength(0); i++)
		{
			for (int j = 0; j < cells.GetLength(1); j++)
			{
				Color color;

				var h = Math.Abs(cells[i, j].height);
				var c = 1 - h;
				if (cells[i, j].height >= 0f)
					color = ContinentalColor;  //land

				else color = OceanicColor;

				//The image is created flipped because the Image uses 0,0 at the topleft but 2d arrays use 0,0 as bottomleft
				SetPixelWorld(i, j, color);
			}
		}

		return img;
	}

	//Bilinear
	void DisplayPlatesSmoothed()
	{
		int width = cells.GetLength(0);
		int height = cells.GetLength(1);

		var numPlates = map.Plates.Count;

		values = new float[numPlates, width, height];
		weights = new float[numPlates, width, height];
		final = new float[numPlates, width, height];

		void AddWeight(int p, int x, int y, float value, float weight)
		{
			if (weight <= 0f) return;
			x = x % width;
			if (x < 0) x += width;
			y = y % height;
			if (y < 0) y += height;
			values[p, x, y] += value * weight;

			weights[p, x, y] += weight;
		}

		Parallel.For(0, map.Plates.Count, plateIdx =>
		{
			var plate = map.Plates[plateIdx];

			foreach(var p in plate.points)
			{
				float val = GetPixelValue(p);
				float cx = p.WorldPos.X;
				float cy = p.WorldPos.Y;
				int x0 = Mathf.FloorToInt(cx);
				int y0 = Mathf.FloorToInt(cy);
				float tx = cx - x0;
				float ty = cy - y0;

				float w00 = (1f - tx) * (1f - ty);
				float w10 = tx * (1f - ty);
				float w01 = (1f - tx) * ty;
				float w11 = tx * ty;

				AddWeight(plateIdx, x0, y0, val, w00);
				AddWeight(plateIdx, x0 + 1, y0, val, w10);
				AddWeight(plateIdx, x0, y0 + 1, val, w01);
				AddWeight(plateIdx, x0 + 1, y0 + 1, val, w11);
			}

			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					if (weights[plateIdx, x, y] > 0)
						final[plateIdx, x, y] = values[plateIdx, x, y] / weights[plateIdx, x, y];
					else final[plateIdx, x, y] = 0;
				}
			}
		});

		Parallel.For(0, width, x =>
		{
			for (int y = 0; y < height; y++)
			{
				float val = 0f;
				float totalWeight = 0f;
				for (int p = 0; p < final.GetLength(0); p++)
				{
					float plateWeight = weights[p, x, y];
					if (plateWeight > 0)
					{
						val += final[p, x, y] * plateWeight;
						totalWeight += plateWeight;
					}

				}

				if (totalWeight > 0)
					val /= totalWeight;

				SetPixelWorld(x, y, GetPixelColor(val));
			}
		});
	}

	float GetPixelValue(PlatePoint point)
	{
		float val = 0f;
		switch (mapMode)
		{
			case MapMode.Elevation:
				val = Mathf.Remap(point.height, 0f, 1f, 0f, 1f);
				val = Mathf.Clamp(val, 0f, 1f);
				break;

			case MapMode.Age:
				val = Mathf.Remap(point.age, 0f, 900f, 0f, 1f);
				val = Mathf.Clamp(val, 0f, 1f);
				break;

			case MapMode.Density:
				val = Mathf.Remap(point.density, 2500f, 3000f, 0f, 1f);
				break;

			case MapMode.Buoyancy:
				val = Mathf.Remap(point.buoyancy, 0.1f, 0.4f, 0f, 1f);
				break;

			default:
				val = 0f;
				break;
		}
		return val;
	}

	Color GetPixelColor(float value)
	{
		Color col = Colors.White;
		
		switch (mapMode)
		{
			case MapMode.Elevation:
				col = ElevationGradient.Sample(value);
				break;

			case MapMode.Age:
				col = AgeGradient.Sample(value);
				break;

			case MapMode.Density:
				col = DensityGradient.Sample(value);
				break;

			case MapMode.Buoyancy:
				col = BuoyancyGradient.Sample(value);
				break;
		}

		return col;
	}

	void SetPixelWorld(int x, int y, Color color)
	{
		img.SetPixel(x, img.GetHeight() - 1 - y, color);
	}

	#endregion
}
