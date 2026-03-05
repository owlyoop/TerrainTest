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

	//total mass is felsic + mafic

	//width of crust in meters
	//calculated from mass / density
	//malic gets denser with age and also thicker
	public float crustThickness;

	// mass / thickness
	public float density;      

	//same as height. in meters.
	

	float age;  //1f = 1mil years?

	//derived from thickness and density.
	//thicker = higher elevation. denser = crust sinks so lower elevation
	public float height { get; private set; }

	public Vector2 localPos;
	public Vector2 WorldPos => plate.LocalToWorld(localPos);    //The world position
	public Vector2 cachedWorldPos;

	

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
	Vector2 lastBoundaryPos; //TODO?: The last position this point became a boundary.
							 //When the plate moves a certain dist away, a new platepoint is created

	/* TODO:
	 * differentiate between oceanic and continental crust
	 *		continental is made up of felsic, oceanic is mafic (cooled magma).
	 *		plutonic rock from magma cooling
	 * make crust age over time. oceanic crust cools and gets denser over time
	 * continental is thicker and less dense
	 * make elevation derived from crust thickness
	 * make plate density derived from points? like a plate of only continental would be less dense than only oceanic.
	 * make platepoints have their own velocity, and the plate's velocity is an avg of all points
	 */

	

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

		CalculateDensity();
		CalculateThickness();
		CalculateHeight();
	}

	public float CalculateDensity()
	{
		float totalMass = Felsic + Mafic;

		float ageRatio = Mathf.Clamp(age / MAFIC_MAX_AGE, 0f, 1f);
		float maficDensity = Mathf.Lerp(DENSITY_MAFIC_YOUNG, DENSITY_MAFIC_OLD, ageRatio);
		float maficVolume = Mafic / maficDensity;

		float totalVolume = (Felsic / DENSITY_FELSIC) + maficVolume;

		density = totalMass / totalVolume;
		return density;
	}
	public void CalculateThickness()
	{
		float ageRatio = Mathf.Clamp(age / MAFIC_MAX_AGE, 0f, 1f);
		float maficDensity = Mathf.Lerp(DENSITY_MAFIC_YOUNG, DENSITY_MAFIC_OLD, ageRatio);

		//assuming the length&width are 1km
		crustThickness = (Felsic / DENSITY_FELSIC) + (Mafic / maficDensity);

	}

	public void CalculateHeight()
	{
		float buoyancy = (3500f - density) / 3500f;	//todo: 
		float baseElevation = crustThickness * buoyancy;
		height = baseElevation;
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
		CalculateHeight();
		if (Felsic < 0.01f && Mafic < 0.01f)
		{
			this.plate.points.Remove(this);

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

	public void OnTimestep()
	{
		age = age + 1f;
		CalculateDensity();
		CalculateThickness();
		CalculateHeight();
	}

	public void UpdateTravelStats()
	{
		//GD.Print(density + " " +crustThickness + " " + height);

		float dist = plate.map.WrappedDistance(cachedWorldPos, cachedWorldPos - (plate.Velocity));
		if (IsEdgeBoundary && !IsColliding)
			distTravelAsBoundary += dist;
		//spawn new platepoints
		//todo: sim eventually slows down to a halt. i think cause too many points spawn.
		//need to consolidate points if theres more than 2 of the same plate in a cell
		if (distTravelAsBoundary >= 0.02f && IsEdgeBoundary)
		{
			var newpt = cachedWorldPos - (plate.Velocity.Normalized());
			var p = plate.TryAddPointToPlate(newpt, 10f, 10f, 1);
			if (p != null)
			{
				p.MarkPointAsBoundary(true);
				p.MarkPointAsEdgeBoundary(true);
				p.Velocity = plate.Velocity;
				distTravelAsBoundary = 0f;
				plate.map.worldGrid.grid[gridIndex.X, gridIndex.Y].MarkAllAsBoundary(false);
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
	#endregion
}
