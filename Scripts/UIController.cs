using Godot;
using System;
using System.Linq;

public partial class UIController : Node
{
	[ExportCategory("UI")]
	[Export] public Label TimestepText;

	[Export] public Control PlateInfoGroup;
	[Export] public Control CellInfoGroup;

	[Export] public Button ButtonPlateSelectLeft;
	[Export] public Label LabelPlateID;
	[Export] public Button ButtonPlateSelectRight;
	[Export] public Label LabelPlateCenter;
	[Export] public Label LabelPlateVelocity;
	[Export] public Label LabelPlateRotation;
	[Export] public Label LabelPlateAngVel;
	[Export] public Label LabelNumPlatePts;
	

	[Export] public Label LabelCellCoords;
	[Export] public Label LabelCellNumPlatePts;
	[Export] public Label LabelCellUniquePlates;
	[Export] public Label LabelHasCollision;
	[Export] public Label LabelCollisionType;

	[Export] public Button ButtonPlatePtSelectLeft;
	[Export] public Button ButtonPlatePtSelectRight;

	[Export] public Label LabelFelsic;
	[Export] public Label LabelMafic;
	[Export] public Label LabelAge;
	[Export] public Label LabelMass;
	[Export] public Label LabelThickness;
	[Export] public Label LabelDensity;
	[Export] public Label LabelBuoyancy;
	[Export] public Label LabelHeight;

	[ExportCategory("References")]
	[Export] public MapViewer mapview;
	[Export] public Camera2D cam;
	[Export] public WorldMap map;
	WorldGrid worldGrid;

	[Signal]
	public delegate void PlateSelectionChangedEventHandler(int plateIndex);
	int selectedPlateIndex = 0;
	GridCell selectedCell;
	int selectedPlatePtIndex = 0;

	public override void _Ready()
	{
		worldGrid = map.worldGrid;
		map.OnTimestepCompleted += OnTimestepCompleted;
		mapview.CellSelected += OnCellSelected;
	}

	

	public override void _ExitTree()
	{
		if (map != null)
			map.OnTimestepCompleted -= OnTimestepCompleted;
		mapview.CellSelected -= OnCellSelected;
	}

	public override void _Input(InputEvent @event)
	{

	}

	private void OnCellSelected(Cell2D cell)
	{
		if (cell == null) return;
		var gridcell = worldGrid.grid[cell.x, cell.y];
		DisplayGridcellInfo(gridcell);
	}

	void OnButtonLeftPressed()
	{
		selectedPlateIndex = (selectedPlateIndex - 1) % (map.Plates.Count() - 1);
		if (selectedPlateIndex < 0)
			selectedPlateIndex = map.Plates.Count() - 1;
		else
			selectedPlateIndex = selectedPlateIndex % (map.Plates.Count() - 1);
		DisplayPlateInfo(selectedPlateIndex);
		EmitSignal(SignalName.PlateSelectionChanged, selectedPlateIndex);
	}

	void OnButtonRightPressed()
	{
		selectedPlateIndex = (selectedPlateIndex + 1);
		selectedPlateIndex = selectedPlateIndex % (map.Plates.Count() - 1);
		DisplayPlateInfo(selectedPlateIndex);
		EmitSignal(SignalName.PlateSelectionChanged, selectedPlateIndex);
	}

	void OnTimestepCompleted(int timestep)
	{
		TimestepText.Text = timestep.ToString();
		DisplayPlateInfo(selectedPlateIndex);
		if (selectedCell != null) DisplayGridcellInfo(selectedCell);
	}


	void DisplayPlateInfo(int index)
	{
		var plate = map.GetPlateByIndex(index);
		var center = new Vector2((float)Math.Round(plate.Center.X, 2), (float)Math.Round(plate.Center.Y, 2));
		LabelPlateCenter.Text = center.ToString();
		LabelPlateRotation.Text = plate.rotation.ToString();
		LabelPlateVelocity.Text = Math.Round(plate.Velocity.LengthSquared(), 2).ToString();
		LabelPlateAngVel.Text = plate.angularVelocity.ToString();
		LabelNumPlatePts.Text = plate.points.Count().ToString();
		LabelPlateID.Text = index.ToString();
	}

	void DisplayGridcellInfo(GridCell cell)
	{
		selectedCell = cell;
		var coords = new Vector2I(cell.x, cell.y);

		LabelCellCoords.Text = coords.ToString();
		LabelCellNumPlatePts.Text = cell.points.Count().ToString();
		LabelCellUniquePlates.Text = cell.PlateIDs.Count().ToString();
		LabelHasCollision.Text = cell.ContainsCollision.ToString();
		LabelCollisionType.Text = cell.collisionType.ToString();
	}

	void DisplayPlatepointInfo()
	{

	}

}
