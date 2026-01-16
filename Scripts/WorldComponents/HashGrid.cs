using Godot;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;

//Class used for the Hashgrid
public class GridCell
{
	public List<PlatePoint> points;
	public bool isActive = false;

	public bool containsBoundary = false;	//If any point in this gridcell is a plate boundary
	public bool containsCollision = false;  //If any point in this gridcell is a plate collision
	public bool hasCollisionChecked = false;	//Used for the update functions that check for collisions/boundaries to avoid repeated checks.

	public GridCell()
	{
		points = new List<PlatePoint>();
	}

	public void AddPoint(PlatePoint point)
	{
		points.Add(point);
	}

	public void RemovePoint(PlatePoint point)
	{
		points.Remove(point);
	}

	public bool CheckIfActive()
	{
		if (containsBoundary) return true;
		else if (containsCollision) return true;
		else return false;
	}
	public bool CheckIfEmpty()
	{
		if (points.Count == 0) 
			return true; 
		else return false;
	}
}

public partial class HashGrid
{

	public GridCell[,] grid;

	int Width;
	int Height;

	public HashGrid(int Width, int Height)
	{
		this.Width = Width;
		this.Height = Height;
		grid = new GridCell[Width, Height];
		for (int x = 0; x < Width; x++)
		{
			for (int y = 0; y < Height; y++)
			{
				grid[x,y] = new GridCell();
			}
		}
	}

	/*public void AddPointOLD(PlatePoint point)
	{
		var idx = GetIndexFromPosition(point.GetWorldPos());

		point.gridIndex = new Vector2I(idx.Item1, idx.Item2);
		grid[idx.Item1, idx.Item2].AddPoint(point);
	}*/

	public void AddPoint(PlatePoint point)
	{
		var idx = GetIndexFromPosition(point.WorldPos);
		point.gridIndex = new Vector2I(idx.Item1, idx.Item2);
		grid[point.gridIndex.X, point.gridIndex.Y].AddPoint(point);
	}

	public void RemovePoint(PlatePoint point)
	{
		var idx = point.gridIndex;

		if (CheckIfIndexInBounds(idx.X, idx.Y))
			grid[idx.X, idx.Y].RemovePoint(point);
	}

	public void MovePoint(PlatePoint point)
	{
		var oldIdx = point.gridIndex;
		var newIdxTuple = GetIndexFromPosition(point.WorldPos);
		var newIdx = new Vector2I(newIdxTuple.Item1, newIdxTuple.Item2);

		if (oldIdx != newIdx)
		{
			RemovePoint(point);
			point.gridIndex = newIdx;
			AddPoint(point);
		}
	}


	public Tuple<int, int> GetIndexFromPosition(Vector2 pos)
	{
		int x = Mathf.FloorToInt(Mathf.PosMod(pos.X, Width));
		int y = Mathf.FloorToInt(Mathf.PosMod(pos.Y, Height));
		//int y = Mathf.FloorToInt(Mathf.Clamp(pos.Y, 0, Height - 1));

		return new Tuple<int, int>(x, y);
	}

	bool CheckIfIndexInBounds(int x, int y)
	{
		if (x < 0 || x >= Width)
			return false;
		else if (y < 0 || y >= Height)
			return false;
		else return true;
	}

	//boundary = if on edge of plate; if any of the bordering grid points are empty or belong to dif plates.
	//	or if any of the bordering gridpoints dont contain the center gridpoint's plate
	//		(should this be done in plate init?)
	//is colliding = if the gridpoint or one of the bordering gridpoints belongs to a dif plate
	public void UpdatePoints()
	{
		int width = grid.GetLength(0);
		int height = grid.GetLength(1);

		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				var cell = grid[i, j];
				if (cell.CheckIfEmpty()) continue;
				//if (!cell.isActive) continue;

				cell.isActive = false;
				cell.containsBoundary = false;
				cell.containsCollision = false;

				//check if cell contains points from different plates
				bool collision = false;
				var plate = cell.points[0].plate;
				for (int p = 1; p < cell.points.Count; p++)
				{
					if (cell.points[p].plate != plate)
					{
						collision = true;
						break;
					}
				}
				if (collision)
				{
					cell.containsCollision = true;
					cell.containsBoundary = true;
				}

				//Check in the 8 directions around the gridpoint
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						if (dx == 0 && dy == 0) continue; // skip self
														  //indexes of the bordering gridcell. i,j is the original center, di,dj is one of 8 directions.
						int di = i + dx;
						int dj = j + dy;
						//di = di % width;
						//dj = dj % height;

						if (CheckIfIndexInBounds(di, dj))
						{
							var otherCell = grid[di, dj];

							//points are a boundary if next to empty space
							if (otherCell.CheckIfEmpty())
							{
								cell.containsBoundary = true;
								otherCell.isActive = false;
								continue;
							}
						}
					}
				}

				if (cell.containsBoundary)
				{
					foreach (var p in cell.points)
					{
						p.isBoundary = true;
						p.isActive = true;
					}
						
					cell.isActive = true;
				}
				else
				{
					foreach (var p in cell.points)
						p.isBoundary = false;
				}

				if (cell.containsCollision)
				{
					foreach (var p in cell.points)
					{
						p.isColliding = true;
						p.isActive = true;
					}
					cell.isActive = true;
				}
				else
				{
					foreach (var p in cell.points)
						p.isColliding = false;
				}
				if (!cell.containsCollision && !cell.containsBoundary)
				{
					cell.isActive = false;
					foreach(var p in cell.points)
					{
						p.isActive = false;
					}
				}
			}
		}
	}


	//Only called on initial world creation, so every gridcell is guranteed to only have 1 platepoint in it
	public void InitializeBoundaries()
	{
		int width = grid.GetLength(0);
		int height = grid.GetLength(1);

		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				if (grid[i, j].points.Count == 0) continue;

				var point = grid[i, j].points[0];
				point.isActive = false;
				point.isBoundary = false;
				grid[i, j].isActive = false;

				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						if (dx == 0 && dy == 0) continue; // skip self
						//indexes of the bordering gridcell. i,j is the original center, di,dj is one of 8 directions.
						int di = i + dx;
						int dj = j + dy;
						//di = di % width;
						//dj = dj % height;
						if (CheckIfIndexInBounds(di, dj))
						{
							if (grid[di, dj].points.Count > 0)
							{
								if (grid[di, dj].points[0].plate != point.plate)
								{
									grid[i, j].isActive = true;
									grid[di, dj].isActive = true;
									point.isBoundary = true;
									point.isActive = true;
									grid[di, dj].points[0].isActive = true;
									grid[di, dj].points[0].isBoundary = true;
								}
							}
						}
					}
				}
			}
		}
	}

	public void Collide()
	{
		int width = grid.GetLength(0);
		int height = grid.GetLength(1);
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				
				var cell = grid[i, j];
				if (!cell.containsCollision || cell.points.Count == 0)
					continue;

				var bestPlate = cell.points[0].plate;
				foreach(var p in cell.points)
				{
					if (p.plate.density > bestPlate.density)
						bestPlate = p.plate;
				}

				for (int k = 0; k < cell.points.Count; k++)
				{
					var p = cell.points[k];
					if (p.plate != bestPlate)
					{
						p.plate.points.Remove(p);
						RemovePoint(p);
						p.plate = bestPlate;
						AddPoint(p);
						bestPlate.points.Add(p);
						//bestPlate.AddPointToPlate(p.WorldPos, p.height);
						p.isActive = true;
					}
				}
			}
		}
	}
}
