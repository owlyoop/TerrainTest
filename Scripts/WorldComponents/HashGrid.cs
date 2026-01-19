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

	public bool IsEmpty() => points.Count == 0;

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
			p.isColliding = false;
			p.isBoundary = false;
			p.isActive = false;
		}
	}

	public void MarkAsColliding(bool choice)
	{
		this.ContainsCollision = choice;
		foreach (var p in this.points)
		{
			p.isColliding = choice;
		}
			
	}

	public void MarkAsBoundary(bool choice)
	{
		this.ContainsBoundary = choice;
		foreach (var p in this.points)
		{
			p.isBoundary = choice;
		}
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

	public void AddPoint(PlatePoint point)
	{
		var idx = GetIndexFromPosition(point.WorldPos);
		point.gridIndex = idx;
		grid[point.gridIndex.X, point.gridIndex.Y].AddPoint(point);
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
				if (cell.IsEmpty())
				{
					cell.MarkAsColliding(false);
					cell.MarkAsBoundary(false);
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
								if (!otherCell.IsEmpty())
								{
									otherCell.MarkAsBoundary(true);
								}
									
							}
						}
					}
				}
				
				foreach(var p in cell.points)
				{
					if (p.height > 0.9f)
						p.height -= 0.001f;
				}

				//if (cell.isActive == false) continue;
				//if (cell.containsBoundary == false && cell.containsCollision == false) continue;

				if (!cell.IsEmpty())
				{
					cell.MarkAsEmpty();

					//check if cell contains points from different plates
					
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
						cell.MarkAsColliding(true);
					}
					else
					{
						//cell.ContainsCollision = false;
						//cell.containsBoundary = false;
						cell.MarkAsColliding(false);
					}
				}
				

				//check bordering cells
				if (!cell.IsEmpty())
				{
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
								if (otherCell.IsEmpty())
								{
									cell.MarkAsBoundary(true);
									continue;
								}

								if (!collision)
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
										cell.MarkAsBoundary(true);
										otherCell.MarkAsBoundary(true);
									}
								}
							}
						}
					}
				}

				//final point categorization
				/*if (cell.ContainsBoundary)
				{
					foreach (var p in cell.points)
					{
						p.isBoundary = true;
						p.isActive = true;
					}
				}
				else
				{
					foreach (var p in cell.points)
						p.isBoundary = false;
				}

				if (cell.ContainsCollision)
				{
					foreach (var p in cell.points)
					{
						p.isColliding = true;
						p.isActive = true;
					}
				}
				else
				{
					foreach (var p in cell.points)
						p.isColliding = false;
				}

				if (!cell.ContainsCollision && !cell.ContainsBoundary)
				{
					foreach (var p in cell.points)
					{
						p.isActive = false;
					}
				}*/
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
				point.isBoundary = false;

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
									grid[i, j].MarkAsBoundary(true);
									grid[di, dj].MarkAsBoundary(true);
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

				//consolidate. doesnt work good at all :(
				/*if (cell.points.Count >= 7)
				{
					var plate = cell.points[0].plate;
					int iter = 0;
					float avgH = 0;
					foreach (var p in cell.points)
					{
						avgH += p.height;
						iter++;
						p.plate.points.Remove(p);
						
					}
					cell.points.Clear();

					var newp = plate.AddPointToPlate(new Vector2(i,j), avgH / iter);
					var newp2 = plate.AddPointToPlate(new Vector2(i - 0.2f, j - 0.2f), avgH / iter);
					AddPoint(newp); AddPoint(newp2);
				}*/

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
						//Vector2 w0 = p.WorldPos;
						p.height -= 0.01f;
						if (p.height < -0.5f)
						{
							p.plate.points.Remove(p);
							RemovePoint(p);
						}

						//p.plate.points.Remove(p);
						//RemovePoint(p);
						if (p.height > 0f)
							h += p.height * 0.01f;
						else h += p.height * 0.005f;

						//AddPoint(p);
						//bestPlate.points.Add(p);
						//bestPlate.AddExistingPointToPlate(p);


						//bestPlate.AddExistingPointToPlate(p);
						//Vector2 w1 = p.WorldPos;
						//if ((w0.DistanceSquaredTo(w1) < 0.0001f))
						//	GD.PrintErr("Point broke");
					}
					else
					{
						p.height += 0.004f;
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

				for (int dx = -1; dx <= 1; dx++)
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
							othercell.MarkAsBoundary(true);
							for (int p = othercell.points.Count - 1; p >= 0; p--)
							{
								othercell.points[p].plate.UpdatePointInHashGrid(othercell.points[p]);
							}
						}
					}
				}
			}
		}
	}
}
