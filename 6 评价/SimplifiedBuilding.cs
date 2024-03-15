using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Rhino.Geometry;

namespace Space_Layout
{
    //用于导出、导入JSON时的简化建筑体量模型
    [JsonObjectAttribute]
    public class SimplifiedBuilding
    {
        public BuildingType name;//场馆名称
        public Point3d pivot;//场馆中心点
        public GH_Box building;//场馆体量
        public List<GH_Box> gymAuxiliaries;//辅助用房体量
        public List<GH_Rectangle> courts;

        [JsonConstructorAttribute]
        public SimplifiedBuilding(BuildingType name, Point3d pivot, GH_Box building, List<GH_Box> gymAuxiliaries, List<GH_Rectangle> courts)
        {
            this.name = name;
            this.pivot = pivot;
            this.building = building;
            this.gymAuxiliaries = gymAuxiliaries;
            this.courts = courts;
        }
        public SimplifiedBuilding(BuildingType name, Point3d pivot, List<GH_Box> gymAuxiliaries)
        {
            this.name = name;
            this.pivot = pivot;
            this.gymAuxiliaries = gymAuxiliaries;
        }
        public SimplifiedBuilding(BuildingType name, Point3d pivot, GH_Box building)
        {
            this.name = name;
            this.pivot = pivot;
            this.building = building;
        }
        //移动场馆到指定展示位置
        public void MoveToPoint(int num,Point3d inputPivot)//num是场馆编号-1
        {
            //方案间隔的距离
            double width = Math.Round( SiteInfo.siteBoundary.Value.GetBoundingBox(false).Max.X- SiteInfo.siteBoundary.Value.GetBoundingBox(false).Min.X);
            double length = Math.Round(SiteInfo.siteBoundary.Value.GetBoundingBox(false).Max.Y - SiteInfo.siteBoundary.Value.GetBoundingBox(false).Min.Y);
            width += 100;
            length += 100;
            //方案移动变量
            Vector3d move;
            Vector3d dist = inputPivot - SiteInfo.siteBoundary.Value.GetBoundingBox(false).Center;


            //求移动变量
            if (num <StaticObject. countPerRow) //若智能排满一行
            {
                move = new Vector3d(width * num , 0, 0)+ dist;
            }
            else//排一行以上
            {
                move = new Vector3d(width * (num% StaticObject.countPerRow) , length*(num/ StaticObject.countPerRow), 0)+ dist;
            }

            //若不为空，则移动
            if (building != null)
            {
                building.Transform(Transform.Translation(move));
            }
            if (gymAuxiliaries != null)
            {
                for (int i = 0; i < gymAuxiliaries.Count; i++)
                {
                    gymAuxiliaries[i].Transform(Transform.Translation(move));
                }
            }
            if (courts != null)
            {
                for (int i = 0; i < courts.Count; i++)
                {
                    courts[i].Transform(Transform.Translation(move));
                }
            }
            
        }
    }
}
