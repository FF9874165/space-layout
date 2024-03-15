using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //指代不同功能运动场馆共享的大厅，并非某个场馆的独立大厅
    public class Lobby : Building
    {
        //外部输入
        public double totalArea;//大厅总面积
        public double minArea = 300;//单个大厅最小面积
        public double maxAreaRatio = 0.15;//单层大厅/建筑基底面积的最大占比
        public double maxArea;//单个大厅最大面积

        //内部数据
        public bool isOnlyOne = false;//是否仅设置1个公共大厅
        public int countMax;//大厅最大数量
        public int count = 0;//大厅数量
        public List<LobbyUnit> lobbyUnits = new List<LobbyUnit>();//大厅单体集合
        public List<double> areaBase = new List<double>();//基底面积

        //初始化大厅
        public Lobby(double area, double minArea, Point3d origin)
        {
            StaticObject.lobby = this;
            GetBuildingType("大厅");
            this.area = area;
            this.minArea = minArea;
            maxArea = SiteInfo.largestGroundFloorArea * maxAreaRatio;
            this.showOrigin = origin;

            //仅设置1个大厅的条件判定
            if (area < minArea * 2)
            {
                isOnlyOne = true;

                countMax = 1;
            }
            else
            {
                //countMax = (int)Math.Ceiling(SiteInfo.largestBuildingArea / SiteInfo.largestGroundFloorArea);
                countMax =2;
            }
            //获取大厅单元数量
            GetUnitCount();
            //创建lobbyUnit
            for (int i = 0; i < count; i++)
            {
                lobbyUnits.Add(new LobbyUnit(i));
            }
        }
        //将lobby放置于输入数据展示基点
        public void ShowLayout()
        {
            Point3d[] firstUnitCorner = lobbyUnits[0].groupBoundary.Boundingbox.GetCorners();
            moveToOrigin = Transform.Translation(showOrigin - firstUnitCorner[0]);
            for (int i = 0; i < count; i++)
            {
                if (i != 0)
                {
                    double detaX = lobbyUnits[i - 1].groupBoundary.Boundingbox.Max.X - lobbyUnits[i - 1].groupBoundary.Boundingbox.Min.X;
                    double detaY = lobbyUnits[i - 1].groupBoundary.Boundingbox.Max.Y - lobbyUnits[i - 1].groupBoundary.Boundingbox.Min.Y;
                    if (detaX > detaY)
                    {
                        lobbyUnits[i].Move(Transform.Translation(Vector3d.XAxis * detaX * (i + 1)));
                    }
                    else
                    {
                        lobbyUnits[i].Move(Transform.Translation(Vector3d.XAxis * detaY * (i + 1)));
                    }
                }
            }
            for (int i = 0; i < count; i++)
            {
                lobbyUnits[i].Move(moveToOrigin);
            }
        }
        //获取大厅单元数量
        public void GetUnitCount()
        {
            count = countMax;
            while (area / countMax > maxArea)
            {
                count += 1;
            }
            while (area / countMax < minArea)
            {
                count -= 1;
            }
        }
    }
}
