using Godot;
using System;

/// <summary>
/// Represents a point of the earth's crust on a tectonic plate.
/// </summary>
public class PlatePoint
{
	public enum CrustType
	{
		Oceanic,
		Continental	//does not subduct
	}
	public CrustType Crust = CrustType.Oceanic;
	public PlateCollisionType collisionType;

	//kg/m^3
	public const float DENSITY_FELSIC = 2600f;	//2300-2800 i think
	public const float DENSITY_MAFIC_YOUNG = 2800f;
	public const float DENSITY_MAFIC_OLD = 3500f;
	public const float MAFIC_MAX_AGE = 1000f;    //age where the mafic reaches max density

	

	private float _felsic;	//Continental, less dense.
	private float _mafic;	//Oceanic, about 10-15% denser than felsic?

	/// <summary>
	/// Mass of Felsic (continental) rock material
	/// </summary>
	public float Felsic
	{
		get => _felsic;
		set => _felsic = value;
	}

	/// <summary>
	/// Mass of Mafic (oceanic) rock material
	/// </summary>
	public float Mafic
	{
		get => _mafic;
		set => _mafic = value;
	}

	//i am representing platepoints as 1km x 1km samples
	public float area = 1f; 
	public float mass;
	public float thickness;
	public float density;
	public float buoyancy;


	public float height { get; private set; }

	public float age;

	public Vector2 localPos;

	private Vector2 _cachedWorldPos;
	private bool _worldPosDirty = true;
	public Vector2 WorldPos
	{
		get
		{
			if (_worldPosDirty)
			{
				_cachedWorldPos = plate.LocalToWorld(localPos);
				_worldPosDirty = false;
			}
			return _cachedWorldPos;
		}
	}


	public Vector2 prevWorldPos;	//The world position of the previous timetick

	public Vector2 boundaryNormal;	//For points that are currently colliding, represents 

	public Plate2D plate;
	public Vector2I gridIndex; //Index for the worldgrid

	public Vector2 Velocity;

	public bool isActive = false;
	public bool IsColliding { get; private set; }   //If the point is 'colliding' with another point on a different plate
	public bool IsBoundary { get; private set; }    //if egde of plate. if moves enough without colliding, spawn a new platepoint behind it.
	public bool IsEdgeBoundary { get; private set; }    //if edge of plate next to empty space (used for determining where point creation should happen
	public bool IsBorderingOtherPlate { get; private set; }

	public float distTravelAsBoundary = 0f;
	public float distTravelNoCollision = 0f; //not used, dunno if i will need to track this in the future

	public PlatePoint(Vector2 localPos, float felsic, float mafic, Plate2D plate)
	{
		this.localPos = localPos;
		this.plate = plate;

		this.isActive = false;
		this.IsBoundary = false;
		this.IsColliding = false;
		this.IsBorderingOtherPlate = false;

		Felsic = felsic;
		Mafic = mafic;

		age = 0f;

		PhysicalProperties();
	}
	public void PhysicalProperties()
	{
		mass = Felsic + Mafic;

		float ageRatio = Mathf.Clamp(age / MAFIC_MAX_AGE, 0f, 1f);
		float maficDensity = Mathf.Lerp(DENSITY_MAFIC_YOUNG, DENSITY_MAFIC_OLD, ageRatio);

		float felsicVolume = Felsic / DENSITY_FELSIC;
		float maficVolume = Mafic / maficDensity;
		float volume = felsicVolume + maficVolume;

		thickness = volume * area;
		density = mass / volume;
		

		buoyancy = (DENSITY_MAFIC_OLD - density) / DENSITY_MAFIC_OLD;
		height = thickness * buoyancy; //km. values seem to mainly be between 0 and 0.5, todo look into this
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="giver"></param>
	/// <param name="receiver"></param>
	/// <param name="felsic">Amount of felsic the giver loses to the receiver</param>
	/// <param name="mafic">Amount of malic the giver loses to the receiver</param>
	public static void TransferMaterial(PlatePoint giver, PlatePoint receiver, float felsic, float mafic)
	{
		giver.Felsic -= felsic;
		giver.Mafic -= mafic;
		receiver.Felsic += felsic;
		receiver.Mafic += mafic;
	}

	public float[] RemoveMaterial(float felsic, float mafic)
	{
		float f = Mathf.Clamp(Felsic - felsic, 0f, float.MaxValue);
		float m = Mathf.Clamp(Mafic - mafic, 0f, float.MaxValue);
		float[] material = new float[2];
		material[0] = f;
		material[1] = m;
		Felsic = f;
		Mafic = m;
		PhysicalProperties();
		//todo: set neighbours as active
		if (Felsic < 0.01f && Mafic < 0.01f)
		{
			//i need to store a reference to grid in platepoint class. jesus christ
			var worldGrid = plate.map.worldGrid;
			var grid = plate.map.worldGrid.grid;

			this.plate.RemovePoint(this);
			grid[gridIndex.X, gridIndex.Y].RemovePoint(this);
			if (grid[gridIndex.X, gridIndex.Y].IsEmptyOrInactive() || grid[gridIndex.X, gridIndex.Y].points.Count == 0)
			{
				worldGrid.ForEachNeighbor(gridIndex.X, gridIndex.Y, (di, dj, otherCell) =>
				{
					otherCell.MarkAllAsBoundary(true);
					otherCell.MarkAllAsEdgeBoundary(true);
				}, checkSelf: false);
			}
		}
		return material;
	}

	public void AddMaterial(float felsic, float mafic)
	{
		Felsic += felsic;
		Mafic += mafic;
	}

	public CrustType GetCrustType()
	{
		var total = Felsic + Mafic;
		if (Mafic / total >= 0.5f)
			Crust = CrustType.Oceanic;
		else Crust = CrustType.Continental;
		return Crust;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns>Returns true if the point still exists & was not removed after timestep</returns>
	public bool OnTimestep()
	{
		age = age + 1f;
		
		if (age > 500f)
		{
			RemoveMaterial(0f, 0.5f);
			if (Felsic <= 0.01f && Mafic <= 0.01f)
				return false;
		}
		PhysicalProperties();
		return true;
	}

	public void UpdateTravelStats(float requiredDistance, string spawnMethod)
	{
		float dist = plate.map.WrappedDistance(prevWorldPos, WorldPos);
		if (IsEdgeBoundary && !IsColliding)
			distTravelAsBoundary += dist;

		if (distTravelAsBoundary < requiredDistance) return;
		if (!IsEdgeBoundary || IsColliding) return;

		if (plate.Velocity.Length() > 0f)
		{
			switch (spawnMethod)
			{
				case "single":
					Vector2 behind = WorldPos - (plate.Velocity.Normalized() * 1f);
					behind.X = Mathf.PosMod(behind.X, plate.map.worldWidth);
					behind.Y = Mathf.PosMod(behind.Y, plate.map.worldHeight);
					Vector2I n = plate.map.worldGrid.GetIndexFromPosition(behind);
					var newpt = new Vector2(n.X + 0.5f, n.Y + 0.5f);
					SpawnPoint(newpt, 5f, 10f);
					break;

				case "area":
					plate.map.worldGrid.ForEachNeighbor(gridIndex.X, gridIndex.Y, (di, dj, otherCell) =>
					{
						if (!otherCell.IsCompletelyEmpty()) return;
						Vector2 dir = new Vector2(di - gridIndex.X, dj - gridIndex.Y).Normalized();
						float dot = dir.Dot(plate.Velocity.Normalized());
						if (dot > 0.2f) return;

						Vector2 worldPos = new Vector2(otherCell.x + 0.5f, otherCell.y + 0.5f);
						SpawnPoint(worldPos, 5f, 10f);
					}, checkSelf: false);
					break;

				default:
					GD.PrintErr("Invalid spawnMethod param for UpdateTravelStats");
					break;
			}
			
		}

		void SpawnPoint(Vector2 worldpos, float felsic, float mafic)
		{
			//check if new point is completely surrounded by this points plate.
			//if so, then material should be avg of all 8
			//	this is because when a plate rotates, the spacing of platepts creates holes so this stops "tearing" in the middle from all
			//		the new pts being spawned with very little material
			bool isInternal = true;
			float f = 0f;
			float m = 0f;
			float age = 0f;
			int count = 0;
			plate.map.worldGrid.ForEachNeighbor(gridIndex.X, gridIndex.Y, (di, dj, otherCell) =>
			{
				if (isInternal)
				{
					foreach (var p in otherCell.points)
					{
						if (p.plate != this.plate)
						{
							isInternal = false;
							break;
						}
						else
						{
							f += p.Felsic;
							m += p.Mafic;
							age += p.age;
							count++;
						}
					}
				}
			}, checkSelf: false);

			var p = plate.AddPointToPlate(worldpos, felsic, mafic);
			if (p != null)
			{
				p.MarkPointAsBoundary(true);
				p.MarkPointAsEdgeBoundary(true);
				p.Velocity = plate.Velocity;
				distTravelAsBoundary = 0f;
				if (isInternal)
				{
					p.Felsic = f / count;
					p.Mafic = m / count;
					p.age = age / count;
					p.PhysicalProperties();
				}
			}
		}
	}
	

	#region state bookkeeping
	public void MarkPointAsBoundary(bool choice)
	{
		IsBoundary = choice;
		if (choice)
		{
			IsColliding = false;
			isActive = true;
		}
	}

	public void MarkPointAsColliding(bool choice)
	{
		IsColliding = choice;
		if (choice)
		{
			distTravelNoCollision = 0f;
			distTravelAsBoundary = 0f;
			IsBoundary = false;
			IsEdgeBoundary = false;
			isActive = true;
		}
	}

	public void MarkPointAsEdgeBoundary(bool choice)
	{
		IsEdgeBoundary = choice;
		if (choice)
			isActive = true;
	}

	public void MarkPointAsBorderingOtherPlate(bool choice)
	{
		IsBorderingOtherPlate = choice;
		if (choice)
			isActive = true;
	}

	public void SetWorldPosDirty()
	{
		_worldPosDirty = true;
	}
	#endregion
}
