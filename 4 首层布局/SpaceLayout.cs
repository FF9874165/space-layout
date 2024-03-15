using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Geometry;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Space_Layout
{
    public class SpaceLayout : GH_Component
    {
        //将首层单体放入建筑退线内
        public SpaceLayout()
          : base("首层场馆初始位置", "1F初始位置",
              "根据各层待布置建筑的表单，将首层单体放入建筑退线内",
               "建筑空间布局", "布局生成")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("各层建筑表单是否完成计算", "运行状态", "本轮布局各层带布置的建筑表单是否完成计算", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("", "运行状态", "", GH_ParamAccess.item);
            pManager.AddPointParameter("", "场馆中心点", "", GH_ParamAccess.list);
            pManager.AddBoxParameter("", "场馆单体", "", GH_ParamAccess.list);
            pManager.AddRectangleParameter("", "运动场地", "", GH_ParamAccess.list);
            pManager.AddBoxParameter("", "辅助用房", "", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //生成状态，用于进程管理
            int isSuccess = 9;
            //首层场馆数
            int groundFloorCount = 0;
            //获取界角点值
            double[] boundaryValue = new double[4];
            //场馆放置点
            Point3d[] groundFloorLayoutPoints = new Point3d[10];

            //用于观察
            List<GH_Box> buildings = new List<GH_Box>();
            List<GH_Box> auxiliary = new List<GH_Box>();
            List<Curve> baseBoundary = new List<Curve>();
            List<Curve> ceilingBoundary = new List<Curve>();
            List<GH_Rectangle> courtR = new List<GH_Rectangle>();
            List<Point3d> baseCenter = new List<Point3d>();
            List<double> baseHalfX = new List<double>();
            List<double> baseHalfY = new List<double>();
            List<BuildingType> buildingTypes = new List<BuildingType>();

            if (!DA.GetData(0, ref isSuccess)) return;

            if (isSuccess == 1)//若各层面积计算成功
            {
                //重置状态
                isSuccess = 0;
                //获取首层待布置场馆列表
                groundFloorCount = GetCount(StaticObject.buildingPerFloor[0]);
                //获取边界角点值
                GetBoundaryValue(ref boundaryValue);
                //随机生成场馆放置点
                groundFloorLayoutPoints = GetLayoutPoints(groundFloorCount, boundaryValue);
                //首层场馆布置
                for (int i = 0; i < groundFloorCount; i++)
                {
                    //获取场馆类型
                    buildingTypes.Add(Tool.GetBuildingType(StaticObject.buildingPerFloor[0][i]));
                    ////场馆随机旋转
                    StaticObject.buildingPerFloor[0][i].Trans(Transform.Rotation(Tool.IFRotateHalfPie(0.5), GetCentralPoint(StaticObject.buildingPerFloor[0][i])));
                    //若场馆过长则旋转
                    isSuccess = RotateForWithinSiteBoundary(StaticObject.buildingPerFloor[0][i], boundaryValue);
                    //判断是否无解
                    if (isSuccess == 2)
                    { 
                        DA.SetData(5, isSuccess.ToString());
                        Manager.restart = true;//本轮空间布局失败
                        return;
                    }
                    else
                    {
                        //场馆放置到指定点
                        StaticObject.buildingPerFloor[0][i].SetToPoint(groundFloorLayoutPoints[i]);
                        //场馆移动至边界内
                        DragIntoBoundary(StaticObject.buildingPerFloor[0][i], SiteInfo.siteRetreat, ref isSuccess);
                        //注册朝向
                        StaticObject.buildingPerFloor[0][i].SetOrientation();
                        //判断是否无解
                        if (isSuccess == 2)
                        { 
                            DA.SetData(5, isSuccess.ToString());
                            Manager.restart = true;//本轮空间布局失败
                            return;
                        }
                        else
                        {
                            GetShape(StaticObject.buildingPerFloor[0][i], buildings, baseBoundary, ceilingBoundary, courtR, auxiliary, baseCenter, baseHalfX, baseHalfY);
                        }
                    }
                }
                //更新lobby待调整状态
                StaticObject.ifGroundLobbyFindLocation = false;
            }

            #region 信息同步
            if (isSuccess == 0) isSuccess = 1;//更新状态
            StaticObject.floorCount = groundFloorCount;
            StaticObject.boundaryValue = boundaryValue;
            StaticObject.buildings = buildings;
            StaticObject.auxiliary = auxiliary;
            StaticObject.baseBoundary = baseBoundary;
            StaticObject.ceilingBoundary = ceilingBoundary;
            StaticObject.courtR = courtR;
            StaticObject.baseCenter = baseCenter;
            StaticObject.baseHalfX = baseHalfX;
            StaticObject.baseHalfY = baseHalfY;
            StaticObject.buildingTypes = buildingTypes;
            Manager.ifStop = false;
            Manager.ifPullCalculate = false;
            Manager.pullTimes = 0;
            StaticObject.shrinkOrder = new List<int>();//首层布局收缩的顺序
            StaticObject.shrinkStatus = new List<bool>();//首层布局收缩完成情况
            Manager.ifShrinkPrepareOK = false;
            Manager.shrinkCount = 0;
            Manager.shrinkIndex = 1;
            StaticObject.pullTimeChange = 500;
            Manager.ifShrinkOk = false;
            Manager.ifBegin2F = false;
            Manager.ifFinishLayout = false;
            #endregion

            DA.SetData(0, isSuccess);
            DA.SetDataList(1, baseCenter);
            DA.SetDataList(2, buildings);
            DA.SetDataList(3, courtR);
            DA.SetDataList(4, auxiliary);
        }
        //获取用地边界值，用于随机生成点
        public void GetBoundaryValue(ref double[] boundaryValue)
        {
            boundaryValue[0] = SiteInfo.siteRetreat.Boundingbox.Min.X;
            boundaryValue[1] = SiteInfo.siteRetreat.Boundingbox.Max.X;
            boundaryValue[2] = SiteInfo.siteRetreat.Boundingbox.Min.Y;
            boundaryValue[3] = SiteInfo.siteRetreat.Boundingbox.Max.Y;
        }
        //获取某层待布置建筑个数
        public int GetCount(List<ITrans> input)
        {
            int count = 0;
            foreach (ITrans trans in input)
            {
                if (trans != null)
                {
                    count++;
                }
            }
            return count;
        }
        //获取平面边界内的随机点
        public Point3d[] GetLayoutPoints(int count, double[] boundaryValue)
        {
            Point3d[] layoutPoints = new Point3d[count];
            double detaX = boundaryValue[1] - boundaryValue[0];
            double detaY = boundaryValue[3] - boundaryValue[2];
            //每轮布置随机一个新种子的Random
            Random random = new Random(Tool.random.Next());
            for (int i = 0; i < count; i++)
            {
                Point3d test;
                PointContainment containment;
                do
                {
                    double xRatio = random.NextDouble();
                    double x = boundaryValue[0] + detaX * xRatio;
                    double yRatio = random.NextDouble();
                    double y = boundaryValue[2] + detaY * yRatio;
                    test = new Point3d(x, y, 0);
                    containment = SiteInfo.siteRetreat.Value.Contains(test, Rhino.Geometry.Plane.WorldXY, 0);
                } while (containment != PointContainment.Inside);

                layoutPoints[i] = test;
            }
            return layoutPoints;
        }
        //将场馆初始化位置
        public void InitializeBuildingPosition(List<ITrans> input, Point3d[] groundFloorLayoutPoints)
        {
            for (int i = 0; i < groundFloorLayoutPoints.Length; i++)
            {
                if (input[i] != null)
                {
                    input[i].SetToPoint(groundFloorLayoutPoints[i]);
                }
            }
        }
        //获取场馆主要形体
        public void GetShape(ITrans toBeLayout, List<GH_Box> buildings, List<Curve> baseBoundary, List<Curve> ceilingBoundary, List<GH_Rectangle> courtR, List<GH_Box> auxiliary, List<Point3d> baseCenter, List<double> baseHalfX, List<double> baseHalfY)
        {
            GH_Box groupBoundary;
            if (toBeLayout is BasketballMatchBuilding)
            {
                buildings.Add((toBeLayout as BasketballMatchBuilding).groupBoundary);
                groupBoundary = (toBeLayout as BasketballMatchBuilding).groupBoundary;
                courtR.Add((toBeLayout as BasketballMatchBuilding).courtActual);
                baseBoundary.Add((toBeLayout as BasketballMatchBuilding).baseBoundary);
                ceilingBoundary.Add((toBeLayout as BasketballMatchBuilding).ceilingBoundary);
                baseCenter.Add((toBeLayout as BasketballMatchBuilding).baseCenter);
                for (int i = 0; i < (toBeLayout as BasketballMatchBuilding).seats.Count; i++)
                {
                    courtR.Add((toBeLayout as BasketballMatchBuilding).seats[i]);
                }
                for (int i = 0; i < (toBeLayout as BasketballMatchBuilding).auxiliary.Count; i++)
                {
                    auxiliary.Add((toBeLayout as BasketballMatchBuilding).auxiliary[i].auxiliaryUnit);
                }
            }
            else if (toBeLayout is GeneralCourtBuildingGroup)
            {
                buildings.Add((toBeLayout as GeneralCourtBuildingGroup).groupBoundary);
                groupBoundary = (toBeLayout as GeneralCourtBuildingGroup).groupBoundary;
                baseBoundary.Add((toBeLayout as GeneralCourtBuildingGroup).baseBoundary);
                ceilingBoundary.Add((toBeLayout as GeneralCourtBuildingGroup).ceilingBoundary);
                baseCenter.Add((toBeLayout as GeneralCourtBuildingGroup).baseCenter);
                for (int i = 0; i < (toBeLayout as GeneralCourtBuildingGroup).generalCourtGroup.courtOutline.Count; i++)
                {
                    courtR.Add((toBeLayout as GeneralCourtBuildingGroup).generalCourtGroup.courtOutline[i]);
                }
                if ((toBeLayout as GeneralCourtBuildingGroup).generalCourtAuxiliaryGroup != null)
                {
                    for (int i = 0; i < (toBeLayout as GeneralCourtBuildingGroup).generalCourtAuxiliaryGroup.auxiliary.Count; i++)
                    {
                        auxiliary.Add((toBeLayout as GeneralCourtBuildingGroup).generalCourtAuxiliaryGroup.auxiliary[i].auxiliaryUnit);
                    }
                }
            }
            else if (toBeLayout is AquaticBuildingGroup)
            {
                buildings.Add((toBeLayout as AquaticBuildingGroup).groupBoundary);
                groupBoundary = (toBeLayout as AquaticBuildingGroup).groupBoundary;
                baseBoundary.Add((toBeLayout as AquaticBuildingGroup).baseBoundary);
                ceilingBoundary.Add((toBeLayout as AquaticBuildingGroup).ceilingBoundary);
                baseCenter.Add((toBeLayout as AquaticBuildingGroup).baseCenter);
                if ((toBeLayout as AquaticBuildingGroup).standardPool != null)
                {
                    courtR.Add((toBeLayout as AquaticBuildingGroup).standardPool.swimmingPool);
                }
                if ((toBeLayout as AquaticBuildingGroup).nonStandardPool != null)
                {
                    courtR.Add((toBeLayout as AquaticBuildingGroup).nonStandardPool.swimmingPool);
                }
                if ((toBeLayout as AquaticBuildingGroup).childrenPools != null)
                {
                    for (int i = 0; i < (toBeLayout as AquaticBuildingGroup).childrenPools.Count; i++)
                    {
                        courtR.Add((toBeLayout as AquaticBuildingGroup).childrenPools[i].swimmingPool);
                    }
                }
                if ((toBeLayout as AquaticBuildingGroup).aquaticBuildingAuxiliaryGroup != null)
                {
                    for (int i = 0; i < (toBeLayout as AquaticBuildingGroup).aquaticBuildingAuxiliaryGroup.auxiliary.Count; i++)
                    {
                        auxiliary.Add((toBeLayout as AquaticBuildingGroup).aquaticBuildingAuxiliaryGroup.auxiliary[i].auxiliaryUnit);
                    }
                }
            }
            else if (toBeLayout is GymnasiumGroup)
            {
                buildings.Add((toBeLayout as GymnasiumGroup).groupBoundary);
                groupBoundary = (toBeLayout as GymnasiumGroup).groupBoundary;
                baseBoundary.Add((toBeLayout as GymnasiumGroup).baseBoundary);
                ceilingBoundary.Add((toBeLayout as GymnasiumGroup).ceilingBoundary);
                baseCenter.Add((toBeLayout as GymnasiumGroup).baseCenter);
                if ((toBeLayout as GymnasiumGroup).gymAuxiliaryGroup != null)
                {
                    for (int i = 0; i < (toBeLayout as GymnasiumGroup).gymAuxiliaryGroup.auxiliary.Count; i++)
                    {
                        auxiliary.Add((toBeLayout as GymnasiumGroup).gymAuxiliaryGroup.auxiliary[i].auxiliaryUnit);
                    }
                }
            }
            else if (toBeLayout is Office)
            {
                buildings.Add((toBeLayout as Office).groupBoundary);
                groupBoundary = (toBeLayout as Office).groupBoundary;
                baseBoundary.Add((toBeLayout as Office).baseBoundary);
                ceilingBoundary.Add((toBeLayout as Office).ceilingBoundary);
                baseCenter.Add((toBeLayout as Office).baseCenter);
                for (int i = 0; i < (toBeLayout as Office).officeUnits.Count; i++)
                {
                    auxiliary.Add((toBeLayout as Office).officeUnits[i].officeUnit);
                }
            }
            else if (toBeLayout is Theater)
            {
                buildings.Add((toBeLayout as Theater).groupBoundary);
                groupBoundary = (toBeLayout as Theater).groupBoundary;
                baseBoundary.Add((toBeLayout as Theater).baseBoundary);
                ceilingBoundary.Add((toBeLayout as Theater).ceilingBoundary);
                auxiliary.Add((toBeLayout as Theater).hall);
                baseCenter.Add((toBeLayout as Theater).baseCenter);
                for (int i = 0; i < (toBeLayout as Theater).theaterUnits.Count; i++)
                {
                    auxiliary.Add((toBeLayout as Theater).theaterUnits[i].theaterUnit);
                }
            }
            else if (toBeLayout is LobbyUnit)
            {
                buildings.Add((toBeLayout as LobbyUnit).groupBoundary);
                groupBoundary = (toBeLayout as LobbyUnit).groupBoundary;
                baseBoundary.Add((toBeLayout as LobbyUnit).baseBoundary);
                ceilingBoundary.Add((toBeLayout as LobbyUnit).ceilingBoundary);
                baseCenter.Add((toBeLayout as LobbyUnit).baseCenter);
            }
            else
            {
                buildings.Add((toBeLayout as OtherFunction).groupBoundary);
                groupBoundary = (toBeLayout as OtherFunction).groupBoundary;
                baseBoundary.Add((toBeLayout as OtherFunction).baseBoundary);
                ceilingBoundary.Add((toBeLayout as OtherFunction).ceilingBoundary);
                baseCenter.Add((toBeLayout as OtherFunction).baseCenter);
                for (int i = 0; i < (toBeLayout as OtherFunction).functionUnits.Count; i++)
                {
                    auxiliary.Add((toBeLayout as OtherFunction).functionUnits[i].functionUnit);
                }
            }
            baseHalfX.Add(toBeLayout.GetHalfBaseSize(0, groupBoundary));
            baseHalfY.Add(toBeLayout.GetHalfBaseSize(1, groupBoundary));
        }
        //将超出退线的场馆拉回线内
        public void DragIntoBoundary(ITrans input, GH_Curve curve, ref int isSuccess)
        {
            isSuccess = input.DragIn(curve);
        }
        //获取场馆底边线中心
        public Point3d GetCentralPoint(ITrans toBeLayout)
        {
            if (toBeLayout is BasketballMatchBuilding)
            {
                return (toBeLayout as BasketballMatchBuilding).baseBoundary.GetBoundingBox(true).Center;
            }
            else if (toBeLayout is GeneralCourtBuildingGroup)
            {
                return (toBeLayout as GeneralCourtBuildingGroup).baseBoundary.GetBoundingBox(true).Center;
            }
            else if (toBeLayout is AquaticBuildingGroup)
            {
                return (toBeLayout as AquaticBuildingGroup).baseBoundary.GetBoundingBox(true).Center;
            }
            else if (toBeLayout is GymnasiumGroup)
            {
                return (toBeLayout as GymnasiumGroup).baseBoundary.GetBoundingBox(true).Center;
            }
            else if (toBeLayout is Office)
            {
                return (toBeLayout as Office).baseBoundary.GetBoundingBox(true).Center;
            }
            else if (toBeLayout is Theater)
            {
                return (toBeLayout as Theater).baseBoundary.GetBoundingBox(true).Center;
            }
            else if (toBeLayout is LobbyUnit)
            {
                return (toBeLayout as LobbyUnit).baseBoundary.GetBoundingBox(true).Center;
            }
            else
            {
                return (toBeLayout as OtherFunction).baseBoundary.GetBoundingBox(true).Center;
            }
        }
        //场馆因超出边界而旋转
        public int RotateForWithinSiteBoundary(ITrans toBeLayout, double[] boundaryValue)
        {
            double index = 0.9;
            double detaX = (boundaryValue[1] - boundaryValue[0]) * index;
            double detaY = (boundaryValue[3] - boundaryValue[2]) * index;
            if (toBeLayout is BasketballMatchBuilding)
            {
                double detaX_Item = (toBeLayout as BasketballMatchBuilding).baseBoundary.GetBoundingBox(true).Max.X - (toBeLayout as BasketballMatchBuilding).baseBoundary.GetBoundingBox(true).Min.X;
                double detaY_Item = (toBeLayout as BasketballMatchBuilding).baseBoundary.GetBoundingBox(true).Max.Y - (toBeLayout as BasketballMatchBuilding).baseBoundary.GetBoundingBox(true).Min.Y;
                if (((detaX_Item >= detaX) && (detaX_Item <= detaY)) || ((detaY_Item <= detaX) && (detaY_Item >= detaY)))//转个能放下
                {
                    (toBeLayout as BasketballMatchBuilding).moveDelegate(Transform.Rotation(Tool.AngleToRadians(90), GetCentralPoint(toBeLayout)));
                    return 0;
                }
                else if ((detaX_Item >= detaX) && (detaX_Item >= detaY) || (detaY_Item >= detaX) && (detaY_Item >= detaY))//转个也不能放下
                {
                    return 2;
                }
                else { return 0; }//无需转个
            }
            else if (toBeLayout is GeneralCourtBuildingGroup)
            {
                double detaX_Item = (toBeLayout as GeneralCourtBuildingGroup).baseBoundary.GetBoundingBox(true).Max.X - (toBeLayout as GeneralCourtBuildingGroup).baseBoundary.GetBoundingBox(true).Min.X;
                double detaY_Item = (toBeLayout as GeneralCourtBuildingGroup).baseBoundary.GetBoundingBox(true).Max.Y - (toBeLayout as GeneralCourtBuildingGroup).baseBoundary.GetBoundingBox(true).Min.Y;
                if (((detaX_Item >= detaX) && (detaX_Item <= detaY)) || ((detaY_Item <= detaX) && (detaY_Item >= detaY)))//转个能放下
                {
                    (toBeLayout as GeneralCourtBuildingGroup).moveDelegate(Transform.Rotation(Tool.AngleToRadians(90), GetCentralPoint(toBeLayout)));
                    return 0;
                }
                else if ((detaX_Item >= detaX) && (detaX_Item >= detaY) || (detaY_Item >= detaX) && (detaY_Item >= detaY))//转个也不能放下
                {
                    return 2;
                }
                else { return 0; }//无需转个
            }
            else if (toBeLayout is AquaticBuildingGroup)
            {
                double detaX_Item = (toBeLayout as AquaticBuildingGroup).baseBoundary.GetBoundingBox(true).Max.X - (toBeLayout as AquaticBuildingGroup).baseBoundary.GetBoundingBox(true).Min.X;
                double detaY_Item = (toBeLayout as AquaticBuildingGroup).baseBoundary.GetBoundingBox(true).Max.Y - (toBeLayout as AquaticBuildingGroup).baseBoundary.GetBoundingBox(true).Min.Y;
                if (((detaX_Item >= detaX) && (detaX_Item <= detaY)) || ((detaY_Item <= detaX) && (detaY_Item >= detaY)))//转个能放下
                {
                    (toBeLayout as AquaticBuildingGroup).moveDelegate(Transform.Rotation(Tool.AngleToRadians(90), GetCentralPoint(toBeLayout)));
                    return 0;
                }
                else if ((detaX_Item >= detaX) && (detaX_Item >= detaY) || (detaY_Item >= detaX) && (detaY_Item >= detaY))//转个也不能放下
                {
                    return 2;
                }
                else { return 0; }//无需转个
            }
            else if (toBeLayout is GymnasiumGroup)
            {
                double detaX_Item = (toBeLayout as GymnasiumGroup).baseBoundary.GetBoundingBox(true).Max.X - (toBeLayout as GymnasiumGroup).baseBoundary.GetBoundingBox(true).Min.X;
                double detaY_Item = (toBeLayout as GymnasiumGroup).baseBoundary.GetBoundingBox(true).Max.Y - (toBeLayout as GymnasiumGroup).baseBoundary.GetBoundingBox(true).Min.Y;
                if (((detaX_Item >= detaX) && (detaX_Item <= detaY)) || ((detaY_Item <= detaX) && (detaY_Item >= detaY)))//转个能放下
                {
                    (toBeLayout as GymnasiumGroup).moveDelegate(Transform.Rotation(Tool.AngleToRadians(90), GetCentralPoint(toBeLayout)));
                    return 0;
                }
                else if ((detaX_Item >= detaX) && (detaX_Item >= detaY) || (detaY_Item >= detaX) && (detaY_Item >= detaY))//转个也不能放下
                {
                    return 2;
                }
                else { return 0; }//无需转个
            }
            else if (toBeLayout is Office)
            {
                double detaX_Item = (toBeLayout as Office).baseBoundary.GetBoundingBox(true).Max.X - (toBeLayout as Office).baseBoundary.GetBoundingBox(true).Min.X;
                double detaY_Item = (toBeLayout as Office).baseBoundary.GetBoundingBox(true).Max.Y - (toBeLayout as Office).baseBoundary.GetBoundingBox(true).Min.Y;
                if (((detaX_Item >= detaX) && (detaX_Item <= detaY)) || ((detaY_Item <= detaX) && (detaY_Item >= detaY)))//转个能放下
                {
                    (toBeLayout as Office).Move(Transform.Rotation(Tool.AngleToRadians(90), GetCentralPoint(toBeLayout)));
                    return 0;
                }
                else if ((detaX_Item >= detaX) && (detaX_Item >= detaY) || (detaY_Item >= detaX) && (detaY_Item >= detaY))//转个也不能放下
                {
                    return 2;
                }
                else { return 0; }//无需转个
            }
            else if (toBeLayout is Theater)
            {
                double detaX_Item = (toBeLayout as Theater).baseBoundary.GetBoundingBox(true).Max.X - (toBeLayout as Theater).baseBoundary.GetBoundingBox(true).Min.X;
                double detaY_Item = (toBeLayout as Theater).baseBoundary.GetBoundingBox(true).Max.Y - (toBeLayout as Theater).baseBoundary.GetBoundingBox(true).Min.Y;
                if (((detaX_Item >= detaX) && (detaX_Item <= detaY)) || ((detaY_Item <= detaX) && (detaY_Item >= detaY)))//转个能放下
                {
                    (toBeLayout as Theater).Move(Transform.Rotation(Tool.AngleToRadians(90), GetCentralPoint(toBeLayout)));
                    return 0;
                }
                else if ((detaX_Item >= detaX) && (detaX_Item >= detaY) || (detaY_Item >= detaX) && (detaY_Item >= detaY))//转个也不能放下
                {
                    return 2;
                }
                else { return 0; }//无需转个
            }
            else if (toBeLayout is LobbyUnit)
            {
                double detaX_Item = (toBeLayout as LobbyUnit).baseBoundary.GetBoundingBox(true).Max.X - (toBeLayout as LobbyUnit).baseBoundary.GetBoundingBox(true).Min.X;
                double detaY_Item = (toBeLayout as LobbyUnit).baseBoundary.GetBoundingBox(true).Max.Y - (toBeLayout as LobbyUnit).baseBoundary.GetBoundingBox(true).Min.Y;
                if (((detaX_Item >= detaX) && (detaX_Item <= detaY)) || ((detaY_Item <= detaX) && (detaY_Item >= detaY)))//转个能放下
                {
                    (toBeLayout as LobbyUnit).Move(Transform.Rotation(Tool.AngleToRadians(90), GetCentralPoint(toBeLayout)));
                    return 0;
                }
                else if ((detaX_Item >= detaX) && (detaX_Item >= detaY) || (detaY_Item >= detaX) && (detaY_Item >= detaY))//转个也不能放下
                {
                    return 2;
                }
                else { return 0; }//无需转个
            }
            else
            {
                double detaX_Item = (toBeLayout as OtherFunction).baseBoundary.GetBoundingBox(true).Max.X - (toBeLayout as OtherFunction).baseBoundary.GetBoundingBox(true).Min.X;
                double detaY_Item = (toBeLayout as OtherFunction).baseBoundary.GetBoundingBox(true).Max.Y - (toBeLayout as OtherFunction).baseBoundary.GetBoundingBox(true).Min.Y;
                if (((detaX_Item >= detaX) && (detaX_Item <= detaY)) || ((detaY_Item <= detaX) && (detaY_Item >= detaY)))//转个能放下
                {
                    (toBeLayout as OtherFunction).Move(Transform.Rotation(Tool.AngleToRadians(90), GetCentralPoint(toBeLayout)));
                    return 0;
                }
                else if ((detaX_Item >= detaX) && (detaX_Item >= detaY) || (detaY_Item >= detaX) && (detaY_Item >= detaY))//转个也不能放下
                {
                    return 2;
                }
                else { return 0; }//无需转个
            }
        }

        protected override System.Drawing.Bitmap Icon => Properties.Resources._9_首层落位;

        public override Guid ComponentGuid
        {
            get { return new Guid("A9664155-58F0-4ECB-9A0C-679D88DDE0B3"); }
        }
    }
}