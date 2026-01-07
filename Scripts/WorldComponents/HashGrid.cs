using Godot;
using System;
using System.Collections.Generic;

public partial class HashGrid
{

	public List<PlatePoint>[,] grid;

	int Width;
	int Height;

	public HashGrid(int Width, int Height)
	{
		this.Width = Width;
		this.Height = Height;
		grid = new List<PlatePoint>[Width, Height];
		for (int x = 0; x < Width; x++)
		{
			for (int y = 0; y < Height; y++)
			{
				grid[x,y] = new List<PlatePoint>();
			}
		}
	}

	public void AddPoint(PlatePoint point)
	{
		var idx = GetIndexFromPosition(point.worldPos);

		point.gridIndex = new Vector2I(idx.Item1, idx.Item2);
		grid[idx.Item1, idx.Item2].Add(point);
	}

	public void RemovePoint(PlatePoint point)
	{
		var idx = point.gridIndex;

		if (CheckIfIndexInBounds(idx.X, idx.Y))
			grid[idx.X, idx.Y].Remove(point);
	}

	public void MovePoint(PlatePoint point, Vector2 newWorldPos)
	{
		//newWorldPos.X = Mathf.PosMod(newWorldPos.X, Width);
		//newWorldPos.Y = Mathf.Clamp(newWorldPos.Y, 0, Height - 1);

		var oldIdx = point.gridIndex;
		var newIdxTuple = GetIndexFromPosition(newWorldPos);
		var newIdx = new Vector2I(newIdxTuple.Item1, newIdxTuple.Item2);
		point.worldPos = newWorldPos;
		if (oldIdx != newIdx)
		{

			RemovePoint(point);
			point.gridIndex = newIdx;
			AddPoint(point);
			//grid[oldIdx.X, oldIdx.Y].Remove(point);
			//grid[newIdx.X, newIdx.Y].Add(point);
		}

		
	}

	public Tuple<int, int> GetIndexFromPosition(Vector2 pos)
	{
		int x = Mathf.FloorToInt(Mathf.PosMod(pos.X, Width));
		int y = Mathf.FloorToInt(Mathf.Clamp(pos.Y, 0, Height - 1));

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

	void GetNeighbours()
	{

	}

	//boundary = if on edge of plate; if any of the bordering grid points are empty or belong to dif plates.
	//	or if any of the bordering gridpoints dont contain the center gridpoint's plate
	//		(should this be done in plate init?)
	//is colliding = if the gridpoint or one of the bordering gridpoints belongs to a dif plate
	public void UpdatePoints()
	{
		int width = grid.GetLength(0);
		int height = grid.GetLength(1);
		bool collision = false;
		bool boundary = false;
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				collision = false;
				boundary = false;
				if (grid[i, j].Count == 0) continue;
				foreach (var p in grid[i, j])
				{
					p.isColliding = false;
					p.isBoundary = false;
				}
					
				//Check for internal collisions (2 dif plates with points in same gridcell

				if (grid[i, j].Count > 1)
				{
					//if theres more than 1 point and they have dif plates, its a collision
					var plate = grid[i, j][0].plate;
					for (int p = 0; p < grid[i, j].Count; p++)
					{
						if (grid[i, j][p].plate != plate)
						{
							collision = true;
							boundary = true;
							break;
						}
					}
				}
				if (collision)
					foreach (var p in grid[i, j])
						p.isColliding = true;
				if (boundary)
					foreach (var p in grid[i, j])
						p.isBoundary = true;

				//Check in the 8 directions around the gridpoint
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						

						//indexes of the bordering gridcell. i,j is the original center, di,dj is one of 8 directions.
						int di = i + dx;
						int dj = j + dy;
						if (di == 0 && dj == 0) continue; // skip self

						if (CheckIfIndexInBounds(di, dj))
						{
							if (grid[di,dj].Count == 0)
							{
								boundary = true;
								foreach (var p in grid[i, j])
									p.isBoundary = true;
							}

							if (collision) //if theres a collision in the center gridpoint then the 8 surrounding ones should be marked?
							{
								foreach (var point in grid[di, dj])
								{
									point.isColliding = true;
								}
							}
							else //the center gridpoint should only have 1 unique plate by here. 
							{
								foreach (var otherPoint in grid[di, dj])
								{
									collision = false;
									otherPoint.isColliding = false;
									if (otherPoint.plate != grid[i, j][0].plate)
									{
										collision = true;
										otherPoint.isColliding = true;
									}
								}
							}
							
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
				if (grid[i, j].Count == 0) continue;

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
							if (grid[di, dj].Count > 0)
							{
								if (grid[di, dj][0].plate != grid[i, j][0].plate)
								{
									grid[i, j][0].isBoundary = true;
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
				if (grid[i, j].Count > 0)
				{
					var bestplate = grid[i, j][0].plate;
					bool hasCollision = false;
					foreach (var p in grid[i,j])
					{
						if (p.isColliding)
						{
							hasCollision = true;
							if (p.plate.density > bestplate.density)
								bestplate = p.plate;
						}
					}

					if (hasCollision)
					{
						foreach (var p in grid[i, j])
						{
							p.plate.points.Remove(p);
							p.plate = bestplate;
							p.plate.points.Add(p);
						}
					}



					/*if (grid[i, j][0].isColliding)
					{
						var bestplate = grid[i, j][0].plate;
						foreach (var p in grid[i, j])
						{
							if (p.plate.density > bestplate.density)
								bestplate = p.plate;
						}
						foreach (var p in grid[i, j])
						{

						}
					}*/
				}
			}
		}
	}
}
