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

	static Dictionary<(int cellKey, int plateId, int otherId), Vector2> gradientCache
		= new Dictionary<(int, int, int), Vector2>();

	static public void ClearGradientCache()
	{
		gradientCache.Clear();
	}

	public static void RegisterCollisions(GridCell cell, WorldMap _map)
	{
		map = _map;
		grid = map.worldGrid;


		if (cell.PlateIDs.Count < 2)
			return;

		var plates = new HashSet<int>(cell.PlateIDs);
		var receivers = new Dictionary<int, List<PlatePoint>>(plates.Count);
		var collisionCache = new Dictionary<(int plateID, int otherID), CollisionInfo>();

		foreach (var id in plates)
			receivers[id] = new List<PlatePoint>(32);

		grid.ForEachNeighbor(cell.x, cell.y, (di, dj, otherCell) =>
		{
			if (otherCell.ContainsCollision || otherCell.ContainsBorderingOtherPlate)
			{
				foreach (var p in otherCell.points)
				{
					if (!p.isActive) continue;
					plates.Add(p.plate.ID);
					if (receivers.TryGetValue(p.plate.ID, out var list))
						list.Add(p);
					else
						receivers[p.plate.ID] = new List<PlatePoint>{p};
				}
			}

		}, checkSelf: true);

		if (plates.Count < 2)
			return;

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
				if (receivers[pi].Count > 0)
					HandleCollision(p, receivers[pi], collisionInfo);
			}
		}
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
		int cellkey = cell.x * 10000 + cell.y;
		var cachekey = (cellkey, point.plate.ID, otherplate.ID);

		if (!gradientCache.TryGetValue(cachekey, out var cachedgradient))
		{
			cachedgradient = ComputeGradient(point, otherplate, cell);
			gradientCache[cachekey] = cachedgradient;
		}

		point.boundaryNormal = cachedgradient;
		//point.boundaryNormal = ComputeGradient(point, otherplate, cell);
		//point.boundaryNormal = ComputeGradientSimple(point, otherplate);

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
		//TODO: this is very slow
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

	static Vector2 ComputeGradientSimple(PlatePoint point, Plate2D otherplate)
	{
		Vector2 relativeVelocity = point.plate.Velocity - otherplate.Velocity;

		if (relativeVelocity.Length() < 0.001f)
			return Vector2.Zero;  // Plates moving together, no clear boundary

		Vector2 normal = new Vector2(-relativeVelocity.Y, relativeVelocity.X).Normalized();
		return normal;
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
		if (info.Type == PlateCollisionType.Divergent) return;
		int count = receivers.Count;
		if (count == 0) return;
		PlatePoint closest = null;
		var dist = 100000f;
		foreach(var p in receivers)
		{
			var d = map.WrappedDistance(p.WorldPos, point.WorldPos);
			if (d < dist)
			{
				dist = d;
				closest = p;
			}
		}

		switch (info.Type)
		{
			case PlateCollisionType.Divergent:
				//SpawnPointsAtDivergentBoundary(point, map);
				break;

			case PlateCollisionType.Orogenic:
				//crust compress & pile, thickness increases
				if (point.density < closest.density)
				{
					float f = closest.Felsic * 0.4f;
					float m = closest.Mafic * 0.1f;
					closest.GiveMaterial(point, f + 10f, m + 10f);
				}
				else
				{
					float f = point.Felsic * 0.4f;
					float m = point.Mafic * 0.1f;
					point.GiveMaterial(closest, f + 10f, m + 10f);
				}

				//point.AddMaterial(10f, 0f);
				//closest.AddMaterial(10f, 0f);
				break;

			case PlateCollisionType.Subduction:
				//transfer felsic to less dense point, mafic subducts
				if (point.density < closest.density)
				{
					float f = closest.Felsic * 0.2f;
					closest.GiveMaterial(point, f, 0);
					closest.RemoveMaterial(0, (closest.Mafic * 0.2f) + 10f);
				}
				else
				{
					float f = point.Felsic * 0.2f;
					point.GiveMaterial(closest, f, 0);
					point.RemoveMaterial(0, (point.Mafic * 0.2f ) + 10f);
				}
				break;

			case PlateCollisionType.Transform:
				if (point.density < closest.density)
				{
					/*closest.GiveMaterial(point,
						closest.Felsic * closest.buoyancy * (closest.Velocity.Normalized().Dot(closest.boundaryNormal)),
						closest.Mafic * closest.buoyancy * (closest.Velocity.Normalized().Dot(closest.boundaryNormal)));*/
					closest.GiveMaterial(point, closest.Felsic * 0.01f, closest.Mafic * 0.01f);
				}
				else
				{
					point.GiveMaterial(closest, point.Felsic * 0.01f,	point.Mafic * 0.01f);
				}
				break;
		}
	}
}

