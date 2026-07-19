using Godot;
using System;
using System.Collections.Generic;

public class GridCell
{
	//TODO: might be better to have a 2D array, one row for each plate (plate.ID = index)
	public List<PlatePoint> points;

	public HashSet<int> PlateIDs;	//A unique list of the plate IDs that contain a point that lies in this gridcell

	

	public int x { get; private set; }
	public int y { get; private set; }

	public bool ContainsBoundary { get; private set; }  //If any point in this gridcell is a plate boundary
	public bool ContainsCollision { get; private set; }  //If any point in this gridcell is a plate collision
	public bool ContainsEdgeBoundary { get; private set; }

	public bool ContainsBorderingOtherPlate { get; private set; }
	public bool HasCollisionChecked { get; private set; }   //Used for the update functions that check for collisions/boundaries to avoid repeated checks.


	//public PlateCollisionType collisionType;

	public Dictionary<int, Vector2> OpposingForceSums = new Dictionary<int, Vector2>();
	public Dictionary<int, float> OpposingMassSums = new Dictionary<int, float>();

	public GridCell(int x, int y)
	{
		points = new List<PlatePoint>();
		PlateIDs = new HashSet<int>();
		ContainsBoundary = false;
		ContainsCollision = false;
		ContainsEdgeBoundary = false;
		ContainsBorderingOtherPlate = false;
		//collisionType = PlateCollisionType.None;
		HasCollisionChecked = false;
		this.x = x;
		this.y = y;

	}

	public void UpdateOpposingForceSums()
	{
		OpposingForceSums.Clear();
		OpposingMassSums.Clear();

		foreach (var p in points)
		{
			if (!p.isActive) continue;
			if (!p.IsColliding && !p.IsBorderingOtherPlate) continue;

			int id = p.plate.ID;
			var mass = p.mass;
			if (mass <= 0f) mass = 1f;

			if (!OpposingForceSums.ContainsKey(id))
			{
				OpposingForceSums[id] = Vector2.Zero;
				OpposingMassSums[id] = 0f;
			}
			OpposingForceSums[id] += p.plate.Velocity * p.mass;
			OpposingMassSums[id] += mass;

		}

		foreach (var plate in OpposingForceSums.Keys)
		{
			OpposingForceSums[plate] /= OpposingMassSums[plate];
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
			Consolidate(point, true);
		}

		if (PlateIDs.Count >= 2)
		{
			//TransferMatDifPlates();
			MergeToDensest();
		}
		else if (PlateIDs.Count >= 2)
		{
			//TransferMatDifPlates();
			//MergeToDensePlate();
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
		//this.collisionType = PlateCollisionType.None;
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

	public void Consolidate(PlatePoint point, bool useGridcellCenter)
	{
		//todo: check if world wrapping works correctly here
		var plate = point.plate;
		var newpos = Vector2.Zero;
		int count = 0;
		float f = 0;
		float m = 0;
		float age = 0.0001f;
		bool newPtShouldBeActive = false;
		for (int i = points.Count - 1; i >= 0; i--)
		{
			if (points[i].plate == plate)
			{
				if (points[i].isActive)
					newPtShouldBeActive = true;
				newpos += points[i].WorldPos;
				count++;
				f += points[i].Felsic;
				m += points[i].Mafic;
				age += points[i].age;
				points[i].Felsic = 0f;
				points[i].Mafic = 0f;
				plate.RemovePoint(points[i]);
				points.Remove(points[i]);
			}
		}
		f /= count;
		m /= count;
		age /= count;
		if (useGridcellCenter)
		{
			newpos = new Vector2(this.x + 0.5f,
				this.y + 0.5f);
		}
		else
			newpos /= count;
		var p = new PlatePoint(plate.WorldToLocal(newpos), f, m, plate);
		p.age = age;
		p.PhysicalProperties();
		p.isActive = newPtShouldBeActive;
		//plate.points.Add(p);
		plate.AddDirect(p);
		p.gridIndex = point.gridIndex;
		points.Add(p);
	}

	public void MergeToDensest()
	{
		if (points.Count < 2 || PlateIDs.Count < 2) return;

		var plate = points[0].plate;
		float maxDensity = float.MaxValue;
		float felsic = 0f;
		float mafic = 0f;
		float age = 0;
		float maxage = 0;
		int count = 0;
		PlatePoint best = null;

		foreach (var pt in points)
		{
			if (!pt.isActive) continue;
			felsic += pt.Felsic;
			mafic += pt.Mafic;
			age += pt.age;
			if (pt.age > maxage)
				maxage = pt.age;
			count++;
			if (pt.density < maxDensity)
			{
				plate = pt.plate;
				maxDensity = pt.density;
				best = pt;
			}
		}

		for (int i = points.Count - 1; i >= 0; i--)
		{
			points[i].RemoveMaterial(float.MaxValue, float.MaxValue);
			points[i].plate.RemovePoint(points[i]);
			points.Remove(points[i]);
		}
		if (count == 0) count = 1;
		//felsic *= 0.99f;
		//mafic *= 0.99f;
		if (felsic > 100000) felsic = 100000;
		if (mafic > 100000) mafic = 100000;
		//age /= count;
		age = maxage;
		var newpos = new Vector2(this.x + 0.5f, this.y + 0.5f);
		var p = new PlatePoint(plate.WorldToLocal(newpos), felsic, mafic, plate);
		p.age = age;
		p.PhysicalProperties();
		p.isActive = true;
		p.gridIndex = new Vector2I(x, y);
		//plate.points.Add(p);
		plate.AddDirect(p);
		points.Add(p);
		PlateIDs.Clear();
		PlateIDs.Add(p.plate.ID);
	}

	public void TransferMatDifPlates()
	{
		if (points.Count < 2 && PlateIDs.Count < 2) return;

		var plate = points[0].plate;
		float bestDensity = 0;
		float felsic = 0f;
		float mafic = 0f;
		float age = 1;
		int count = 0;
		PlatePoint best = null;
		foreach (var pt in points)
		{
			if (!pt.isActive) continue;
			felsic += pt.Felsic;
			mafic += pt.Mafic;
			age += pt.age;
			count++;
			if (pt.plate.totalMass > bestDensity)
			{
				plate = pt.plate;
				bestDensity = pt.plate.totalMass;
				best = pt;
			}
		}

		if (count == 0) count = 1;

		for (int i = points.Count - 1; i >= 0; i--)
		{
			var point = points[i];
			if (best == null) continue;
			if (point == best) continue;
			if (!point.isActive) continue;

			float f = point.Felsic * 0.98f;
			float m = point.Mafic * 0.98f;

			point.GiveMaterial(best, f + 100f, m + 100f);
			point.CheckIfDestroySelf();
		}
		if (best != null)
		{
			//best.Felsic /= count;
			//best.Mafic /= count;
			best.Felsic *= 0.99f;
			best.Mafic *= 0.99f;
			//best.PhysicalProperties();
		}
		

	}

}
