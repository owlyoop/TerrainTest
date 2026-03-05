using Godot;
using System;
using System.Collections.Generic;


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
	public static void RegisterCollisions(GridCell cell, WorldGrid grid, WorldMap map)
	{
		var plates = new HashSet<int>(cell.PlateIDs);

		grid.ForEachNeighbor(cell.x, cell.y, (di, dj, otherCell) =>
		{
			if (otherCell.ContainsCollision || otherCell.ContainsBorderingOtherPlate)
				plates.UnionWith(otherCell.PlateIDs);
		});

		if (plates.Count < 2)
			return;
		

		foreach (var p in cell.points)
		{
			foreach (var pi in plates)
			{
				var otherplate = map.Plates[pi];
				if (p.plate.ID != pi)
				{
					var collisionInfo = GetLocalCollisionType(p, otherplate, cell, grid);
					cell.collisionType = collisionInfo.Type;
					p.collisionType = collisionInfo.Type;
					HandleCollision(p, otherplate, collisionInfo);
				}
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
		//Collision type is local because if a square plate collides into an L-shaped plate, then one side of the square plate would collide
		//	more similarily to a transform fault vs another side being mountain building
		//kinda slow
		var boundary = -ComputeGradient(point, otherplate, cell, grid);

		var vel = point.plate.Velocity.Normalized();
		//close to 0 = plate is shearing other plate. negative = plate is colliding headon
		float boundaryDot = vel.Dot(boundary);

		var type = ClassifyCollision(boundaryDot, point);

		return new CollisionInfo
		{
			BoundaryNormal = boundary,
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
	/// <returns>A normalized Vector2 pointing in the average direction of other plates</returns>
	static Vector2 ComputeGradient(PlatePoint point, Plate2D otherplate, GridCell cell, WorldGrid grid)
	{
		Vector2 gradient = Vector2.Zero;
		int count = 0;

		grid.ForEachNeighbor(cell.x, cell.y, (di, dj, otherCell) =>
		{
			if (otherCell.ContainsBorderingOtherPlate || otherCell.ContainsCollision)
			{
				foreach (var p in otherCell.points)
				{
					if (p.plate != point.plate)
					{
						Vector2 dir = (p.WorldPos - point.WorldPos).Normalized();
						gradient += dir;
						count++;
					}
				}
			}
		});
		gradient = gradient.Normalized();
		if (count > 0)
			return (gradient / count).Normalized();
		else //shouldnt be possible to reach this part, but just incase
			return (otherplate.Velocity - point.plate.Velocity).Normalized();
	}

	static PlateCollisionType ClassifyCollision(float boundaryDot, PlatePoint point)
	{
		const float threshold = 0.1f;

		if (boundaryDot < threshold && boundaryDot > -threshold)
			return PlateCollisionType.Transform;
		else if (boundaryDot > 0.9f)
			return PlateCollisionType.Divergent;
		else if (point.GetCrustType() == PlatePoint.CrustType.Oceanic)
			return PlateCollisionType.Subduction;
		else return PlateCollisionType.Orogenic;
	}

	static void HandleCollision(PlatePoint point, Plate2D otherplate, CollisionInfo info)
	{
		//todo: transfer material to other platepoints
		switch(info.Type)
		{
			case PlateCollisionType.Divergent:
				point.RemoveMaterial(60f, 60f);
				break;
			case PlateCollisionType.Orogenic:
				point.AddMaterial(20f, 0.0f);
				break;
			case PlateCollisionType.Subduction:
				point.RemoveMaterial(5f, 200f);
				break;
			case PlateCollisionType.Transform:
				point.RemoveMaterial(10f, 10f);
				break;
		}
	}
}

