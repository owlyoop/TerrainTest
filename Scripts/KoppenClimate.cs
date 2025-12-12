using Godot;
using System;

public partial class KoppenClimate : Node
{
    /*
     * ..::==== GROUP A - Tropical Climates ====::..
     *      avg yearly temp of 18C(64.4F) or higher
     *      
     * 
     * .....Af...Tropical Rainforest Climate
     *              avg monthly precip of 60mm(2.4in)
     *          
     * .....Am...Tropical Monsoon Climate
     *              driest month precip less than 60mm(2.4in)
     *              but atleast 100 - (yearly precip / 25)
     *              
     * .....Aw/As...Tropical Wet&Dry / Savannah Climate
     *              driest month precip less than 60mm(2.4in)
     *              less than 100 - (yearly precip / 25)
     *              Aw - dry winter. As - dry summer
     * 
     * 
     * 
     * ..::==== GROUP B - Desert and semi-arid climates ====::..
     *      avg temp greater than 10C(50F)
     *      precip threshold (mm) = (avg annual temp (C) * 20) then adding:
     *          280 if 70% or more of total precip is in spring&summer (April-Sept in northern hemi, Oct-March in southern hemi)
     *          140 is 30%-70% of total precip is received during spring&summer
     *          0 if less than 30% of total precip is received during spring&summer
     *      if annual precip < 50% of threshold, classification is BW(arid:desert climate)
     *      if annual precip is 50%-100% of threshold, classification is BS(semi-arid:steppe climate)
     *      
     *      third letter is temp. 
     *          h = low-latitude climates = avg annual temp > 18C(64.4F)
     *          k = mid-latitude climate = avg annaul temp < 18C(64.4F)
     *          n = frequent fog. H = high altitudes
     *          
     *          
     * 
     * ..::==== GROUP C - Temperate Climates ====::..
     *      coldest month avg temp between -3C or 0C?(32F) and 18C (64.4F), and atleast one month avg temp above 10C (50F)
     *      Cs (dry summer) vs Cw(dry winter)
     *          Cw = more precip in summer months than winter months
     *          Cs = more precip in winter months
     *          
     * .....Cfa...Humid Subtropical Climate
     *          coldest month avg above 0C(32F), 
     *          atleast one month's avg temp above 22C(71.6F),
     *          and atleast four months avg above 10C(50F)
     *          (suggestion)
     *              cool vs mild winters. avg day/night temp < 10C = cool, > 10C = mild.
     * 
     * .....CFb...Temperate Oceanic Climate or Subtropical Highland Climate
     *          coldest month avg above 0C(32F),
     *          all months avg temps below 22C(71.6F),
     *          and atleast four months averaging above 10C(50F)
     * 
     * .....Cfc...Subpolar Oceanic Climate
     *          coldest month avg above 0C(32F)
     *          and 1-3 months avg above 10C(50F)
     *          
     * .....Cwa...Monsoon-influenced Humid Subtropical Climate
     *          coldest month avg above 0C
     *          atleast one months avg temp above 22C
     *          atleast four months avg above 10C
     *          atleast 10x as much rain in wettest month of summer vs driest month of winter
     * 
     * .....Cwb...Subtropical HighlandClimate or Monsoon-influenced Temperate Oceanic Climate
     *          coldest month avg above 0C
     *          all months avg temp below 22C
     *          atleast four months avg above 10C
     *          atleast 10x as much rain in wettest month of summer vs driest month of winter
     * 
     * .....Cwc...Cold Subtropical Highland Climate or Monsoon-influenced Subpolar Oceanic Climate
     *          coldest month avg above 0C
     *          1-3 months avg above 10C
     *          atleast 10x as much rain in wettest month of summer vs driest month of winter
     *          
     * .....Csa...Hot-summer Mediterranean Climate
     *          coldest month avg above 0C
     *          atleast one months avg temp above 22C
     *          four months avg above 10C
     *          atleast 3x precip in wettest month of winter vs driest month of summer
     *          driest month of summer recieves less than 40mm
     * 
     * .....Csb...Warm-summer Mediterranean Climate
     *          coldest month avg above 0C
     *          all months avg temp below 22C
     *          atleast four months avg above 10C
     *          atleast 3x precip in wettest month of winter vs driest month of summer
     *          driest month of summer recieves less than 40mm
     *          
     * .....Csc...Cold-summer Mediterranean Climate
     *          coldest month avg above 0C
     *          1-3 months avg above 10C
     *          atleast 3x precip in wettest month of winter vs driest month of summer
     *          driest month of summer recieves less than 40mm
     * 
     * 
     * 
     * ..::==== GROUP D - Continental Climates ====::..
     *      atleast one month avg below 0C, one month avg above 10C
     * 
     * .....Dfa...Hot-summer Humid Continental Climate
     *          coldest month avg below 0C
     *          atleast one month avg above 22C
     *          atleast four months avg above 10C
     * 
     * .....Dfb...Warm-summer Humid Continental Climate
     *          coldest month avg below 0C
     *          all months avg below 22C
     *          atleast four months avg above 10C
     *  
     * .....Dfc...Subarctic Climate
     *          coldest month avg below 0C
     *          1-3 months avg above 10C
     *          
     * .....Dfd...Extremely Cold Subarctic Climate
     *          coldest month avg below -38C
     *          1-3 months avg above 10C
     * 
     * .....Dwa...Monsoon-influenced Hot-summer Humid Continental Climate
     *          coldest month avg below 0C
     *          atleast one months avg above 22C
     *          atleast four months avg above 10C
     *          atleast 10x rain in wettest summer month vs driest winter month
     *          
     * .....Dwb...Monsoon-influenced Warm-summer Humid Continental Climate
     *          coldest month avg below 0C
     *          all months with avg below 22C
     *          atleast four months avg above 10C
     *          atleast 10x rain in wettest summer month vs driest winter month
     * 
     * .....Dwc...Monsoon-influenced Subarctic Climate
     *          coldest month avg below 0C
     *          1-3 months avg above 10C
     *          atleast 10x rain in wettest summer month vs driest winter month
     *        
     * .....Dwd...Monsoon-influenced Extremely Cold Subarctic Climate
     *          coldest month avg below -38C
     *          1-3 months avg above 10C
     *          atleast 10x rain in wettest summer month vs driest winter month
     *          
     * .....Dsa...Mediterranean-influenced Hot-summer Humid Continental Climate
     *          coldest month avg below 0C
     *          avg temp of warmest month above 22C
     *          atleast four months avg above 10C
     *          atleast 3x precip in wettest winter month vs driest summer month
     *          driest summer month receives less than 30mm
     *          
     * .....Dsb...Mediterranean-influenced Warm-summer Humid Continental Climate
     *          coldest month avg below 0C
     *          avg temp of warmest month below 22C
     *          four months avg above 10C
     *          atleast 3x precip in wettest winter month vs driest summer month
     *          driest summer month receives less than 30mm
     *          
     * .....Dsc...Mediterranean-influenced subarctic climate
     *          coldest month avg below 0C
     *          1-3 months avg above 10C
     *          atleast 3x precip in wettest winter month vs driest summer month
     *          driest summer month receives less than 30mm
     *          
     * .....Dsd...Mediterranean-influenced Extremely Cold Subarctic Climate
     *          coldest month avg below -38C
     *          1-3 months avg above 10C
     *          atleast 3x precip in wettest winter month vs driest summer month
     *          driest summer month receives less than 30mm
     *          
     *          
     *          
     * ..::==== GROUP E - Polar and Alpine Climates ====::..
     *      every month avg temp below 10C
     *      
     * .....ET...Tundra Climate
     *          avg temp of warmest month between 0C and 10C
     *          
     * .....EF...Ice Cap Climate
     *          eternal winter, all 12 months avg below 0C
     * 
     */

    void ClassifyClimate(float[] temps)
    {

    }
}
