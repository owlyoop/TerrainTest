using Godot;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

/// <summary>
/// Struct used to update Gridcell information for multi-threading
/// </summary>
public struct CellUpdateResult
{
	public bool IsBoundary;
	public bool IsCollision;
	public bool IsEdgeBoundary;
	public bool IsBorderingOtherPlate;
	public bool IsActive;
	public bool RegisterCollisions;
	public List<int> DepletedPointIndices;

}

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

	public void UpdatePointCategoriesParallel()
	{
		int width = grid.GetLength(0);
		int height = grid.GetLength(1);
		var results = new CellUpdateResult[width, height];

		//todo: flatten the array instead of this(?) i dunno
		Parallel.For(0, width, i =>
		{
			for (int j = 0; j < height; j++)
			{
				results[i, j] = ClassifyCellParallel(i, j);
			}
		});


		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				//todo: look over , see what i can move out of here to be done in parallel
				ApplyCellResults(i, j, results[i, j]);
			}
		}

		
		
	}

	CellUpdateResult ClassifyCellParallel(int i, int j)
	{
		var cell = grid[i,j];
		var result = new CellUpdateResult();

		//id depleted pts by index
		for (int p = cell.points.Count - 1; p >= 0; p--)
		{
			if (cell.points[p].Felsic <= 0.01f && cell.points[p].Mafic <= 0.01f)
			{
				if (result.DepletedPointIndices == null)
					result.DepletedPointIndices = new List<int>();
				result.DepletedPointIndices.Add(p);
			}
		}

		
		if (!cell.IsEmptyOrInactive())
		{
			//flag whether to register collision
			if (cell.ContainsCollision || cell.ContainsBorderingOtherPlate)
				result.RegisterCollisions = true;

			//flag colliding
			if (cell.points.Count > 1 && cell.PlateIDs.Count > 1)
				result.IsCollision = true;

			//classify boundary/edgebound/borderingother
			bool boundary = false;
			bool otherplate = false;

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
			}, checkSelf: false);

			if (!boundary && !otherplate && !result.IsCollision)
				result.IsActive = false;
			else result.IsActive = true;

				result.IsBoundary = boundary;
			result.IsEdgeBoundary = boundary;
			result.IsBorderingOtherPlate = otherplate;
		}
		return result;
	}

	void ApplyCellResults(int i, int j, CellUpdateResult result)
	{
		var cell = grid[i, j];

		//Remove points based off of gathered indices from cellupdateresult
		if (result.DepletedPointIndices != null)
		{
			//depletedindices already added in reverse order so we can traverse it normally
			for (int p = 0; p < result.DepletedPointIndices.Count; p++)
			{
				cell.points.RemoveAt(result.DepletedPointIndices[p]);
			}
			if (cell.points.Count == 0)
			{
				ForEachNeighbor(i, j, (di, dj, otherCell) =>
				{
					otherCell.MarkAllAsBoundary(true);
					otherCell.MarkAllAsEdgeBoundary(true);
				}, checkSelf: false);
			}
		}


		if (!result.IsActive)
			cell.MarkAsEmpty();

		//collision register
		//cell.collisionType = PlateCollisionType.None;
		if (!cell.IsEmptyOrInactive())
		{
			cell.MarkAllAsColliding(result.IsCollision);
			cell.MarkAllAsBoundary(result.IsBoundary);
			cell.MarkAllAsEdgeBoundary(result.IsEdgeBoundary);
			cell.MarkAllAsBorderingOtherPlate(result.IsBorderingOtherPlate);
			if (result.RegisterCollisions)
			{
				//PlateCollision.RegisterCollisions(cell, map);
			}
				
		}

		//mark empty cell neighbours as boundaries
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
	}

	public void UpdateForces()
	{
		int width = grid.GetLength(0);
		int height = grid.GetLength(1);

		Parallel.For(0, width, i =>
		{
			for (int j = 0; j < height; j++)
			{
				var cell = grid[i, j];

				//if (!cell.ContainsCollision || cell.PlateIDs.Count < 2)
				//	continue;

				foreach (var p in cell.points)
				{
					if (!p.isActive) continue;
					var totalForce = Vector2.Zero;

					ForEachNeighbor(i, j, (di, dj, otherCell) =>
					{
						foreach (var op in otherCell.points)
						{
							if (op.plate.ID == p.plate.ID) continue;

							Vector2 dirAway = (p.WorldPos - op.WorldPos).Normalized();

							float repulsionStr = (op.mass) * 1f;
							totalForce += dirAway * repulsionStr;
						}

					}, checkSelf: true);

					var force = totalForce;

					var r = p.WorldPos - p.plate.Center;
					var tau = (r.X * force.Y - r.Y * force.X);

					p.plate.sumTorque += tau * 1f;// * totalForce.LengthSquared();
					p.plate.sumForce += (force);
					p.plate.numForcePts++;
				}
			}
		});
	}

	public void Erosion()
	{
		int width = grid.GetLength(0);
		int height = grid.GetLength(1);


		//for (int i = 0; i < width; i++)
		Parallel.For(0, width, i => 
		{
			for (int j = 0; j < height; j++)
			{
				var cell = grid[i, j];

				if (cell.points.Count == 0) continue;
				//if (cell.IsEmptyOrInactive()) continue;

				foreach (var p in cell.points)
				{
					var neighbors = new List<(PlatePoint otherPoint, float dif)>();
					//if (!p.isActive) continue;
					//float fmax = p.Felsic * 0.2f;
					//float mmax = p.Mafic * 0.2f;
					float totaldifs = 0f;

					ForEachNeighbor(i, j, (di, dj, otherCell) =>
					{
						float dif = float.MaxValue;

						foreach (var o in otherCell.points)
						{
							dif = p.height - o.height;
							
							if (dif > 0f && dif < float.MaxValue)
							{
								neighbors.Add((o, dif));
								totaldifs += dif;
							}
						}
					}, false);


					neighbors.Sort((a, b) => a.dif.CompareTo(b.dif));

					int iter = 1;
					foreach (var (o, dif) in neighbors)
					{
						if (p != o && dif > 1f)
						{
							//float f = (dif / totaldifs) * fmax;
							//float m = (dif / totaldifs) * mmax;

							float f = p.Felsic * 0.005f * (1 + dif);
							float m = p.Mafic * 0.005f * (1 + dif);
							p.GiveMaterial(o, f, m);
							//p.CheckIfDestroySelf();
							iter++;
						}
					}
				}
			}
		});
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
