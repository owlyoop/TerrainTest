using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class MapViewer : Node2D
{
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

	[ExportCategory("References")]
	[Export] public UIController ui;
	[Export] public Camera2D cam1;
	[Export] public WorldMap map;
	[Export] public Node2D LineOverlay;
	[Export] public float camSpeed;
	[Export] public float zoomSpeed;
	

	[Export] public MeshInstance2D mapDisplay;

	Cell2D selectedCell;
	#region Multimesh Instances
	MultiMeshInstance2D mmiPlatePts;
	MultiMesh mmPlatePts;

	MultiMeshInstance2D mmiPlatePtVels;
	MultiMesh mmPlatePtVels;

	MultiMeshInstance2D mmiPlateVels;
	MultiMesh mmPlateVels;

	MultiMeshInstance2D mmiPlateCenters;
	MultiMesh mmPlateCenters;

	int plateIndex = 0;

	#endregion



	Cell2D[,] cells;

	Image img;
	float[,] avgHeights;
	int[,] counts;

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
			if (ui.PlateCheckButton.ButtonPressed)
			{
				ui.PlateInfoGroup.Visible = false;
				ui.CellInfoGroup.Visible = true;
				//GD.Print(GetViewport().GetMousePosition());
				var viewToWorld = cam1.GetCanvasTransform().AffineInverse();
				var worldPos = viewToWorld * GetViewport().GetMousePosition();
				//GD.Print(worldPos);
				OnCellSelected(GetCellFromPosition(worldPos));
			}
			else
			{
				ui.PlateInfoGroup.Visible = true;
				ui.CellInfoGroup.Visible = false;
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


	

	public void OnSpinBoxValueChanged(float value)
	{
		foreach (var n in LineOverlay.GetChildren())
		{
			LineOverlay.RemoveChild(n);
			n.QueueFree();
		}

		plateIndex = (int)value;

		DrawSelectedPlateOverlay();
	}

	void OnCellSelected(Cell2D cell)
	{
		var grid = map.worldGrid.grid;
		
		selectedCell = cell;
		if (cell != null)
		{
			ui.OnCellSelected(cell);
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

	void DrawSelectedPlateOverlay()
	{
		var plate = map.GetPlateByIndex(plateIndex);

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
					color = ContinentalColor;  //land

				else color = OceanicColor;

				//The image is created flipped because the Image uses 0,0 at the topleft but 2d arrays use 0,0 as bottomleft
				SetPixelWorld(i, j, color);
			}
		}

		return img;
	}

	void DisplayPlates()
	{
		int width = counts.GetLength(0);
		int height = counts.GetLength(1);
		Parallel.For(0, width, i =>
		{
			for (int j = 0; j < height; j++)
			{
				SetPixelWorld(i, j, DefaultColor);
				counts[i, j] = 0;
				avgHeights[i, j] = 0f;
			}
		});

		Parallel.ForEach(map.Plates, plate =>
		{
			foreach (var p in plate.points)
			{
				var x = p.gridIndex.X;
				var y = p.gridIndex.Y;

				counts[x, y]++;
				avgHeights[x, y] += (p.height / (float)counts[x, y]);
			}
		});

		Parallel.For(0, width, i =>
		{
			for (int j = 0; j < height; j++)
			{
				var cell = map.worldGrid.grid[i, j];
				var c = DefaultColor;
				if (cell.points.Count > 0)
				{
					if (cell.points[0].GetCrustType() == PlatePoint.CrustType.Oceanic)
						c = OceanicColor;
					else c = ContinentalColor;
				}
				if (cell.points.Count > 0)
				{
					float hue = (float)cell.points[0].plate.ID / (float)map.Plates.Count;
					Color hsv = new Color();
					hsv = Color.FromHsv(hue, 1f, 1f, 1f);
					c = c + (hsv*0.1f);
				}
				

				

				if (!cell.IsCompletelyEmpty())
				{
					if (cell.collisionType == PlateCollisionType.Transform)
					{
						c = DebugCollisionTransformColor;
					}
					else if (cell.collisionType == PlateCollisionType.Subduction)
					{
						c = DebugCollisionSubductionColor;
					}
					else if (cell.collisionType == PlateCollisionType.Orogenic)
					{
						c = DebugCollisionOrogenicColor;
					}
					else if (cell.collisionType == PlateCollisionType.Divergent)
					{
						c = DebugCollisionDivergentColor;
					}

					//if (cell.ContainsEdgeBoundary)
					//	c = Colors.BlueViolet + (cell.points[0].distTravelAsBoundary* Colors.White);
				}
				c = c + (c * avgHeights[i, j] * 0.5f);
				if (counts[i, j] > 0)
				{
					SetPixelWorld(i, j, c);
				}
					
			}
		});

		

		//for empty points, get average of surrounding points
		/*for (int i = 0; i < avgHeights.GetLength(0); i++)
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
		}*/
	}



	#endregion


}
