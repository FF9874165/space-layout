using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    public class TheaterUnit
    {
        //内部变量
        public GH_Box theaterUnit;

        public TheaterUnit(double widthMin, double widthMax, double lengthMin, double lengthMax, double height)
        {
            theaterUnit = new GH_Box(new Box(Plane.WorldXY, new Interval(widthMin, widthMax), new Interval(lengthMin, lengthMax), new Interval(0, height)));
        }
    }
}
