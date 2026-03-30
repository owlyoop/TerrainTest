using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


public enum PlateCollisionType
{
	None,
	Orogenic,       //continental vs continental. mountain building
	Subduction,     //oceanic vs continental OR oceanic vs oceanic.
					//	oceanic subducts under continental or less dense oceanic plate
	Divergent,      //moving away from eachother.
	Transform       //any 2 plates moving past eachother
}

public struct CollisionInfo
{
	public Vector2 BoundaryNormal;
	public float BoundaryDot;
	public PlateCollisionType Type;
}
public static class PlateCollision
{
	static WorldMap map;
	static WorldGrid grid;
	public static void RegisterCollisions(GridCell cell, WorldMap _map)
	{
		map = _map;
		grid = map.worldGrid;

		if (cell.PlateIDs.Count < 2)
			return;

		var plates = new HashSet<int>(cell.PlateIDs);
		List<PlatePoint> otherNeighbors = new List<PlatePoint>();

		grid.ForEachNeighbor(cell.x, cell.y, (di, dj, otherCell) =>
		{
			if (otherCell.ContainsCollision || otherCell.ContainsBorderingOtherPlate)
			{
				plates.UnionWith(otherCell.PlateIDs);
			}
				
		});

		if (plates.Count < 2)
			return;

		var collisionCache = new Dictionary<(int plateID, int otherID), CollisionInfo>();
		var receivers = new Dictionary<int, List<PlatePoint>>(plates.Count);
		foreach (var id in plates)
			receivers[id] = new List<PlatePoint>(16);

		grid.ForEachNeighbor(cell.x, cell.y, (di, dj, otherCell) =>
		{
			if (otherCell.ContainsCollision || otherCell.ContainsBorderingOtherPlate)
			{
				foreach (var p in otherCell.points)
				{
					if (!p.isActive) continue;
					if (receivers.TryGetValue(p.plate.ID, out var list))
						list.Add(p);
				}
			}

		}, checkSelf: true);

		for (int i = cell.points.Count - 1; i >= 0; i--)
		{
			var p = cell.points[i];
			foreach (var pi in plates)
			{
				if (p.plate.ID == pi)
					continue;

				var cacheKey = (p.plate.ID, pi);

				if (!collisionCache.TryGetValue(cacheKey, out var collisionInfo))
				{
					var otherplate = map.Plates[pi];
					collisionInfo = GetLocalCollisionType(p, otherplate, cell, grid);
					collisionCache[cacheKey] = collisionInfo;
				}

				cell.collisionType = collisionInfo.Type;
				p.collisionType = collisionInfo.Type;
				HandleCollision(p, receivers[pi], collisionInfo);
			}
		}

		/*foreach (var p in cell.points)
		{
			foreach (var pi in plates)
			{
				if (p.plate.ID == pi)
					continue;

				var cacheKey = (p.plate.ID, pi);

				if (!collisionCache.TryGetValue(cacheKey, out var collisionInfo))
				{
					var otherplate = map.Plates[pi];
					collisionInfo = GetLocalCollisionType(p, otherplate, cell, grid);
					collisionCache[cacheKey] = collisionInfo;
				}

				cell.collisionType = collisionInfo.Type;
				p.collisionType = collisionInfo.Type;
				HandleCollision(p, map.Plates[pi], collisionInfo);
			}
		}*/
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="point"></param>
	/// <param name="otherplate"></param>
	/// <param name="cell"></param>
	/// <returns></returns>
	static CollisionInfo GetLocalCollisionType(PlatePoint point, Plate2D otherplate, GridCell cell, WorldGrid grid)
	{
		//Collision type is local because if a square plate collides into an L-shaped plate, then one side of the square plate would collide
		//	more similarily to a transform fault vs another side being mountain building
		//kinda slow
		point.boundaryNormal = ComputeGradient(point, otherplate, cell);

		var vel = point.plate.Velocity.Normalized();

		PlateCollisionType type = PlateCollisionType.None;
		float boundaryDot = vel.Dot(point.boundaryNormal);
		
		if (point.boundaryNormal.Length() > 0f)
		{
			//dot close to 0 = plate is shearing other plate. negative = plate is colliding headon
			type = ClassifyCollision(boundaryDot, point, otherplate);
		}
		else
		{
			//set the collision type to subduction or orogenic if the boundary normal is 0 (gets returned when theres 7 or 8 neighbours
			if (point.GetCrustType() == PlatePoint.CrustType.Oceanic)
				type = PlateCollisionType.Subduction;
			else type = PlateCollisionType.Orogenic;
		}



		return new CollisionInfo
		{
			BoundaryNormal = point.boundaryNormal,
			BoundaryDot = boundaryDot,
			Type = type
		};
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="point"></param>
	/// <param name="otherplate"></param>
	/// <param name="cell"></param>
	/// <returns>A normalized Vector2 pointing towards average direction of other plates</returns>
	static Vector2 ComputeGradient(PlatePoint point, Plate2D otherplate, GridCell cell)
	{
		//TODO: if a point is completely surrounded by the other plate (like if a collision was like the tibetan plateua),
		//	then i shouldnt use the gradient
		//		maybe fallback to using the 2 plate vels to compute boundary
		//		or just assume its either subduction or orogenic? since itll never happen for divergent or transform
		Vector2 gradient = Vector2.Zero;
		int count = 0;

		grid.ForEachNeighbor(cell.x, cell.y, (di, dj, otherCell) =>
		{
			if ((otherCell.ContainsBorderingOtherPlate || otherCell.ContainsCollision)
			&& !otherCell.IsEmptyOrInactive())
			{
				foreach (var p in otherCell.points)
				{
					if (p.plate != point.plate && p.isActive)
					{
						Vector2 dir = (p.WorldPos - point.WorldPos).Normalized();
						gradient += dir;
						count++;
					}
				}
			}
		});
		gradient = gradient.Normalized();
		if (count > 0 && count < 7)
			return -(gradient / count);
		else
			return Vector2.Zero;
	}

	static PlateCollisionType ClassifyCollision(float boundaryDot, PlatePoint point, Plate2D otherPlate)
	{
		//TODO: consider other plate
		const float threshold = 0.12f;

		if (boundaryDot < threshold && boundaryDot > -threshold)
			return PlateCollisionType.Transform;
		else if (boundaryDot <= -0.3f)
			return PlateCollisionType.Divergent;
		else if (point.GetCrustType() == PlatePoint.CrustType.Oceanic)
			return PlateCollisionType.Subduction;
		else return PlateCollisionType.Orogenic;
	}

	static void HandleCollision(PlatePoint point, List<PlatePoint> receivers, CollisionInfo info)
	{
		//todo: transfer material to other platepoints & dont use these arbituary placeholder values
		switch(info.Type)
		{
			case PlateCollisionType.Divergent:
				//SpawnPointsAtDivergentBoundary(point, map);
				break;
			case PlateCollisionType.Orogenic:
				point.RemoveMaterial(10f, 0.5f);
				break;
			case PlateCollisionType.Subduction:
				point.RemoveMaterial(30f, 120f);
				break;
			case PlateCollisionType.Transform:
				point.RemoveMaterial(0.5f, 10f);
				break;
		}
	}

	
	static void SpawnPointsAtDivergentBoundary(PlatePoint point, WorldMap map)
	{
		//check if gridcell is empty
		var cell = map.worldGrid.grid[point.gridIndex.X, point.gridIndex.Y];

		Vector2 otherpos = point.gridIndex + point.Velocity;
		var idx = map.worldGrid.GetIndexFromPosition(otherpos);
		var otherCell = map.worldGrid.grid[idx.X, idx.Y];

		if (otherCell.IsCompletelyEmpty())
		{
			//point.plate.AddPointToPlate(otherpos, 1f, 1f);
		}
	}
}

