using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //其他功能的单体
    public class OtherUnits
    {
        //内部变量
        public int groupNumber;
        public GH_Box functionUnit;
        public OtherUnits(int groupNumber)
        {
            this.groupNumber = groupNumber;

            if (groupNumber != StaticObject.otherFunction.functionUnits.Count - 1)
            {
                functionUnit = new GH_Box(new Box(Plane.WorldXY, new Interval(0, StaticObject.otherFunction.unitWidth), new Interval(0, StaticObject.otherFunction.unitMaxArea / StaticObject.otherFunction.unitWidth), new Interval(0, StaticObject.otherFunction.heightPerFloor)));
            }
            else
            {
                double length = (StaticObject.otherFunction.area - StaticObject.otherFunction.unitMaxArea * (StaticObject.otherFunction.functionUnits.Count - 1));
                functionUnit = new GH_Box(new Box(Plane.WorldXY, new Interval(0, StaticObject.otherFunction.unitWidth), new Interval(0, length), new Interval(0, StaticObject.otherFunction.heightPerFloor)));
            }
        }
    }
}
