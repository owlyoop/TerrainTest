using Godot;
using System;
using System.Linq;

public partial class MapViewer : Node
{
	[Export] public Camera2D cam1;
    [Export] public WorldMap map;
    [Export] public RichTextLabel cellText;
    [Export] public Node2D LineOverlay;
    [Export] public float camSpeed;
    [Export] public float zoomSpeed;
    // Called when the node enters the scene tree for the first time.

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

    void UpdateCellText(Cell2D cell)
    {
        if (cell != null)
            cellText.Text = "Cell(" + cell.x + ',' + cell.y + ") height: " + cell.height + ", color is: " + cell.color + ", plateID: " + cell.owner.ID;
    }

    void OnCellSelected(Cell2D cell)
    {
        selectedCell = cell;
        UpdateCellText(selectedCell);
        HighlightSelectedCell(cell);
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

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("Select"))
        {
            //GD.Print(GetViewport().GetMousePosition());
            var viewToWorld = cam1.GetCanvasTransform().AffineInverse();
            var worldPos = viewToWorld * GetViewport().GetMousePosition();
            GD.Print(worldPos);
            OnCellSelected(map.GetCellFromPosition(worldPos));
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
