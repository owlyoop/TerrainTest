using Godot;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Drawing;


//Class used for spatially tracking platepoints for collision detecting
public partial class WorldGrid
{
	//todo: look into multi-threading. parallel.for and parallel.foreach
	public GridCell[,] grid;
	int Width;
	int Height;
	WorldMap map;

	public int GridcellConsolidateThreshold = 2;

	public WorldGrid(int Width, int Height, WorldMap map)
	{
		this.Width = Width;
		this.Height = Height;
		this.map = map;
		grid = new GridCell[Width, Height];
		for (int x = 0; x < Width; x++)
		{
			for (int y = 0; y < Height; y++)
			{
				grid[x,y] = new GridCell(x, y);
			}
		}
	}

	public void AddPoint(PlatePoint point)
	{
		var idx = GetIndexFromPosition(point.WorldPos);
		point.gridIndex = idx;
		grid[point.gridIndex.X, point.gridIndex.Y].AddPoint(point);
	}

	/// <summary>
	/// Returns true if theres atleast [num] neighbours of [plate] bordering gridcell idx
	/// </summary>
	/// <param name="idx"></param>
	/// <param name="plate"></param>
	/// <param name="num"></param>
	/// <returns></returns>
	public bool CheckIfHasNeighbours(Vector2I idx, Plate2D plate, int num)
	{
		int count = 0;
		ForEachNeighbor(idx.X, idx.Y, (di, dj, otherCell) =>
		{
			foreach (var p in grid[di, dj].points)
			{
				if (p.plate == plate)
				{
					count++;
					continue;
				}
			}
		});

		if (count >= num)
			return true;
		else return false;
	}

	public void RemovePoint(PlatePoint point)
	{
		var idx = point.gridIndex;
		grid[idx.X, idx.Y].RemovePoint(point);
			
	}

	public void MovePoint(PlatePoint point, Vector2 newWorldPos)
	{
		var newIdx = GetIndexFromPosition(newWorldPos);
		if (point.gridIndex != newIdx)
		{
			grid[point.gridIndex.X, point.gridIndex.Y].RemovePoint(point);
			point.gridIndex = newIdx;
			grid[newIdx.X, newIdx.Y].AddPoint(point);
			grid[newIdx.X, newIdx.Y].PlateIDs.Add(point.plate.ID);
		}
	}

	public void MovePoint(PlatePoint point)
	{
		var newIdx = GetIndexFromPosition(point.WorldPos);

		if (point.gridIndex != newIdx)
		{
			grid[point.gridIndex.X, point.gridIndex.Y].RemovePoint(point);
			point.gridIndex = newIdx;
			AddPoint(point);
		}
	}


	public Vector2I GetIndexFromPosition(Vector2 pos)
	{
		return new Vector2I((int)pos.X % Width, (int)pos.Y % Height);
	}

	/* Usage:
	 * ForEachNeighbor(i, j, (di, dj, otherCell) =>
		{
		});
	 */
	public void ForEachNeighbor(int i, int j, Action<int, int, GridCell> action, bool checkSelf = false)
	{
		for (int dx = -1; dx <= 1; dx++)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				if (!checkSelf && dx == 0 && dy == 0) continue;
				int di = i + dx;
				int dj = j + dy;

				if (CheckIfIndexInBounds(di, dj))
				{
					action(di, dj, grid[di, dj]);
				}
			}
		}
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
	public void UpdatePointCategories()
	{
		int width = grid.GetLength(0);
		int height = grid.GetLength(1);

		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				var cell = grid[i, j];

				//remove if point runs out of material
				for (int p = cell.points.Count - 1; p >= 0; p--)
				{
					if (cell.points[p].Felsic < 0.01f && cell.points[p].Mafic < 0.01f)
					{
						cell.points.RemoveAt(p);
						if (cell.points.Count == 0)
						{
							ForEachNeighbor(i, j, (di, dj, otherCell) =>
							{
								otherCell.MarkAllAsEdgeBoundary(true);
								otherCell.MarkAllAsBoundary(true);
							});
						}
						
					}
				}
				

				cell.collisionType = PlateCollisionType.None;
				if (!cell.IsEmptyOrInactive())
				{
					if (cell.ContainsCollision || cell.ContainsBorderingOtherPlate)
						PlateCollision.RegisterCollisions(cell, map);
				}

				//cell.MarkAllAsColliding(false);
				//cell.MarkAllAsBoundary(false);
				//cell.MarkAllAsEdgeBoundary(false);
				//cell.MarkAllAsBorderingOtherPlate(false);
				//Mark empty cells as not containing a boundary or collision, and then check its neighbours
				if (cell.IsCompletelyEmpty())
				{
					ForEachNeighbor(i, j, (di, dj, otherCell) =>
					{
						if (!otherCell.IsCompletelyEmpty())
						{
							otherCell.MarkAllAsBoundary(true);
							otherCell.MarkAllAsEdgeBoundary(true);
						}
					});
				}

				if (!cell.IsEmptyOrInactive())
				{
					bool collision = false;
					//check if cell contains points from different plates, if so then it's a collision
					if (cell.points.Count > 1 && cell.PlateIDs.Count > 1)
					{
						collision = true;
					}
					cell.MarkAllAsColliding(collision);

					bool boundary = false;
					bool otherplate = false;
					//cell.MarkAllAsBoundary(false);
					//cell.MarkAllAsEdgeBoundary(false);
					//cell.MarkAllAsBorderingOtherPlate(false);

					//Check in the 8 directions around the gridpoint
					ForEachNeighbor(i, j, (di, dj, otherCell) =>
					{
						if (!boundary)
						{
							if (otherCell.IsCompletelyEmpty())
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
					});
					cell.MarkAllAsBoundary(boundary);
					cell.MarkAllAsEdgeBoundary(boundary);
					cell.MarkAllAsBorderingOtherPlate(otherplate);
				}
			}
		}
	}

	void ErodeMaterial(GridCell cell)
	{

	}

	//test function
	public void CollideWithForces()
	{
		int width = grid.GetLength(0);
		int height = grid.GetLength(1);

		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				var cell = grid[i, j];
				cell.UpdateOpposingForceSums();
				if (!cell.ContainsCollision && !cell.ContainsBorderingOtherPlate)
					continue;
				
				//calculate forces being applied to this from bordering gridcells
				Vector2 totalForce = Vector2.Zero;
				float count = 0.001f;

				ForEachNeighbor(i, j, (di, dj, otherCell) =>
				{
					foreach (var o in otherCell.OpposingForceSums)
					{
						totalForce += o.Value;
						count = count + 1;
					}
				}, true);
				
				totalForce =  totalForce / count;

				foreach (var p in cell.points)
				{
					p.Velocity = p.Velocity * 0.98f;
					//TODO: apply force properly. this is arbituary for testing
					float speed = p.Velocity.Length();
					float ospeed = totalForce.Length();
					p.Velocity = p.Velocity.Normalized().Lerp(totalForce.Normalized(), 0.2f);
					if (ospeed <= 0f)
						p.Velocity *= speed;
					else
						p.Velocity *= Mathf.Clamp(ospeed / (float)count, 0.01f, 10f);
					//p.Velocity *= speed;
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

				ForEachNeighbor(i, j, (di, dj, otherCell) =>
				{
					if (grid[di, dj].points.Count > 0)
					{
						if (grid[di, dj].points[0].plate != point.plate)
						{
							grid[i, j].MarkAllAsBoundary(true);
							grid[di, dj].MarkAllAsBoundary(true);
						}
					}
				});
			}
		}
	}
}
