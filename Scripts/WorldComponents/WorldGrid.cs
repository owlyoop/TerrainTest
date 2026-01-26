using Godot;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;

//Class used for the Hashgrid
public class GridCell
{
	public List<PlatePoint> points;

	public bool ContainsBoundary { get; private set; }	//If any point in this gridcell is a plate boundary
	public bool ContainsCollision { get; private set; }  //If any point in this gridcell is a plate collision
	public bool HasCollisionChecked { get; private set; }   //Used for the update functions that check for collisions/boundaries to avoid repeated checks.

	public GridCell()
	{
		points = new List<PlatePoint>();
		ContainsBoundary = false;
		ContainsCollision = false;
		HasCollisionChecked = false;
	}

	public bool IsEmptyOrInactive()
	{
		if (points.Count == 0)
			return true;

		bool noneActive = true;
		foreach(var p in points)
		{
			if (p.isActive)
			{
				noneActive = false;
				break;
			}
		}

		if (noneActive)
			return true;
		else return false;
	}

	public bool IsCompletelyEmpty()
	{
		if (points.Count == 0)
			return true;
		else return false;
	}

	public void AddPoint(PlatePoint point)
	{
		points.Add(point);
	}

	public void RemovePoint(PlatePoint point)
	{
		points.Remove(point);
	}

	public void MarkAsEmpty()
	{
		this.ContainsCollision = false;
		this.ContainsBoundary = false;
		foreach(var p in points)
		{
			p.MarkPointAsColliding(false);
			p.MarkPointAsBoundary(false);
			p.isActive = false;
		}
	}


	public void MarkAllPointsAsColliding(bool choice)
	{
		this.ContainsCollision = choice;
		foreach (var p in this.points)
		{
			p.MarkPointAsColliding(choice);
			if (choice)
				p.isActive = choice;
		}
	}

	public void MarkAllPointsAsBoundary(bool choice)
	{
		this.ContainsBoundary = choice;
		foreach (var p in this.points)
		{
			p.MarkPointAsBoundary(choice);
			if (choice)
				p.isActive = choice;
		}
	}

	public int GetNumberOfSamePlate(PlatePoint point)
	{
		int result = 0;
		foreach(var p in this.points)
		{
			if (p.plate == point.plate)
				result++;
		}
		return result;
	}
}

//Class used for spatially tracking platepoints for collision detecting
public partial class WorldGrid
{
	public GridCell[,] grid;
	int Width;
	int Height;

	public WorldGrid(int Width, int Height)
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

	public void AddPoint(PlatePoint point)
	{
		var idx = GetIndexFromPosition(point.WorldPos);
		point.gridIndex = idx;
		grid[point.gridIndex.X, point.gridIndex.Y].AddPoint(point);
	}

	public bool TryAddPoint(PlatePoint point, int threshhold)
	{
		var idx = GetIndexFromPosition(point.WorldPos);
		point.gridIndex = idx;
		if (grid[point.gridIndex.X, point.gridIndex.Y].GetNumberOfSamePlate(point) < threshhold)
		{
			grid[point.gridIndex.X, point.gridIndex.Y].AddPoint(point);
			return true;
		}
		else return false;
			
	}

	public void RemovePoint(PlatePoint point)
	{
		var idx = point.gridIndex;
		grid[idx.X, idx.Y].RemovePoint(point);
			
	}

	public void MovePoint(PlatePoint point)
	{
		var oldIdx = GetIndexFromPosition(point.cachedWorldPos);
		//var oldIdx = point.gridIndex; //neither this or cachedworldpos works as a solution atm
		var newIdx = GetIndexFromPosition(point.WorldPos);

		if (oldIdx != newIdx)
		{
			RemovePoint(point);
			if (CheckIfIndexInBounds(oldIdx.X, oldIdx.Y))
				grid[oldIdx.X, oldIdx.Y].RemovePoint(point);
			point.gridIndex = newIdx;
			AddPoint(point);
		}
	}

	public void ReactivatePoint(PlatePoint point, Vector2 cachedPos)
	{
		var cache = cachedPos;
		var oldIdx = GetIndexFromPosition(cachedPos);
		var newIdx = GetIndexFromPosition(point.WorldPos);
		RemovePoint(point);
		if (CheckIfIndexInBounds(oldIdx.X, oldIdx.Y))
			grid[oldIdx.X, oldIdx.Y].RemovePoint(point);
		point.gridIndex = newIdx;
		point.cachedWorldPos = cache;
		AddPoint(point);
	}


	public Vector2I GetIndexFromPosition(Vector2 pos)
	{
		//int x = Mathf.FloorToInt(Mathf.PosMod(pos.X, Width));
		//int y = Mathf.FloorToInt(Mathf.PosMod(pos.Y, Height));

		//int x = (int)Mathf.PosMod(pos.X, Width);
		//int y = (int)Mathf.PosMod(pos.Y, Height);
		//return new Vector2I(x, y);
		return new Vector2I((int)pos.X % Width, (int)pos.Y % Height);
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
				bool boundary = false;
				bool collision = false;
				//Mark empty cells as not containing a boundary or collision, and then check its neighbours
				if (cell.IsCompletelyEmpty())
				{
					cell.MarkAllPointsAsColliding(false);
					cell.MarkAllPointsAsBoundary(false);
					foreach (var p in cell.points)
						p.isActive = false;
					for (int dx = -1; dx <= 1; dx++)
					{
						for (int dy = -1; dy <= 1; dy++)
						{
							//if (dx == 0 && dy == 0) continue;
							int di = i + dx;
							int dj = j + dy;

							if (CheckIfIndexInBounds(di, dj))
							{
								var otherCell = grid[di, dj];
								if (!otherCell.IsEmptyOrInactive())
								{
									otherCell.MarkAllPointsAsBoundary(true);
								}
								else
								{
									foreach (var p in otherCell.points)
										p.isActive = false;
								}
									
							}
						}
					}
				}

				//if (cell.isActive == false) continue;
				//if (cell.ContainsBoundary == false && cell.ContainsCollision == false) continue;

				if (!cell.IsEmptyOrInactive())
				{
					//cell.MarkAsEmpty();

					//check if cell contains points from different plates, if so then it's a collision
					var plate = cell.points[0].plate.ID;
					for (int p = 0; p < cell.points.Count; p++)
					{
						if (cell.points[p].plate.ID != plate)
						{
							collision = true;
							break;
						}
					}
					if (collision)
					{
						cell.MarkAllPointsAsColliding(true);
					}
					else
					{
						cell.MarkAllPointsAsColliding(false);
					}

					//Check in the 8 directions around the gridpoint
					for (int dx = -1; dx <= 1; dx++)
					{
						for (int dy = -1; dy <= 1; dy++)
						{
							if (dx == 0 && dy == 0) continue;
							int di = i + dx;
							int dj = j + dy;
							
							if (CheckIfIndexInBounds(di, dj))
							{
								var otherCell = grid[di, dj];
								if (otherCell.IsCompletelyEmpty())
								{
									cell.MarkAllPointsAsBoundary(true);
									//continue;
									boundary = true;
								}

								/*if (!collision)
								{
									bool otherplate = false;
									foreach (var op in otherCell.points)
									{
										if (otherplate) continue;
										foreach (var p in cell.points)
										{
											if (otherplate) continue;
											if (op.plate != p.plate)
											{
												otherplate = true;
												continue;
											}
										}
									}

									if (otherplate)
									{
										cell.MarkAllPointsAsBoundary(true);
										otherCell.MarkAllPointsAsBoundary(true);
									}
								}*/
							}
							if (!boundary)
								cell.MarkAllPointsAsBoundary(false);
						}
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
				if (grid[i, j].points.Count != 1) continue;

				var point = grid[i, j].points[0];
				point.isActive = false;
				point.MarkPointAsBoundary(false);

				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						if (dx == 0 && dy == 0) continue; // skip self
						//indexes of the bordering gridcell. i,j is the original center, di,dj is one of 8 directions.
						int di = i + dx;
						int dj = j + dy;
						if (CheckIfIndexInBounds(di, dj))
						{
							if (grid[di, dj].points.Count > 0)
							{
								if (grid[di, dj].points[0].plate != point.plate)
								{
									grid[i, j].MarkAllPointsAsBoundary(true);
									grid[di, dj].MarkAllPointsAsBoundary(true);
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

				if (!cell.ContainsCollision || cell.points.Count < 1)
					continue;

				var bestPlate = cell.points[0].plate;
				foreach (var p in cell.points)
				{
					if (p.plate.density > bestPlate.density)
						bestPlate = p.plate;
				}

				var h = 0f;
				for (int k = 0; k < cell.points.Count; k++)
				{
					var p = cell.points[k];
					if (p.plate.ID != bestPlate.ID)
					{
						p.height = p.height - 0.05f;
						h += 0.001f;
						if (p.height < -1f)
						{
							p.plate.points.Remove(p);
							RemovePoint(p);
						}
						
					}
					else
					{
						p.height += 0.001f;
					}
				}

				for (int k = 0; k < cell.points.Count; k++)
				{
					var p = cell.points[k];
					if (p.plate.ID == bestPlate.ID)
					{
						p.height += h;
					}
				}

				//mark bordering cells of collision as new boundaries
				/*for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						if (dx == 0 && dy == 0)
						{
							continue;
						}
						int di = i + dx;
						int dj = j + dy;
						if (CheckIfIndexInBounds(di, dj))
						{
							var othercell = grid[di, dj];
							othercell.MarkAllPointsAsBoundary(true);
							for (int p = othercell.points.Count - 1; p >= 0; p--)
							{
								othercell.points[p].plate.UpdatePointInHashGrid(othercell.points[p]);
							}
						}
					}
				}*/
			}
		}
	}
}
