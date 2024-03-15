using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //辅助用房父类
    public class AuxiliaryGroup
    {
        public SportsBuilding sportsBuilding;//查询该类运动场馆管理类
        public SportsBuildingGroup sportsBuildingGroup;//查询隶属的运动场馆管理类
        public BuildingType buildingType;

        public int groupNumber;//辅助用房序号
        public double areaRequired;
        public double areaActual;
        public List<Auxiliary> auxiliary = new List<Auxiliary>();//该场馆群中的所有附属用房，每个方向、每层是一个对象

        public double boxWidth;//球场X轴长度
        public double boxLength;//球场Y轴长度
        public double widthDivideLength;//X轴除Y轴
        public double boxShort;//球场短边
        public double boxLong;//球场长边

        public double areaShort;//球场短边对应的单侧单层面积
        public double areaLong;//球场长边对应的单侧单层面积
        public double areaShortAndLong;//球场1短+1长对应单层面积
        public double area3Sides;//球场2短+1长对应单层面积
        public double area4Sides;//球场4周单层面积

        public AuxiliaryLayoutType auxiliaryLayoutType;

        //空间位置变动
        public void Move(Transform transform)
        {
            for (int i = 0; i < auxiliary.Count; i++)
            {
                auxiliary[i].auxiliaryUnit.Transform(transform);
            }
        }
    }
}
