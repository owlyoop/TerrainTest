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
	[Export] public Label LabelCellAvgHeight;

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

	[ExportCategory("Mapmode UI")]
	[Export] public Button MapButtonElevation;
	[Export] public Button MapButtonAge;

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
		selectedPlatePtIndex = 0;
		DisplayGridcellInfo(gridcell);
	}

	void OnPlayButtonPressed()
	{
		map.IsRunning = true;
		if (!map.timer.IsConnected(Timer.SignalName.Timeout, Callable.From(map.Timestep)))
			map.timer.Timeout += map.Timestep;
	}

	void OnPauseButtonPressed()
	{
		map.IsRunning = false;
		if (map.timer.IsConnected(Timer.SignalName.Timeout, Callable.From(map.Timestep)))
			map.timer.Timeout -= map.Timestep;
	}

	void OnPlateButtonLeftPressed()
	{
		selectedPlateIndex = (selectedPlateIndex - 1) % (map.Plates.Count() - 1);
		if (selectedPlateIndex < 0)
			selectedPlateIndex = map.Plates.Count() - 1;
		else
			selectedPlateIndex = selectedPlateIndex % (map.Plates.Count() - 1);
		DisplayPlateInfo(selectedPlateIndex);
		EmitSignal(SignalName.PlateSelectionChanged, selectedPlateIndex);
	}

	void OnPlateButtonRightPressed()
	{
		selectedPlateIndex = (selectedPlateIndex + 1);
		selectedPlateIndex = selectedPlateIndex % (map.Plates.Count() - 1);
		DisplayPlateInfo(selectedPlateIndex);
		EmitSignal(SignalName.PlateSelectionChanged, selectedPlateIndex);
	}

	void OnPlatePtButtonLeftPressed()
	{
		selectedPlatePtIndex--;
		if (selectedPlatePtIndex < 0)
			selectedPlatePtIndex = 0;
		DisplayPlatepointInfo(selectedCell.points[selectedPlatePtIndex]);
	}

	void OnPlatePtButtonRightPressed()
	{
		selectedPlatePtIndex++;
		if (selectedPlatePtIndex > selectedCell.points.Count() - 1)
			selectedPlatePtIndex = selectedCell.points.Count - 1;
		DisplayPlatepointInfo(selectedCell.points[selectedPlatePtIndex]);
	}

	void OnTimestepCompleted(int timestep)
	{
		TimestepText.Text = timestep.ToString();
		DisplayPlateInfo(selectedPlateIndex);
		if (selectedCell != null)
		{
			DisplayGridcellInfo(selectedCell);
			if (selectedCell.points.Count > 0)
				DisplayPlatepointInfo(selectedCell.points[selectedPlatePtIndex]);
		}
		
	}

	void OnMapmodeElevationButtonPressed()
	{
		mapview.mapMode = MapViewer.MapMode.Elevation;
	}

	void OnMapmodeAgeButtonPressed()
	{
		mapview.mapMode = MapViewer.MapMode.Age;
	}

	void OnMapmodeDensityButtonPressed()
	{
		mapview.mapMode = MapViewer.MapMode.Density;
	}

	void OnMapmodeBuyoancyButtonPressed()
	{
		mapview.mapMode = MapViewer.MapMode.Buoyancy;
	}


	void DisplayPlateInfo(int index)
	{
		var plate = map.GetPlateByIndex(index);
		var center = new Vector2((float)Math.Round(plate.Center.X, 2), (float)Math.Round(plate.Center.Y, 2));
		LabelPlateCenter.Text = center.ToString();
		LabelPlateRotation.Text = plate.rotation.ToString();
		LabelPlateVelocity.Text = Math.Round(plate.Velocity.Length(), 3).ToString();
		LabelPlateAngVel.Text = Math.Round(plate.angularVelocity, 4).ToString();
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

		var avgh = 0f;
		int count = 0;
		foreach(var p in cell.points)
		{
			avgh += p.height;
			count++;
		}
		avgh = avgh / count;

		LabelCellAvgHeight.Text = Math.Round(avgh, 2).ToString();
	}

	void DisplayPlatepointInfo(PlatePoint point)
	{
		LabelFelsic.Text = Math.Round(point.Felsic).ToString();
		LabelMafic.Text = Math.Round(point.Mafic).ToString();
		LabelAge.Text = Math.Round(point.age).ToString();
		LabelMass.Text = Math.Round(point.mass).ToString();
		LabelThickness.Text = Math.Round(point.thickness, 2).ToString();
		LabelDensity.Text = Math.Round(point.density).ToString();
		LabelBuoyancy.Text = Math.Round(point.buoyancy, 2).ToString();
		LabelHeight.Text = Math.Round(point.height, 2).ToString();
	}

}
