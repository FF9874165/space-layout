using Rhino.Geometry;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //办公单元
    public class OfficeUnits
    {
        //内部变量
        public int groupNumber;
        public GH_Box officeUnit;
        
        //构造函数
        public OfficeUnits(int groupNumber)
        {
            this.groupNumber = groupNumber;

            if (groupNumber != StaticObject.office.unitCount - 1)
            {
                officeUnit = new GH_Box(new Box(Plane.WorldXY, new Interval(0, StaticObject.office.unitWidth), new Interval(0, StaticObject.office.unitMaxArea / StaticObject.office.unitWidth), new Interval(0, StaticObject.office.heightPerFloor)));
            }
            else
            {
                double length = (StaticObject.office.area - StaticObject.office.unitMaxArea * (StaticObject.office.unitCount - 1)) / StaticObject.office.unitWidth;
                officeUnit = new GH_Box(new Box(Plane.WorldXY, new Interval(0, StaticObject.office.unitWidth), new Interval(0, length), new Interval(0, StaticObject.office.heightPerFloor)));
            }
        }
    }

}
