using Godot;
using System;
using System.Linq;

public partial class MapViewer : Node
{
	[Export] public Camera2D cam1;
    [Export] public WorldMap map;
    [Export] public Node2D LineOverlay;
    [Export] public float camSpeed;
    [Export] public float zoomSpeed;

	[Export] public MeshInstance2D mapDisplay;

	[ExportCategory("UI")]
    [Export] public Control PlateInfoGroup;
    [Export] public Control CellInfoGroup;
    [Export] public SpinBox PlateSpinBox;
	[Export] public CheckButton PlateCheckButton;
    [Export] public RichTextLabel SelectedPlatePosition;
	[Export] public RichTextLabel SelectedPlateRotation;
	[Export] public RichTextLabel SelectedPlateDensity;
	[Export] public RichTextLabel SelectedCellInfo;
    [Export] public RichTextLabel NumPlatePointsInCell;

	Cell2D selectedCell;

	MultiMeshInstance2D mmiPlatePts;
	MultiMesh mmPlatePts;
	MultiMeshInstance2D mmiPlateVels;
	MultiMesh mmPlateVels;

	int plateIndex = 0;


	Cell2D[,] cells;

	Image img;
	float[,] avgHeights;
	int[,] counts;


	public override void _Ready()
	{
        cam1.Position = new Vector2(map.worldWidth / 2f, map.worldHeight / 2f);
        cam1.Zoom *= 2.5f;
        OnCameraZoom();
		map.OnTimestepCompleted += HighlightSelectedPlate;

		avgHeights = new float[map.worldWidth, map.worldHeight];
		counts = new int[map.worldWidth, map.worldHeight];
	}

	public void Initialize(int width, int height)
	{
		GenerateCells(width, height);
		CreateMesh();
	}

	public void DisplayMap()
	{
		DisplayPlates();
		RedrawMap();
	}


	#region Input
	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("Select"))
		{
			if (PlateCheckButton.ButtonPressed)
			{
				PlateInfoGroup.Visible = false;
				CellInfoGroup.Visible = true;
				//GD.Print(GetViewport().GetMousePosition());
				var viewToWorld = cam1.GetCanvasTransform().AffineInverse();
				var worldPos = viewToWorld * GetViewport().GetMousePosition();
				//GD.Print(worldPos);
				OnCellSelected(GetCellFromPosition(worldPos));
			}
			else
			{
				PlateInfoGroup.Visible = true;
				CellInfoGroup.Visible = false;
			}

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
	

	void DisplayPlateInfo()
    {
		var plate = map.GetPlateByIndex(plateIndex);
		SelectedPlatePosition.Text = plate.origin.ToString();
		SelectedPlateRotation.Text = plate.rotation.ToString();
        SelectedPlateDensity.Text = plate.density.ToString();
    }

    public void OnSpinBoxValueChanged(float value)
    {
		foreach (var n in LineOverlay.GetChildren())
		{
			LineOverlay.RemoveChild(n);
			n.QueueFree();
		}

		plateIndex = (int)value;
		
		HighlightSelectedPlate();
		DisplayPlateInfo();
	}

    void OnCellSelected(Cell2D cell)
    {
		var grid = map.worldGrid.grid;

		selectedCell = cell;
        if (cell != null)
        {
            HighlightSelectedCell(cell);
            SelectedCellInfo.Text = cell.x.ToString() + ", " + cell.y.ToString();
            NumPlatePointsInCell.Text = grid[cell.x, cell.y].points.Count().ToString();

			GD.Print("\n" + NumPlatePointsInCell.Text);
			foreach(var p in grid[cell.x, cell.y].points)
			{
				GD.Print(p.isActive);
				GD.Print(p.plate.ID, " , grid idx: ", p.gridIndex.X, " ", p.gridIndex.Y);
				GD.Print(p.collisionType);
				GD.Print("----");
			}
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
            selectLine.DefaultColor = new Color(1,1,1,0.5f);
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

	void HighlightSelectedPlate()
	{
		var plate = map.GetPlateByIndex(plateIndex);
		
		if (mmPlatePts != null)
			mmPlatePts.InstanceCount = 0;
		if (mmPlatePts == null || mmPlatePts.InstanceCount != plate.points.Count())
			CreatePlatePtsOverlay();

		if (mmPlateVels != null)
			mmPlateVels.InstanceCount = 0;
		if (mmPlateVels == null || mmPlateVels.InstanceCount != plate.points.Count())
			CreatePlateVelsOverlay();


		for (int i = 0; i < plate.points.Count(); i++)
		{
			var p = plate.points[i];

			var ptsTransform = new Transform2D(Mathf.DegToRad(plate.rotation), p.WorldPos + new Vector2(0.0f, 0.0f));
			mmPlatePts.SetInstanceTransform2D(i, ptsTransform);

			//if (!p.isActive) continue;
			//if (!p.IsColliding && !p.IsBorderingOtherPlate) continue;

			var velTransform = new Transform2D(p.Velocity.Angle() - (MathF.PI/2), 
				new Vector2(1f, Mathf.Clamp(p.Velocity.Length() * 10f, 1f, 5f)),
				0f, 
				p.WorldPos + new Vector2(0.0f, 0.0f));
			mmPlateVels.SetInstanceTransform2D(i, velTransform);
		}
	}

	#endregion


	#region Overlay Rendering
	void CreatePlatePtsOverlay()
	{
		var plate = map.GetPlateByIndex(plateIndex);
		mmPlatePts = new MultiMesh();
		mmPlatePts.TransformFormat = MultiMesh.TransformFormatEnum.Transform2D;
		mmPlatePts.InstanceCount = plate.points.Count();

		var mesh = CreateWireBox();
		mmPlatePts.Mesh = mesh;

		mmiPlatePts = new MultiMeshInstance2D();
		mmiPlatePts.Multimesh = mmPlatePts;
		mmiPlatePts.Modulate = Colors.Yellow;

		AddChild(mmiPlatePts);
	}

	ArrayMesh CreateWireBox()
	{
		var vertices = new Vector2[]
		{
			new(-0.1f, -0.1f), new(0.1f, -0.1f),
			new(0.1f, -0.1f), new(0.1f, 0.1f),
			new(0.1f, 0.1f), new(-0.1f, 0.1f),
			new(-0.1f, 0.1f), new(-0.1f, -0.1f)
		};
		var mesh = new ArrayMesh();

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);

		return mesh;
	}

	void CreatePlateVelsOverlay()
	{
		var plate = map.GetPlateByIndex(plateIndex);
		mmPlateVels = new MultiMesh();
		mmPlateVels.TransformFormat = MultiMesh.TransformFormatEnum.Transform2D;
		mmPlateVels.InstanceCount = plate.points.Count();

		var mesh = CreateVelocityArrow();
		mmPlateVels.Mesh = mesh;

		mmiPlateVels = new MultiMeshInstance2D();
		mmiPlateVels.Multimesh = mmPlateVels;
		mmiPlateVels.Modulate = Colors.Red;

		AddChild(mmiPlateVels);
	}

	ArrayMesh CreateVelocityArrow()
	{
		var vertices = new Vector2[]
		{
			new(0f, 0f), new(0, 0.7f),
			new(0, 0.7f), new(0.1f, 0.2f),
			new(0, 0.7f), new(-0.1f, 0.2f),
		};

		var mesh = new ArrayMesh();

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
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
	void SetPixelWorld(int x, int y, Color color)
	{
		img.SetPixel(x, img.GetHeight() - 1 - y, color);
	}

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
					color = new Color(Mathf.Lerp(0f, 0.4f, h),
											Mathf.Lerp(0.25f, 1f, h),
											Mathf.Lerp(0f, 0.4f, h));  //land

				else color = new Color(Mathf.Lerp(0f, 0.25f, c),
											Mathf.Lerp(0f, 0.25f, c),
											Mathf.Lerp(0.1f, 1f, c));  //water

				//The image is created flipped because the Image uses 0,0 at the topleft but 2d arrays use 0,0 as bottomleft
				SetPixelWorld(i, j, color);
			}
		}

		return img;
	}

	void DisplayPlates()
	{
		for (int i = 0; i < counts.GetLength(0); i++)
		{
			for (int j = 0; j < counts.GetLength(1); j++)
			{
				SetPixelWorld(i, j, Colors.Black);
				counts[i, j] = 0;
				avgHeights[i, j] = 0f;
			}
		}

		foreach (var plate in map.Plates)
		{
			foreach (var p in plate.points)
			{
				var x = p.gridIndex.X;
				var y = p.gridIndex.Y;

				counts[x, y]++;
				avgHeights[x, y] += (p.height / (float)counts[x, y]);
			}
		}

		for (int i = 0; i < map.worldGrid.grid.GetLength(0); i++)
		{
			for (int j = 0; j < map.worldGrid.grid.GetLength(1); j++)
			{
				var cell = map.worldGrid.grid[i, j];
				var c = new Color(0.3f, 0.4f, 0.5f);
				if (cell.points.Count > 0)
				{
					if (cell.points[0].GetCrustType() == PlatePoint.CrustType.Oceanic)
						c = new Color(0.5f, 0.6f, 0.75f);
					else c = new Color(0.5f, 0.8f, 0.5f);
				}

				if (!cell.IsCompletelyEmpty())
				{
					if (cell.collisionType == PlateCollisionType.Transform)
					{
						c = Colors.Green;
					}
					else if (cell.collisionType == PlateCollisionType.Subduction)
					{
						c = Colors.Red;
					}
					else if (cell.collisionType == PlateCollisionType.Orogenic)
					{
						c = Colors.Cyan;
					}
					else if (cell.collisionType == PlateCollisionType.Divergent)
					{
						c = Colors.Yellow;
					}
					
				}
				SetPixelWorld(i, j, c);
			}
		}

		//for empty points, get average of surrounding points
		for (int i = 0; i < avgHeights.GetLength(0); i++)
		{
			for (int j = 0; j < avgHeights.GetLength(1); j++)
			{
				if (counts[i, j] == 0)
				{
					var b = 0;
					var h = 0f;
					for (int dx = -1; dx <= 1; dx++)
					{
						for (int dy = -1; dy <= 1; dy++)
						{
							if (dx == 0 & dy == 0) continue;
							int di = i + dx;
							int dj = j + dy;

							if (di >= 0 && di < counts.GetLength(0)
								&& dj >= 0 && dj < counts.GetLength(1))
							{
								if (counts[di, dj] > 0)
								{
									b++;
									h += avgHeights[di, dj];
								}
							}
						}
					}

					h = h / b;
					var c = Colors.DarkSeaGreen;
					if (h < 0.5f)
						c = Colors.DeepSkyBlue;
					c *= (h + 0.5f);
					//SetPixelWorld(i, j, c);
				}
			}
		}
	}



	#endregion


}
