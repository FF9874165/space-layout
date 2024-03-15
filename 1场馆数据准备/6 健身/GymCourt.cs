using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //健身馆运动场地
    public class GymCourt
    {
        //单个场地信息
        public int groupNumber;
        public double lengthPerCourt;//场地长度
        public double widthPerCourt;//场地宽度
        public double height = 6;//场地层高

        public GH_Box gymCourt;//运动场地实体
        
        //构造函数
        public GymCourt(int groupNumber)
        {
            widthPerCourt = StaticObject.gymnasium.courtWidth;
            lengthPerCourt = StaticObject.gymnasium.areaCourtGroupActual[groupNumber] / widthPerCourt;
            gymCourt = new GH_Box(new Box(Plane.WorldXY, new Interval(0, widthPerCourt), new Interval(0, lengthPerCourt), new Interval(0, height)));
        }
    }
}
