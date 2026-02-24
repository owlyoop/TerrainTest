using Godot;
using System;

/// <summary>
/// Deprecated class, currently used as an intermediary between noise generation and assigning values to platepoints
/// TODO: remove
/// </summary>
public partial class Cell2D : GodotObject
{
    public int x; //width position
    public int y; //height position
    public float height;
    float[] precipitations = new float[12];
    float[] temperatures = new float[12];
    public Color color;

    public Plate2D plate;
	public Vector2I localPos;	//The local position relative to the plate
	public Vector2 position;

    public Cell2D(int widthPos, int heightPos)
    {
        x = widthPos;
        y = heightPos;
    }
    
    //normalizes height to a meter range. float input is -1f to 1f, result is ???
    public void SetHeight(float input)
    {
        height = input;
        //SetColor();
    }
    
}
