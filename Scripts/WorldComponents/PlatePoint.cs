using Godot;
using System;

public class PlatePoint
{
	public enum CrustType
	{
		Oceanic,
		Continental
	}

	public enum CrustMaterial
	{
		Felsic,     //Continental, less dense. 4x thicker on avg on earth?
		Malic       //Oceanic, about 10-15% denser than felsic?
	}

	public CrustType Crust = CrustType.Oceanic;
	float[] materialAmounts;        //Amount of material per CrustMaterial enum. rename to mass?

	float crustThickness;   //calculated from amount of felsic and malic.
							//malic gets denser with age so less thick?

	float density;      //amount / thickness
	float elevation;    //derived from thickness and density. thicker = higher elevation. denser = crust sinks so lower elevation
	float age;  //1f = 1mil years?

	public Vector2 localPos;
	public Vector2 WorldPos => plate.LocalToWorld(localPos);    //The world position
	public Vector2 cachedWorldPos;

	public float height; //	TODO: derive this from other factors instead of explicitly setting
	public Plate2D plate;
	public Vector2I gridIndex; //Index for the worldgrid


	public bool isActive = false;
	public bool IsColliding { get; private set; }   //If the point is 'colliding' with another point on a different plate
	public bool IsBoundary { get; private set; }    //if egde of plate. if moves enough without colliding, spawn a new platepoint behind it.
	public bool IsEdgeBoundary { get; private set; }    //if edge of plate next to empty space (used for determining where point creation should happen

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

	public PlatePoint(Vector2 localPos, float height, Plate2D plate)
	{
		this.localPos = localPos;
		this.height = height;
		this.plate = plate;
		this.isActive = false;
		this.IsBoundary = false;
		this.IsColliding = false;
		//neighbours = new List<PlatePoint>();


		materialAmounts = new float[Enum.GetNames(typeof(CrustMaterial)).Length];
	}

	public void OnEndTimestep()
	{

	}

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
		{
			isActive = true;
		}
		else
		{
			//distTravelAsBoundary = 0f;
		}
	}

	public void UpdateTravelStats()
	{
		float dist = plate.map.WrappedDistance(cachedWorldPos, cachedWorldPos - (plate.MovementDirection.Normalized() * plate.MovementSpeed));
		if (IsBoundary)
			distTravelAsBoundary += dist;
		//else distTravelAsBoundary = 0f;
		if (!IsColliding)
			distTravelNoCollision += dist;

		//spawn new platepoints
		//todo: sim eventually slows down to a halt. i think cause too many points spawn.
		//need to consolidate points if theres more than 2 of the same plate in a cell
		if (distTravelAsBoundary > 0.1f && IsEdgeBoundary)
		{
			var newpt = cachedWorldPos - (plate.MovementDirection.Normalized() * 1f);
			var p = plate.TryAddPointToPlate(newpt, height - 0.01f, 3);
			if (p != null)
			{
				p.MarkPointAsBoundary(true);
				p.MarkPointAsEdgeBoundary(true);
				//MarkPointAsBoundary(false);
				distTravelAsBoundary = 0f;
				plate.map.worldGrid.grid[gridIndex.X, gridIndex.Y].MarkAllPointsAsBoundary(false);
				plate.map.worldGrid.grid[gridIndex.X, gridIndex.Y].MarkAllPointsAsEdgeBoundary(false);
			}
		}

	}
}
