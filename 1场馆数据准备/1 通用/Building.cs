using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Space_Layout
{
    public class Building
    {
        //外部输入的基础数据
        public BuildingType buildingType;
        public Orientation orientationBox;
        public double area;//该场馆任务书指定面积
        public Point3d showOrigin;//数据准备时，用户观察用的展示基点
        public Transform moveToOrigin;//生成对象移至展示基点的Transform

        public double height;
        public Point3d baseCenter;//底面中心店，用于计算场馆间位置关系

        public int currentLevel = -1;//布局完成后，场馆所在楼层
        public int itemIndex = -1;//布局完成后，场馆在楼层的第几个

        //获取建筑类型
        public void GetBuildingType(string name)
        {
            if (name == "篮球比赛馆")
            {
                buildingType = BuildingType.篮球比赛馆;
            }
            else if (name == "篮球训练馆")
            {
                buildingType = BuildingType.篮球训练馆;
            }
            else if (name == "游泳馆")
            {
                buildingType = BuildingType.游泳馆;
            }
            else if (name == "羽毛球馆")
            {
                buildingType = BuildingType.羽毛球馆;
            }
            else if (name == "网球馆")
            {
                buildingType = BuildingType.网球馆;
            }
            else if (name == "冰球馆")
            {
                buildingType = BuildingType.冰球馆;
            }
            else if (name == "乒乓球馆")
            {
                buildingType = BuildingType.乒乓球馆;
            }
            else if (name == "健身馆")
            {
                buildingType = BuildingType.健身馆;
            }
            else if (name == "办公")
            {
                buildingType = BuildingType.办公;
            }
            else if (name == "观演厅")
            {
                buildingType = BuildingType.观演厅;
            }
            else if (name == "大厅")
            {
                buildingType = BuildingType.大厅;
            }
            else if (name == "其他")
            {
                buildingType = BuildingType.其他;
            }
        }
    }
}
