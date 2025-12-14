using Godot;
using System;

public partial class Cell2D : GodotObject
{
    public int x; //width position
    public int y; //height position
    public float height;
    Vector2 windDir;
    Vector2 flowDir;
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

    public Cell2D(float height, int widthPos, int heightPos)
    {
        this.height = height;
        x = widthPos;
        y = heightPos;
    }
    
    //normalizes height to a meter range. float input is -1f to 1f, result is ???
    public void SetHeight(float input)
    {
        height = input;
        //SetColor();
    }

    public void PrintInfo()
    {
        GD.Print("Cell (" + x + ',' + y + ") height is: "  + height + ", color is: " + color);
    }

    public void SetColor()
    {
        var h = Math.Abs(height);
        var c = 1 - h;
        if (height >= 0f)
            this.color = new Color( Mathf.Lerp(0f, 0.4f, h), 
                                    Mathf.Lerp(0.25f, 1f, h),
                                    Mathf.Lerp(0f, 0.4f, h));  //land

        else this.color = new Color(Mathf.Lerp(0f, 0.25f, c), 
                                    Mathf.Lerp(0f, 0.25f, c), 
                                    Mathf.Lerp(0.1f, 1f, c));  //water
        
    }
    
}
