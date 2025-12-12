using Godot;
using System;

public partial class TrewarthaClimate : Node
{
    /*
     *  Trewartha Climate Classification
     *      an extension of the Koppen climate classification
     *      
     *      Group A - Tropical:         avg monthly temp above 18C
     *      Group B - Dry:              Potential evaporation equals or exceeds precipitation
     *      Group C - Subtropical:      Atleast 8 months avg temps above 10C
     *      Group D - Temperate:        Atleast 4 months avg temps above 10C
     *      Group E - Boreal:           Warmest month avg temp above 10C
     *      Group F - Polar:            All months avg temp below 10C
     *      
     *      Ar  Tropical Wet:           all months avg above 18C and no dry season
     *      Aw  Tropical Wet-Dry:       same as Ar but atleast 2 months dry in winter
     *      
     *      BSh Tropical/subtropical semi-arid:     evap exceeds precip, all months avg temp above 0C
     *      BWh Tropical/subtropical arid:          one-half of below the precip of BSh, all months avg temp above 0C
     *      BSk Temperate semi-arid:                same as BSh but with atleast one month avg temp below 0C
     *      BWk Temperate arid:                     same as BWh but with atleast one month avg temp below 0C
     *      
     *      Cs  Subtropical dry summer (Mediterrranean):    8 months avg temp above 10C, coldest month avg temp below 18C, dry summer
     *      Cf  Subtropical humid:                          same as Cs but no dry season
     *      
     *      Do  Temperate oceanic:          4-7 months avg temp above 10C, coldest month avg above 0C
     *      Dc  Temperate continental:      same as Do, but with coldest month avg temp below 0C
     *      
     *      E   Boreal of subarctic:        Up to 3 months avg above 10C
     *      Ft  Tundra:                     All months avg below 10C
     *      Fi  Polar ice cap:              All months avg below 0C
     *      
     *      i like koppen more.
     */
}
