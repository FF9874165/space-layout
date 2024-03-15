using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //该类运动馆若干个单体中的某个场馆
    public class SportsBuildingGroup : BuildingGroup
    {
        public BuildingType buildingType;

        public int groupNumber;//该场馆在同类场馆中的序号
        public double areaRequired;//本馆单体面积 理论值
        public double areaActual;//本馆单体面积 实际值

        public double courtAreaRequired;
        public double courtAreaActual;
        public double auxiliaryAreaRequired;
        public double auxiliaryAreaActual;

        public int currentLevel = -1;//布局完成后，场馆所在楼层
        public int itemIndex = -1;//布局完成后，场馆在楼层的第几个

        //创建场馆内的场地
        public virtual void CreateBallCourtGroup(BallCourt ballCourt) { }
        //创建场馆内的辅助用房
        public virtual void CreateAuxiliaryGroup() { }

        public virtual void ShowLayout(SportsBuilding sportsBuilding) { }
    }
}
