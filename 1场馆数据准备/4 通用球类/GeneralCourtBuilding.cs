using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Space_Layout
{
    //除篮球、游泳、全民健身以外的球类场馆建筑
    public class GeneralCourtBuilding : SportsBuilding
    {
        public bool isMultifuction;
        //总管球场信息
        public BallCourt generalCourt;
        //包括此类运动馆所有单体
        public List<GeneralCourtBuildingGroup> generalCourtBuildingGroups = new List<GeneralCourtBuildingGroup>();

        //如果本项目需要设置此类球馆，则声明一个相应的球场信息管理类
        public GeneralCourtBuilding(string name, bool IsValid, int count, double area, bool hasAuxiliary, double reductionRatio, Point3d origin, bool isMultifuction)
        {
            if (IsValid)//如果要设置本馆
            {
                //外部数据初始化
                GetBuildingType(name);
                this.count = count;
                this.area = area;
                this.hasAuxiliary = hasAuxiliary;
                this.showOrigin = origin;
                this.isMultifuction = isMultifuction;

                //获取面积划分比
                if (!hasAuxiliary)
                { this.reductionRatio = 1; }
                else
                { this.reductionRatio = reductionRatio; }

                //构建运动场地特性数据、总体数据
                CreateBallCourt();
                generalCourt.SplitBallCourt(count);
            }
        }
        //创建建筑单体
        public override void CreateSportsBuildingGroup()
        {
            //创建第1组场馆
            generalCourtBuildingGroups.Add(new GeneralCourtBuildingGroup(0, this));
            generalCourtBuildingGroups[0].CreateBallCourtGroup(generalCourt);
            generalCourtBuildingGroups[0].generalCourtGroup.UpdateCourtGroupBoundary(this);
            //获取球场的底面面积
            generalCourtBuildingGroups[0].baseAreaIdeal += generalCourtBuildingGroups[0].generalCourtGroup.groupActualArea;
            //创建辅助用房
            if (hasAuxiliary) generalCourtBuildingGroups[0].CreateAuxiliaryGroup();
            //获取移动到展示位置的向量
            GetMoveToOrigin();
            //创建第2组场馆
            if (generalCourt.courtGroupNumber > 1)
            {
                generalCourtBuildingGroups.Add(new GeneralCourtBuildingGroup(1, this));
                generalCourtBuildingGroups[1].CreateBallCourtGroup(generalCourt);
                generalCourtBuildingGroups[1].generalCourtGroup.UpdateCourtGroupBoundary(this);
                //获取球场的底面面积
                generalCourtBuildingGroups[1].baseAreaIdeal += generalCourtBuildingGroups[1].generalCourtGroup.groupActualArea;
                //创建辅助用房
                if (hasAuxiliary) generalCourtBuildingGroups[1].CreateAuxiliaryGroup();
            }
            //将第二组拉开距离
            if (generalCourtBuildingGroups.Count > 1)
            {
                double detaX = generalCourtBuildingGroups[0].groupBoundingBox.Max.X - generalCourtBuildingGroups[0].groupBoundingBox.Min.X;
                double detaY = generalCourtBuildingGroups[0].groupBoundingBox.Max.Y - generalCourtBuildingGroups[0].groupBoundingBox.Min.Y;
                Point3d[] point1 = generalCourtBuildingGroups[0].groupBoundingBox.GetCorners();
                Point3d[] point2 = generalCourtBuildingGroups[1].groupBoundingBox.GetCorners();
                double detaAxisY = point1[0].Y - point2[0].Y;
                if (detaX > detaY)
                {
                    generalCourtBuildingGroups[1].moveDelegate(Transform.Translation(detaX * 2, detaAxisY, 0));
                }
                else
                {
                    generalCourtBuildingGroups[1].moveDelegate(Transform.Translation(detaY * 2, detaAxisY, 0));
                }
                generalCourtBuildingGroups[1].UpdateBoundingBox();
            }
            //所有场馆移动至展示位置
            for (int i = 0; i < generalCourt.courtGroupNumber; i++)
            {
                generalCourtBuildingGroups[i].ShowLayout(this);
            }
            //更新面积数据
            UpdateArea();
        }
        //创建运动场地
        public void CreateBallCourt()
        {
            switch (buildingType)
            {
                case BuildingType.篮球训练馆:
                    StaticObject.basketballTrainingBuilding = this;
                    generalCourt = new BallCourt(CourtType.篮球训练场);
                    break;
                case BuildingType.羽毛球馆:
                    StaticObject.badmintonBuilding = this;
                    generalCourt = new BallCourt(CourtType.羽毛球场);
                    break;
                case BuildingType.网球馆:
                    StaticObject.tennisBuilding = this;
                    generalCourt = new BallCourt(CourtType.网球场);
                    break;
                case BuildingType.冰球馆:
                    StaticObject.iceHockeyBuilding = this;
                    generalCourt = new BallCourt(CourtType.冰球场);
                    break;
                case BuildingType.乒乓球馆:
                    StaticObject.tableTennisBuilding = this;
                    generalCourt = new BallCourt(CourtType.乒乓球场);
                    break;
            }
        }
        //室内空间布局完成后，将场馆单体实体移动至指定点进行程序运行过程展示
        public void GetMoveToOrigin()
        {
            //将场馆单体移至预设点进行展示
            Point3d[] points = generalCourtBuildingGroups[0].groupBoundingBox.GetCorners();
            moveToOrigin = Transform.Translation(showOrigin - points[0]);
        }
        //场馆准备阶段，汇总指标数据
        public void UpdateArea()
        {
            for (int i = 0; i < generalCourtBuildingGroups.Count; i++)
            {
                areaTotalGroupRequired.Add(generalCourtBuildingGroups[i].areaRequired);
                areaTotalGroupActual.Add(generalCourtBuildingGroups[i].areaActual);
                areaCourtGroupRequired.Add((generalCourtBuildingGroups[i].courtAreaRequired));
                areaCourtGroupActual.Add(generalCourtBuildingGroups[i].courtAreaActual);
                areaAuxiliaryGroupRequired.Add(generalCourtBuildingGroups[i].auxiliaryAreaRequired);
                areaAuxiliaryGroupActual.Add(generalCourtBuildingGroups[i].auxiliaryAreaActual);
            }

        }
    }
}
