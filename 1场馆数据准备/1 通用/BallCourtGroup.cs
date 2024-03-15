using Eto.Drawing;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //球类场地分组后各组数据统计，具象成形
    public class BallCourtGroup
    {
        #region 变量
        public GeneralCourtBuilding generalCourtBuilding;//查找所属场馆管理类

        //基础数据
        public int groupNumber;//组别编号
        public int courtNumber;//本组球场数

        //计算人数
        public int people;

        //用于球场最紧凑排布
        public double multiRowRatio;//分为多行的机率
        public int row;
        public int column;
        public double rotation;//弧度制
        public double courtTotalWidth;//X轴方向
        public double courtTotalLength;//Y轴方向
        public double floorHeight;//最小理论高度
        public double groupArea;//最小理论面积

        //球场具体形态
        public GH_Box boundaryMin;//场地组边界
        public List<GH_Rectangle> courtOutline = new List<GH_Rectangle>();//球场集合

        //球场形态调整后数值
        public double courtActualWidth;//X轴方向
        public double courtActualLength;//Y轴方向
        public double floorActualHeight;
        public double groupActualArea;
        public GH_Box boundaryActual;//场地组边界
        #endregion

        //确定场地排列数、总尺寸
        public virtual void BallCourtLayout(BallCourt ballCourt) { }
        //求得boundary、courtOutline几何形态
        public virtual void DrawCourt(BallCourt ballCourt, bool multiFunctional) { }
        //计算人数
        public virtual void CalculatePeople(SportsBuilding sportBuilding, BallCourt ballCourt) { }
        //模数化场馆高度
        public virtual void GetActualHeight()
        {
            if (!generalCourtBuilding.isMultifuction)
            {
                if (floorHeight < 6) { floorActualHeight = 6; }
                else { floorActualHeight = 12; }
            }
            else
            {
                floorActualHeight = 24;
            }
        }
        public virtual void Move(Transform transform) { }
    }
}
