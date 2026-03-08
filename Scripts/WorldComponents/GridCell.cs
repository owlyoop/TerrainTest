using Godot;
using System;
using System.Collections.Generic;

public class GridCell
{
	//TODO: might be better to have a 2D array, one row for each plate (plate.ID = index)
	public List<PlatePoint> points;

	public HashSet<int> PlateIDs;	//A unique list of the plate IDs that contain a point that lies in this gridcell

	public Dictionary<int, Vector2> OpposingForceSums = new Dictionary<int, Vector2>();

	public int x { get; private set; }
	public int y { get; private set; }

	public bool ContainsBoundary { get; private set; }  //If any point in this gridcell is a plate boundary
	public bool ContainsCollision { get; private set; }  //If any point in this gridcell is a plate collision
	public bool ContainsEdgeBoundary { get; private set; }

	public bool ContainsBorderingOtherPlate { get; private set; }
	public bool HasCollisionChecked { get; private set; }   //Used for the update functions that check for collisions/boundaries to avoid repeated checks.


	public PlateCollisionType collisionType;
	public GridCell(int x, int y)
	{
		points = new List<PlatePoint>();
		PlateIDs = new HashSet<int>();
		ContainsBoundary = false;
		ContainsCollision = false;
		ContainsEdgeBoundary = false;
		ContainsBorderingOtherPlate = false;
		collisionType = PlateCollisionType.None;
		HasCollisionChecked = false;
		this.x = x;
		this.y = y;

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

		foreach (var p in points)
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
		PlateIDs.Add(point.plate.ID);
		var num = GetNumberOfSamePlate(point);
		if (num >= 4)
		{
			Consolidate(point);
		}
	}

	public void RemovePoint(PlatePoint point)
	{
		bool lastPoint = true;
		points.Remove(point);
		foreach (var p in points)
		{
			if (p.plate.ID == point.plate.ID)
			{
				lastPoint = false;
				break;
			}
		}

		if (lastPoint)
			PlateIDs.Remove(point.plate.ID);

		if (points.Count == 0)
			MarkAsEmpty();
	}

	public void MarkAsEmpty()
	{
		this.ContainsCollision = false;
		this.ContainsBoundary = false;
		this.ContainsEdgeBoundary = false;
		this.ContainsBorderingOtherPlate = false;
		this.collisionType = PlateCollisionType.None;
		foreach (var p in points)
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
		foreach (var p in points)
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
		foreach (var p in this.points)
		{
			if (p.plate == point.plate)
				result++;
		}
		return result;
	}

	public void Consolidate(PlatePoint point)
	{
		var plate = point.plate;
		var newpos = Vector2.Zero;
		int count = 0;
		float f = 0;
		float m = 0;
		for (int i = points.Count - 1; i >= 0; i--)
		{
			if (points[i].plate == plate)
			{
				newpos += points[i].WorldPos;
				count++;
				f = points[i].Felsic;
				m = points[i].Mafic;
				plate.points.Remove(points[i]);
				points.Remove(points[i]);
			}
		}
		newpos /= count;
		f /= count;
		m /= count;

		var p = new PlatePoint(plate.WorldToLocal(newpos), f, m, plate);
		plate.points.Add(p);
		p.gridIndex = point.gridIndex;
		points.Add(p);
	}
}
