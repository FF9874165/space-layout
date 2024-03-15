using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //用于面积比大小
    public class DoubleCompare : IComparer<int>
    {
        List<double> area = new List<double>();
        public DoubleCompare(List<double> area)
        {
            this.area = area;
        }
        //根据面积大小排序号
        public int Compare(int x, int y)
        {
            double areaX = area[x];
            double areaY = area[y];
            return -(areaX.CompareTo(areaY));
        }
    }
}
