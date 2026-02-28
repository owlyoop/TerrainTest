using Godot;
using System;
using System.Collections.Generic;


public enum PlateCollisionType
{
	Orogenic,       //continental vs continental. mountain building
	Subduction,     //oceanic vs continental OR oceanic vs oceanic.
					//	oceanic subducts under continental or less dense oceanic plate
	Divergent,      //moving away from eachother.
	Transform       //any 2 plates moving past eachother
}

public struct CollisionInfo
{
	public Vector2 BoundaryNormal;
	public float ConvergenceSpeed;
	public float ShearSpeed;
	public PlateCollisionType Type;
}
public static class PlateCollision
{
	public static void RegisterCollisions(GridCell cell, WorldGrid grid, WorldMap map)
	{
		//TODO: collisions instead of this placeholder stuff
		/* ideas
		 * get relative velocity between plates?
		 * dif collision types? continental vs continental: no subduction, build mountain
		 * continental vs oceanic: oceanic subducts
		 * oceanic vs oceanic: denser oceanic plate subducts
		 * 
		 * platepoints can have both continental&oceanic material on them.
		 */

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
					HandleCollision(p, otherplate, collisionInfo);

					if (p.plate.VelocityDots.ContainsKey(otherplate))
					{

					}
					else
					{
						GD.PrintErr("Plate doesn't contain collision info");
					}
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
		var boundary = ComputeGradient(point, otherplate, cell, grid);
		var relativeVel = otherplate.Velocity - point.plate.Velocity;

		float convergence = relativeVel.Dot(boundary);
		var parallel = relativeVel - (boundary * convergence);
		float shear = parallel.Length();

		var type = ClassifyCollision(convergence, shear, point);

		return new CollisionInfo
		{
			BoundaryNormal = boundary,
			ConvergenceSpeed = convergence,
			ShearSpeed = shear,
			Type = type
		};
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="point"></param>
	/// <param name="otherplate"></param>
	/// <param name="cell"></param>
	/// <returns></returns>
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
					Vector2 dir = (p.WorldPos - point.WorldPos).Normalized();
					gradient += dir;
					count++;
				}
			}
		});

		if (count > 0)
			return (gradient / count).Normalized();
		else
			return (otherplate.Velocity - point.plate.Velocity).Normalized();
	}

	static PlateCollisionType ClassifyCollision(float convergenceSpeed, float shearSpeed, PlatePoint point)
	{
		const float convergenceThreshold = 0.05f;
		const float shearThreshold = 0.05f;

		if (convergenceSpeed < -convergenceThreshold)
			return PlateCollisionType.Divergent;
		if (convergenceSpeed < convergenceThreshold && shearSpeed > shearThreshold)
			return PlateCollisionType.Transform;
		if (point.GetCrustType() == PlatePoint.CrustType.Oceanic)
			return PlateCollisionType.Subduction;
		else return PlateCollisionType.Orogenic;
	}

	static void HandleCollision(PlatePoint point, Plate2D otherplate, CollisionInfo info)
	{
		switch(info.Type)
		{
			case PlateCollisionType.Divergent:
				point.AddMaterial(0.0f, 0.05f);
				break;
			case PlateCollisionType.Orogenic:
				point.AddMaterial(0.05f, 0.0f);
				break;
			case PlateCollisionType.Subduction:
				break;
			case PlateCollisionType.Transform:
				break;
		}
	}
}

