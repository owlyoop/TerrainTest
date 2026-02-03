using Godot;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;

//Class used for the Hashgrid
public class GridCell
{

	//TODO: might be better to have a 2D array, one row for each plate (plate.ID = index)
	public List<PlatePoint> points;

	public Dictionary<int, Vector2> OpposingForceSums = new Dictionary<int, Vector2>();

	public bool ContainsBoundary { get; private set; }	//If any point in this gridcell is a plate boundary
	public bool ContainsCollision { get; private set; }  //If any point in this gridcell is a plate collision
	public bool ContainsEdgeBoundary { get; private set; }

	public bool ContainsBorderingOtherPlate { get; private set; }
	public bool HasCollisionChecked { get; private set; }   //Used for the update functions that check for collisions/boundaries to avoid repeated checks.

	public GridCell()
	{
		points = new List<PlatePoint>();
		ContainsBoundary = false;
		ContainsCollision = false;
		ContainsEdgeBoundary = false;
		ContainsBorderingOtherPlate = false;

		HasCollisionChecked = false;

	}

	public void UpdateOpposingForceSums()
	{
		OpposingForceSums.Clear();
		foreach (var p in points)
		{
			//if (!p.isActive) continue;
			//if (!p.IsColliding && !p.IsBorderingOtherPlate) continue;
			int id = p.plate.ID;
			if (!OpposingForceSums.ContainsKey(id))
				OpposingForceSums[id] = Vector2.Zero;
			OpposingForceSums[id] += p.Velocity;
		}
	}

	public bool IsEmptyOrInactive()
	{
		if (points.Count == 0)
			return true;

		foreach(var p in points)
		{
			if (p.isActive)
				return false;
		}
		return true;
	}

	public bool IsCompletelyEmpty()
	{
		if (points.Count == 0)
			return true;
		else return false;
	}

	#region bookkeeping
	
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
		this.ContainsEdgeBoundary = false;
		this.ContainsBorderingOtherPlate = false;
		foreach(var p in points)
		{
			p.MarkPointAsColliding(false);
			p.MarkPointAsBoundary(false);
			p.MarkPointAsEdgeBoundary(false);
			p.MarkPointAsBorderingOtherPlate(false);
			p.isActive = false;
		}
	}

	public void MarkAllAsColliding(bool choice)
	{
		this.ContainsCollision = choice;
		foreach (var p in this.points)
		{
			p.MarkPointAsColliding(choice);
			if (choice == true)
				p.isActive = true;
		}
	}

	public void MarkAllAsBoundary(bool choice)
	{
		this.ContainsBoundary = choice;
		foreach (var p in this.points)
		{
			p.MarkPointAsBoundary(choice);
			if (choice == true)
				p.isActive = true;
		}
	}
	public void MarkAllAsEdgeBoundary(bool choice)
	{
		this.ContainsEdgeBoundary = choice;
		foreach (var p in this.points)
		{
			p.MarkPointAsEdgeBoundary(choice);
			if (choice == true)
				p.isActive = true;
		}
	}

	public void MarkAllAsBorderingOtherPlate(bool choice)
	{
		this.ContainsBorderingOtherPlate = choice;
		foreach(var p in points)
		{
			p.MarkPointAsBorderingOtherPlate(choice);
			if (choice == true)
				p.isActive = true;
		}
	}

	#endregion

	public int GetNumberOfSamePlate(PlatePoint point)
	{
		int result = 0;
		foreach(var p in this.points)
		{
			if (p.plate == point.plate && p.isActive)
				result++;
		}
		return result;
	}

	public void Consolidate(PlatePoint point)
	{
		var plate = point.plate;
		var newpos = Vector2.Zero;
		int count = 0;
		float h = 0;
		for (int i = points.Count - 1; i >= 0; i--)
		{
			if (points[i].plate == plate)
			{
				newpos += points[i].WorldPos;
				count++;
				h += points[i].height;
				plate.points.Remove(points[i]);
				points.Remove(points[i]);
			}
		}
		newpos /= count;
		h /= count;

		var p = new PlatePoint(plate.WorldToLocal(newpos), h, plate);
		p.gridIndex = point.gridIndex;
		points.Add(p);
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

		if (CheckIfHasNeighbours(idx, point.plate, 2))
		{
			if (grid[idx.X, idx.Y].GetNumberOfSamePlate(point) < threshhold && grid[idx.X, idx.Y].IsEmptyOrInactive())
			{
				grid[idx.X, idx.Y].AddPoint(point);
				return true;
			}
			else
			{
				grid[idx.X, idx.Y].Consolidate(point);
				return true;
			}
		}
		else return false;
			
	}

	//Returns true if theres atleast [num] neighbours of [plate] bordering gridcell idx
	public bool CheckIfHasNeighbours(Vector2I idx, Plate2D plate, int num)
	{
		int count = 0;
		for (int dx = -1; dx <= 1; dx++)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				if (dx == 0 && dy == 0) continue;
				int di = idx.X + dx;
				int dj = idx.Y + dy;
				if (CheckIfIndexInBounds(di, dj))
				{
					foreach(var p in grid[di,dj].points)
					{
						if (p.isActive && p.plate == plate)
						{
							count++;
							continue;
						}
					}	
				}
			}
		}

		if (count >= num)
			return true;
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
				cell.MarkAllAsColliding(false);
				cell.MarkAllAsBoundary(false);
				cell.MarkAllAsEdgeBoundary(false);
				cell.MarkAllAsBorderingOtherPlate(false);
				//Mark empty cells as not containing a boundary or collision, and then check its neighbours
				if (cell.IsCompletelyEmpty())
				{
					
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
									otherCell.MarkAllAsBoundary(true);
									otherCell.MarkAllAsEdgeBoundary(true);
								}
							}
						}
					}
				}


				if (!cell.IsEmptyOrInactive())
				{
					bool collision = false;
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
						cell.MarkAllAsColliding(true);
					}
					else
					{
						cell.MarkAllAsColliding(false);
					}

					bool boundary = false;
					bool otherplate = false;
					cell.MarkAllAsBoundary(false);
					cell.MarkAllAsEdgeBoundary(false);
					cell.MarkAllAsBorderingOtherPlate(false);
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
								if (otherCell.IsCompletelyEmpty() && !boundary)
								{
									boundary = true;
								}

								if (!otherplate)
								{
									foreach (var op in otherCell.points)
									{
										if (otherplate) continue;
										foreach (var p in cell.points)
										{
											if (otherplate) continue;
											if (op.plate != p.plate && op.isActive)
											{
												otherplate = true;
												continue;
											}
										}
									}
								}
							}

						}
					}
					cell.MarkAllAsBoundary(boundary);
					cell.MarkAllAsEdgeBoundary(boundary);
					cell.MarkAllAsBorderingOtherPlate(otherplate);
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

				if (cell.points.Count <= 1)
					continue;
				//if (!cell.ContainsCollision && !cell.ContainsBorderingOtherPlate)
				//	continue;

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
						/*p.height = p.height - 0.01f;
						h += 0.001f;
						if (p.height < -1f)
						{
							p.plate.points.Remove(p);
							RemovePoint(p);
						}*/
						p.plate.points.Remove(p);
						RemovePoint(p);
					}
					else
					{
						p.height += 0.001f;
					}
				}
			}
		}
	}


	public void CollideWithForces()
	{
		int width = grid.GetLength(0);
		int height = grid.GetLength(1);

		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				var cell = grid[i, j];
				//if (!cell.ContainsCollision && !cell.ContainsBorderingOtherPlate)
				//	continue;

				cell.UpdateOpposingForceSums();
			}
		}

		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				var cell = grid[i, j];
				//if (!cell.ContainsCollision && !cell.ContainsBorderingOtherPlate)
				//	continue;

				//calculate forces being applied to this from bordering gridcells
				foreach (var p in cell.points)
				{
					Vector2 totalForce = Vector2.Zero;
					int count = 0;
					for (int dx = -1; dx <= 1; dx++)
					{
						for (int dy = -1; dy <= 1; dy++)
						{
							int di = i + dx;
							int dj = j + dy;
							if (CheckIfIndexInBounds(di, dj))
							{
								var othercell = grid[di, dj];
								//if (!othercell.ContainsCollision && othercell.ContainsBorderingOtherPlate) continue;
								foreach(var o in othercell.OpposingForceSums)
								{
									if (o.Key != p.plate.ID)
									{
										totalForce += o.Value;
										count++;
									}
								}
							}
						}
					}

					//TODO: apply force properly. this is arbituary for testing
					float speed = p.Velocity.Length();
					float ospeed = totalForce.Length();
					p.Velocity = p.Velocity.Normalized().Lerp(totalForce.Normalized(), 0.9f);
					if (ospeed <= 0f)
						p.Velocity *= speed;
					else
						p.Velocity *= Mathf.Clamp(ospeed / (float)count, 0.01f, 10f);
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
									grid[i, j].MarkAllAsBoundary(true);
									grid[di, dj].MarkAllAsBoundary(true);
								}
							}
						}
					}
				}
			}
		}
	}
}
