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

    public override void _Ready()
	{
        cam1.Position = new Vector2(map.worldWidth / 2f, map.worldHeight / 2f);
        cam1.Zoom *= 2.5f;
        OnCameraZoom();
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

    void DisplayPlateInfo(Plate2D plate)
    {
        SelectedPlatePosition.Text = plate.origin.ToString();
        //SelectedPlateRotation.Text = plate.rot
        SelectedPlateDensity.Text = plate.density.ToString();
    }

    public void OnSpinBoxValueChanged(float value)
    {
		foreach (var n in LineOverlay.GetChildren())
		{
			LineOverlay.RemoveChild(n);
			n.QueueFree();
		}

		int i = (int)value;
		var plate = map.GetPlateByIndex(i);
		HighlightSelectedPlate(plate);
		DisplayPlateInfo(plate);

	}

    void OnCellSelected(Cell2D cell)
    {
        selectedCell = cell;
        if (cell != null)
        {
            HighlightSelectedCell(cell);
            SelectedCellInfo.Text = cell.x.ToString() + ", " + cell.y.ToString();
            NumPlatePointsInCell.Text = map.hashgrid.grid[cell.x, cell.y].Count().ToString();
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

	void HighlightSelectedPlate(Plate2D plate)
	{

		foreach(var p in plate.points)
		{
			var n = new Vector2(p.position.X + 0.5f, p.position.Y + 0.5f);
			var s = new Vector2(p.position.X + 0.5f, p.position.Y);
			var line = new Line2D();
			line.Width = 0.32f;
			line.DefaultColor = new Color(1, 1, 1, 0.5f);
			var l = new Vector2[2];
			l[0] = n;
			l[1] = s;
			line.Points = l;
			LineOverlay.AddChild(line);
		}
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
                GD.Print(worldPos);
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
