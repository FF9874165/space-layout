using Grasshopper.Kernel.Geometry;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //单个辅助用房对象，即某方向、某层的单个对象
    public class Auxiliary
    {
        public double width;
        public double length;
        public double height;
        public double area;//实际值
        public double rotation = 0;

        public GH_Box auxiliaryUnit;//实体对象

        //不计实际方向，获取单层单个辅助空间单元
        public Auxiliary(double width, double length, double height)
        {
            this.width = width;
            this.length = length;
            area = width * length;
            //计算辅助空间高度
            if (length == 6) { this.height = height; }//场地层高6米时，辅助6米
            else { this.height = height / 2; }//场地层高>6米时，辅助高度减半

            auxiliaryUnit = new GH_Box(new Box(Rhino.Geometry.Plane.WorldXY, new Interval(0, this.width), new Interval(0, this.length), new Interval(0, this.height)));
        }
        //构造方法，高度使用默认数值,bool仅为区分方法名，无实际意义
        public Auxiliary(double width, double length, double height, bool heightFixed)
        {
            this.width = width;
            this.length = length;
            this.height = height;
            area = width * length;
            auxiliaryUnit = new GH_Box(new Box(Rhino.Geometry.Plane.WorldXY, new Interval(0, this.width), new Interval(0, this.length), new Interval(0, height)));
        }
    }
}
