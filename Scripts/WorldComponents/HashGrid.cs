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
		
		if (CheckIfIndexInBounds(tuple) == true)
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

	bool CheckIfIndexInBounds(Tuple<int,int> index)
	{
		if (index.Item1 < 0 || index.Item1 >= Width)
			return false;
		else if (index.Item2 < 0 || index.Item2 >= Height)
			return false;
		else return true;
	}

	void UpdateBoundaries()
	{
		for (int i = 0; i < grid.GetLength(0); i++)
		{
			for (int j = 0; j < grid.GetLength(1); j++)
			{
				if (grid[i + 1,j].Count != 1)
				{

				}
			}
		}
	}
}
