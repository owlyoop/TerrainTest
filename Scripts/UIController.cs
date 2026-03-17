using Godot;
using System;
using System.Linq;

public partial class UIController : Node
{
	[ExportCategory("UI")]
	[Export] public Control PlateInfoGroup;
	[Export] public Control CellInfoGroup;
	[Export] public SpinBox PlateSpinBox;
	[Export] public CheckButton PlateCheckButton;
	[Export] public RichTextLabel SelectedPlatePosition;
	[Export] public RichTextLabel SelectedPlateRotation;
	[Export] public RichTextLabel SelectedCellInfo;
	[Export] public RichTextLabel NumPlatePointsInCell;
	[Export] public RichTextLabel celldebug;

	[ExportCategory("References")]
	[Export] public MapViewer mapview;
	[Export] public Camera2D cam;
	[Export] public WorldMap map;
	WorldGrid worldGrid;

	public override void _Ready()
	{
		worldGrid = map.worldGrid;
	}

	public override void _Input(InputEvent @event)
	{

	}

	public void OnCellSelected(Cell2D cell)
	{
		var grid = worldGrid.grid;
		if (cell != null)
		{
			SelectedCellInfo.Text = cell.x.ToString() + ", " + cell.y.ToString();
			NumPlatePointsInCell.Text = grid[cell.x, cell.y].points.Count().ToString();

			celldebug.Text = "";
			string celltext = "";
			foreach (var p in grid[cell.x, cell.y].points)
			{
				celltext += p.plate.ID + " , grid idx: " + p.gridIndex.X + " " + p.gridIndex.Y + "\n";
				celltext += "Age: " + p.age + " , collisiontype: " + p.collisionType + "\n";
				celltext += "Felsic: " + p.Felsic + " , Mafic: " + p.Mafic + "\n";
				celltext += "Height: " + Mathf.Round(p.height * 1000) + "m " + " , density: " + p.density +
					" , thickness: " + Mathf.Round(p.thickness * 1000) + "m\n";
				celltext += "----\n";
			}
			celldebug.Text = celltext;
		}
	}

	void OnSpinBoxValueChanged(float value)
	{
		DisplayPlateInfo((int)value);
	}

	void DisplayPlateInfo(int index)
	{
		var plate = map.GetPlateByIndex(index);
		SelectedPlatePosition.Text = plate.origin.ToString();
		SelectedPlateRotation.Text = plate.rotation.ToString();
	}

}
