using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //游泳馆建筑类
    public class AquaticBuilding : SportsBuilding
    {
        //泳池
        public int standardPoolCount = 0;
        public List<int> standardPoolLaneCount = new List<int>();
        public int nonStandardPoolCount = 0;
        public int nonStandardPoolLaneCount = 8;
        public int childrenPoolCount = 0;
        //辅助用房
        public List<AquaticBuildingGroup> aquaticBuildingGroup = new List<AquaticBuildingGroup>();
        
        //构造函数
        public AquaticBuilding(int standardPool, List<int> standardPoolLaneCount, int nonStandardPool, int nonStandardPoolLaneCount, int childrenPool, double area, double reductionRatio, Point3d showOrigin)
        {
            //注册到静态类
            StaticObject.aquaticBuilding = this;

            //输入外部数据
            #region
            this.buildingType = BuildingType.游泳馆;
            this.standardPoolCount = standardPool;
            for (int i = 0; i < standardPool; i++)
            {
                this.standardPoolLaneCount.Add(standardPoolLaneCount[i]);
            }
            this.nonStandardPoolCount = nonStandardPool;
            this.nonStandardPoolLaneCount = nonStandardPoolLaneCount;
            this.childrenPoolCount = childrenPool;
            this.area = area;
            this.reductionRatio = reductionRatio;
            this.showOrigin = showOrigin;
            height = 12;
            auxiliaryHeight = 6;
            #endregion

            //创建本工程游泳馆单体组,截止至游泳设施初步排布完毕
            if (standardPoolCount == 2)//当标准游泳池需要分设时，分馆
            {
                for (int i = 0; i < standardPoolCount; i++)
                {
                    aquaticBuildingGroup.Add(new AquaticBuildingGroup(i));
                }
            }
            else
            {
                aquaticBuildingGroup.Add(new AquaticBuildingGroup(0));
            }
            //获取分配给各建筑单体的辅助用房面积
            CalculateAuxiliaryArea();
            //创建辅助用房群组
            for (int i = 0; i < aquaticBuildingGroup.Count; i++)
            {
                aquaticBuildingGroup[i].CreateAuxiliaryGroup();
                aquaticBuildingGroup[i].CreateGroupBoundary();
                aquaticBuildingGroup[i].moveDelegate += aquaticBuildingGroup[i].Move;//代理注册
            }

            //将场馆单体移动至展示位置
            MoveToShowPoint();

        }

        //获取分配给各建筑单体的辅助用房面积
        public void CalculateAuxiliaryArea()
        {
            double auxiliaryArea = area;//总面积-泳池区面积获得辅助用房理论面积
            for (int i = 0; i < aquaticBuildingGroup.Count; i++)
            {
                auxiliaryArea -= areaCourtGroupRequired[i];
            }
            //按泳池区面积比划分辅助功能面积
            double ratio = 1;
            if (aquaticBuildingGroup.Count > 1)
            {
                ratio = areaCourtGroupRequired[0] / areaCourtGroupRequired[1];
                areaAuxiliaryGroupRequired.Add(auxiliaryArea * (ratio / (ratio + 1)));
                areaAuxiliaryGroupRequired.Add(auxiliaryArea * (1 / (ratio + 1)));
            }
            else
            {
                areaAuxiliaryGroupRequired.Add(auxiliaryArea);
            }
            //更新泳池区整体边界
            for (int i = 0; i < aquaticBuildingGroup.Count; i++)
            {
                aquaticBuildingGroup[i].UpdateCourtGroupBoundary();
                aquaticBuildingGroup[i].areaRequired = aquaticBuildingGroup[i].areaActual;
                areaTotalGroupRequired.Add(aquaticBuildingGroup[i].areaRequired);//更新场馆初始面积
            }
        }
        //将场馆单体移至预设点进行展示
        public void MoveToShowPoint()
        {
            Point3d fromPointGroup1 = aquaticBuildingGroup[0].groupBoundary.Boundingbox.Min;//获取场馆边界左下点
            Transform transToShowPoint = Transform.Translation(showOrigin - fromPointGroup1);//移动至指定点的Trans
            Move(transToShowPoint, 0);
            if (aquaticBuildingGroup.Count > 1)
            {
                double detaX = aquaticBuildingGroup[1].groupBoundary.Boundingbox.Max.X - aquaticBuildingGroup[1].groupBoundary.Boundingbox.Min.X;
                double detaY = aquaticBuildingGroup[1].groupBoundary.Boundingbox.Max.Y - aquaticBuildingGroup[1].groupBoundary.Boundingbox.Min.Y;
                Point3d fromPointGroup2 = aquaticBuildingGroup[1].groupBoundary.Boundingbox.Min;//获取场馆边界左下点
                Vector3d v = Vector3d.Unset;
                if (detaX > detaY)
                {
                    v = showOrigin - fromPointGroup2 + (new Vector3d(detaX, 0, 0));
                }
                else
                {
                    v = showOrigin - fromPointGroup2 + (new Vector3d(detaY, 0, 0));
                }
                transToShowPoint = Transform.Translation(v);//移动至指定点的Trans
                Move(transToShowPoint, 1);
            }

        }
        //移动场馆单体及内部实体
        public void Move(Transform trans, int groupNumber)
        {
            aquaticBuildingGroup[groupNumber].groupBoundary.Transform(trans);//场馆外轮廓
            aquaticBuildingGroup[groupNumber].swimmingPoolActualBoundary.Transform(trans);//泳池外轮廓
            if (standardPoolCount != 0)
            {
                aquaticBuildingGroup[groupNumber].standardPool.swimmingPool.Transform(trans);
            }
            if (nonStandardPoolCount != 0)
            {
                aquaticBuildingGroup[groupNumber].nonStandardPool.swimmingPool.Transform(trans);
            }
            if (childrenPoolCount != 0)
            {
                foreach (var childrenPool in aquaticBuildingGroup[groupNumber].childrenPools)
                {
                    childrenPool.swimmingPool.Transform(trans);
                }
            }
            foreach (var item in aquaticBuildingGroup[groupNumber].aquaticBuildingAuxiliaryGroup.auxiliary)//辅助用房
            {
                item.auxiliaryUnit.Transform(trans);
            }
            aquaticBuildingGroup[groupNumber].baseBoundary.Transform(trans);//基地边界
            aquaticBuildingGroup[groupNumber].ceilingBoundary.Transform(trans);//顶部扩展边界
            aquaticBuildingGroup[groupNumber].baseCenter.Transform(trans);
        }
    }
}
