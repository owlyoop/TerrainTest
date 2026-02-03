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

	public override void _Ready()
	{
        cam1.Position = new Vector2(map.worldWidth / 2f, map.worldHeight / 2f);
        cam1.Zoom *= 2.5f;
        OnCameraZoom();
		map.OnTimestepCompleted += HighlightSelectedPlate;

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
        selectedCell = cell;
        if (cell != null)
        {
            HighlightSelectedCell(cell);
            SelectedCellInfo.Text = cell.x.ToString() + ", " + cell.y.ToString();
            NumPlatePointsInCell.Text = map.worldGrid.grid[cell.x, cell.y].points.Count().ToString();

			GD.Print("----------");
			//GD.Print(cell.x.ToString() + ", " + cell.y.ToString());
			
			//GD.Print(map.hashgrid.GetIndexFromPosition(new Vector2(cell.x, cell.y)));
			GD.Print(map.worldGrid.grid[cell.x, cell.y].points.Count().ToString());
			foreach(var p in map.worldGrid.grid[cell.x, cell.y].points)
			{
				GD.Print(p.plate.ID, " , grid idx: ", p.gridIndex.X, " ", p.gridIndex.Y);
			}
			GD.Print("----------");
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
		mmiPlateVels.Modulate = Colors.White;

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
                OnCellSelected(map.GetCellFromPosition(worldPos));
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

}
