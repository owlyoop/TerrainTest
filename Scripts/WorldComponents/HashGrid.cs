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
		var tuple = GetIndexFromPosition(point.position);
		
		if (CheckIfIndexInBounds(tuple.Item1, tuple.Item2) == true)
		{
			grid[tuple.Item1, tuple.Item2].Add(point);
		}
			
	}

	public void RemovePoint(PlatePoint point)
	{
		var tuple = GetIndexFromPosition(point.position);
		grid[tuple.Item1, tuple.Item2].Remove(point);
	}

	public void MovePoint(PlatePoint point, Vector2 newPos)
	{
		//todo: check if point moved cells
		Vector2 oldPos = point.position;
		//GD.Print(oldPos, newPos);
		RemovePoint(point);
		point.position = newPos;
		AddPoint(point);
	}

	public Tuple<int, int> GetIndexFromPosition(Vector2 pos)
	{
		//todo: make this properly get the correct image cell index and also handle image wrapping
		int x = (int)Math.Round(pos.X);
		int y = (int)Math.Round(pos.Y);
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
				if (grid[i, j].Count == 0) continue;

				//Check for internal collisions (2 dif plates with points in same gridcell
				
				bool collision = false;
				bool boundary = false;
				if (grid[i, j].Count > 1)
				{
					//if theres more than 1 point and they have dif plates, its a collision
					var plate = grid[i, j][0].plate;
					for (int p = 0; p < grid[i, j].Count; p++)
					{
						if (grid[i, j][p].plate != plate)
						{
							collision = true;
							break;
						}
					}
				}
				if (collision)
					foreach (var p in grid[i, j])
						p.isColliding = true;

				//Check in the 8 directions around the gridpoint
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
							//
							if (grid[di, dj].Count == 0)
							{
								//Base gridcell borders an empty cell, so it's a boundary
								boundary = true;
							}
							else if (collision) //if theres a collision in the center gridpoint then the 8 surrounding ones should be marked?
							{
								foreach(var point in grid[di,dj])
								{
									point.isColliding = true;
								}

							}
							else //the center gridpoint should only have 1 unique plate by here. 
							{
								foreach(var otherPoint in grid[di,dj])
								{
									if (otherPoint.plate != grid[di, dj][0].plate)
									{
										boundary = true;
										break;
									}
								}
							}

							if (boundary) //set center gridcell and bordering gridcell as boundary points
							{
								foreach(var point in grid[di,dj])
								{
									point.isBoundary = true;
								}
								foreach(var point in grid[i,j])
								{
									point.isBoundary = true;
								}
							}
						}
					}
				}
			}
		}
	}
}
