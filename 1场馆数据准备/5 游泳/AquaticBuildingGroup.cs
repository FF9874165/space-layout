using Grasshopper.Kernel.Types.Transforms;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System.Collections.Generic;
using System;
using Rhino.Geometry.Collections;
using static System.Net.Mime.MediaTypeNames;

namespace Space_Layout
{
    public class AquaticBuildingGroup : SportsBuildingGroup, ITrans
    {
        #region 变量
        //游泳设施的组合模式
        public enum SwimmingPoolAssemblyType
        {
            标,//标准游泳池
            非标,//非标游泳池
            儿童,//儿童戏水池
            标及非标,
            标及儿童,
            非标及儿童,
            非标及儿童及儿童,
            标及非标及儿童,
        }

        //游泳设施
        public SwimmingPool standardPool;
        public SwimmingPool nonStandardPool;
        public List<SwimmingPool> childrenPools = new List<SwimmingPool>();
        public SwimmingPoolAssemblyType type;//游泳设施的组合模式

        public GH_Box swimmingPoolMinBoundary;//游泳设施最小占用场地
        public GH_Box swimmingPoolActualBoundary;//游泳设施实际占用场地

        //附属用房
        public AquaticBuildingAuxiliaryGroup aquaticBuildingAuxiliaryGroup;//辅助功能组
        #endregion

        #region 属性
        //接口的属性
        int ITrans.CurrentLevel
        {
            get { return currentLevel; }
            set { currentLevel = value; }
        }
        int ITrans.ItemIndex
        {
            get { return itemIndex; }
            set { currentLevel = value; }
        }
        //获取朝向
        public Orientation Orientation => orientationBox;
        #endregion

        //构造函数
        public AquaticBuildingGroup(int groupNumber)
        {
            //获取外部输入数据
            this.groupNumber = groupNumber;
            buildingType = BuildingType.游泳馆;

            //创建游泳设施实体
            if (groupNumber == 0)
            {
                if (StaticObject.aquaticBuilding.standardPoolLaneCount.Count > 0)//标准游泳池
                {
                    standardPool = new SwimmingPool(CourtType.标准游泳池, StaticObject.aquaticBuilding.standardPoolLaneCount[0]);
                    standardPool.DrawSwimmingPool();
                }
                if (StaticObject.aquaticBuilding.nonStandardPoolCount > 0)//非标游泳池
                {
                    nonStandardPool = new SwimmingPool(CourtType.非标游泳池, StaticObject.aquaticBuilding.nonStandardPoolLaneCount);
                    nonStandardPool.DrawSwimmingPool();
                }
                if (StaticObject.aquaticBuilding.childrenPoolCount > 0)//儿童池
                {
                    for (int i = 0; i < StaticObject.aquaticBuilding.childrenPoolCount; i++)
                    {
                        childrenPools.Add(new SwimmingPool(CourtType.儿童戏水池, StaticObject.aquaticBuilding.nonStandardPoolLaneCount));
                        childrenPools[i].DrawSwimmingPool();
                    }
                }
            }
            else
            {
                standardPool = new SwimmingPool(CourtType.标准游泳池, StaticObject.aquaticBuilding.standardPoolLaneCount[1]);
                standardPool.DrawSwimmingPool();
            }

            //游泳设施排布组合
            GetSwimmingPoolAssemblyType();
            SwimmingPoolLayout();

        }

        //获取游泳设施组合类型
        public void GetSwimmingPoolAssemblyType()
        {
            if ((StaticObject.aquaticBuilding.standardPoolCount == 1) && (StaticObject.aquaticBuilding.nonStandardPoolCount == 0) && (StaticObject.aquaticBuilding.childrenPoolCount == 0))
            {
                type = SwimmingPoolAssemblyType.标;
            }
            else if ((StaticObject.aquaticBuilding.standardPoolCount == 2) && (groupNumber == 1))//2个标准泳池的VIP池
            {
                type = SwimmingPoolAssemblyType.标;
            }
            else if ((StaticObject.aquaticBuilding.standardPoolCount == 0) && (StaticObject.aquaticBuilding.nonStandardPoolCount != 0) && (StaticObject.aquaticBuilding.childrenPoolCount == 0))
            {
                type = SwimmingPoolAssemblyType.非标;
            }
            else if ((StaticObject.aquaticBuilding.standardPoolCount == 0) && (StaticObject.aquaticBuilding.nonStandardPoolCount == 0) && (StaticObject.aquaticBuilding.childrenPoolCount != 0))
            {
                type = SwimmingPoolAssemblyType.儿童;
            }
            else if ((StaticObject.aquaticBuilding.standardPoolCount != 0) && (StaticObject.aquaticBuilding.nonStandardPoolCount != 0) && (StaticObject.aquaticBuilding.childrenPoolCount == 0))
            {
                type = SwimmingPoolAssemblyType.标及非标;
            }
            else if ((StaticObject.aquaticBuilding.standardPoolCount != 0) && (StaticObject.aquaticBuilding.nonStandardPoolCount == 0) && (StaticObject.aquaticBuilding.childrenPoolCount != 0))
            {
                type = SwimmingPoolAssemblyType.标及儿童;
            }
            else if ((StaticObject.aquaticBuilding.standardPoolCount == 0) && (StaticObject.aquaticBuilding.nonStandardPoolCount != 0) && (StaticObject.aquaticBuilding.childrenPoolCount == 1))
            {
                type = SwimmingPoolAssemblyType.非标及儿童;
            }
            else if ((StaticObject.aquaticBuilding.standardPoolCount == 0) && (StaticObject.aquaticBuilding.nonStandardPoolCount != 0) && (StaticObject.aquaticBuilding.childrenPoolCount == 2))
            {
                type = SwimmingPoolAssemblyType.非标及儿童及儿童;
            }
            else
            {
                type = SwimmingPoolAssemblyType.标及非标及儿童;
            }
        }
        //排列游泳设施
        public void SwimmingPoolLayout()
        {
            switch (type)
            {
                #region 标
                case SwimmingPoolAssemblyType.标:
                    //构建泳池区最小边界
                    double width = standardPool.widthPerCourt + standardPool.sidelineDistance * 2;
                    double length = standardPool.lengthPerCourt + standardPool.terminalDistance * 2;
                    swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, standardPool.height)));
                    //对齐游泳设施及建泳池区边界
                    standardPool.swimmingPool.Transform(Transform.Translation(swimmingPoolMinBoundary.Value.Center - standardPool.swimmingPool.Value.Center - Vector3d.ZAxis * standardPool.height * 0.5));
                    //数据同步
                    UpdateCourtAreaRequired();
                    break;
                #endregion

                #region 非标
                case SwimmingPoolAssemblyType.非标:
                    width = nonStandardPool.widthPerCourt + nonStandardPool.sidelineDistance * 2;
                    length = nonStandardPool.lengthPerCourt + nonStandardPool.terminalDistance * 2;
                    swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, nonStandardPool.height)));
                    //对齐游泳设施及建泳池区边界
                    nonStandardPool.swimmingPool.Transform(Transform.Translation(swimmingPoolMinBoundary.Value.Center - nonStandardPool.swimmingPool.Value.Center - Vector3d.ZAxis * nonStandardPool.height * 0.5));
                    //数据同步
                    UpdateCourtAreaRequired();
                    break;
                #endregion

                #region 儿童
                case SwimmingPoolAssemblyType.儿童:
                    width = childrenPools[0].widthPerCourt + childrenPools[0].sidelineDistance * 2;
                    length = childrenPools[0].lengthPerCourt + childrenPools[0].terminalDistance * 2;
                    swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, childrenPools[0].height)));
                    //对齐游泳设施及建泳池区边界
                    foreach (var item in childrenPools)
                    {
                        item.swimmingPool.Transform(Transform.Translation(swimmingPoolMinBoundary.Value.Center - item.swimmingPool.Value.Center - Vector3d.ZAxis * item.height * 0.5));
                    }
                    //数据同步
                    UpdateCourtAreaRequired();
                    break;
                #endregion

                #region 标及非标
                case SwimmingPoolAssemblyType.标及非标:
                    double random = Tool.GetSpecificDouble(0, 10);
                    if (random > 0.5)//一行
                    {
                        width = standardPool.sidelineDistance + standardPool.widthPerCourt + standardPool.sidelineSpacing + nonStandardPool.widthPerCourt + nonStandardPool.sidelineDistance;
                        length = standardPool.lengthPerCourt + standardPool.terminalDistance * 2;
                        swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, standardPool.height)));
                        //对齐游泳设施及建泳池区边界
                        standardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance, standardPool.terminalDistance, 0)));
                        nonStandardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance + standardPool.widthPerCourt + standardPool.sidelineSpacing, standardPool.terminalDistance, 0)));
                    }
                    else//一列
                    {
                        width = standardPool.sidelineDistance * 2 + standardPool.widthPerCourt;
                        length = standardPool.terminalDistance + standardPool.lengthPerCourt + standardPool.terminalSpacing + nonStandardPool.lengthPerCourt + nonStandardPool.terminalDistance;
                        swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, standardPool.height)));
                        //对齐游泳设施及建泳池区边界
                        standardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance, standardPool.terminalDistance, 0)));
                        nonStandardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance, standardPool.terminalDistance + standardPool.lengthPerCourt + standardPool.terminalSpacing, 0)));
                    }
                    //数据同步
                    UpdateCourtAreaRequired();
                    break;
                #endregion

                #region 标及儿童
                case SwimmingPoolAssemblyType.标及儿童:
                    random = Tool.GetSpecificDouble(0, 10);
                    if (random > 0.5)//一行
                    {
                        width = standardPool.sidelineDistance + standardPool.widthPerCourt + standardPool.sidelineSpacing + childrenPools[0].widthPerCourt + childrenPools[0].sidelineDistance;
                        length = standardPool.lengthPerCourt + standardPool.terminalDistance * 2;
                        swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, standardPool.height)));
                        //对齐游泳设施及建泳池区边界
                        standardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance, standardPool.terminalDistance, 0)));
                        childrenPools[0].swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance + standardPool.widthPerCourt + standardPool.sidelineSpacing, standardPool.terminalDistance, 0)));
                    }
                    else//一列
                    {
                        width = standardPool.sidelineDistance * 2 + standardPool.widthPerCourt;
                        length = standardPool.terminalDistance + standardPool.lengthPerCourt + standardPool.terminalSpacing + childrenPools[0].lengthPerCourt + childrenPools[0].terminalDistance;
                        swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, standardPool.height)));
                        //对齐游泳设施及建泳池区边界
                        standardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance, standardPool.terminalDistance, 0)));
                        childrenPools[0].swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance, standardPool.terminalDistance + standardPool.lengthPerCourt + standardPool.terminalSpacing, 0)));
                    }
                    //数据同步
                    UpdateCourtAreaRequired();
                    break;
                #endregion

                #region 非标及儿童
                case SwimmingPoolAssemblyType.非标及儿童:
                    random = Tool.GetSpecificDouble(0, 10);
                    if (random > 0.5)//一行
                    {
                        width = nonStandardPool.sidelineDistance + nonStandardPool.widthPerCourt + nonStandardPool.sidelineSpacing + childrenPools[0].widthPerCourt + childrenPools[0].sidelineDistance;
                        length = nonStandardPool.lengthPerCourt + nonStandardPool.terminalDistance * 2;
                        swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, nonStandardPool.height)));
                        //对齐游泳设施及建泳池区边界
                        nonStandardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(nonStandardPool.sidelineDistance, nonStandardPool.terminalDistance, 0)));
                        childrenPools[0].swimmingPool.Transform(Transform.Translation(new Vector3d(nonStandardPool.sidelineDistance + nonStandardPool.widthPerCourt + nonStandardPool.sidelineSpacing, nonStandardPool.terminalDistance, 0)));

                    }
                    else//一列
                    {
                        width = nonStandardPool.sidelineDistance * 2 + nonStandardPool.widthPerCourt;
                        length = nonStandardPool.terminalDistance + nonStandardPool.lengthPerCourt + nonStandardPool.terminalSpacing + childrenPools[0].lengthPerCourt + childrenPools[0].terminalDistance;
                        swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, nonStandardPool.height)));
                        //对齐游泳设施及建泳池区边界
                        nonStandardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(nonStandardPool.sidelineDistance, nonStandardPool.terminalDistance, 0)));
                        childrenPools[0].swimmingPool.Transform(Transform.Translation(new Vector3d(nonStandardPool.sidelineDistance, nonStandardPool.terminalDistance + nonStandardPool.lengthPerCourt + nonStandardPool.terminalSpacing, 0)));
                    }
                    //数据同步
                    UpdateCourtAreaRequired();
                    break;
                #endregion

                #region 非标及儿童及儿童
                case SwimmingPoolAssemblyType.非标及儿童及儿童:
                    random = Tool.GetSpecificDouble(0, 10);
                    if (random > 0.5)//非标在南，儿童在北东西排布
                    {
                        width = childrenPools[0].sidelineDistance * 2 + childrenPools[0].widthPerCourt * 2 + childrenPools[0].sidelineSpacing;
                        length = nonStandardPool.lengthPerCourt + nonStandardPool.terminalDistance + nonStandardPool.terminalSpacing + childrenPools[0].lengthPerCourt + childrenPools[0].terminalDistance;
                        swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, nonStandardPool.height)));
                        //对齐游泳设施及建泳池区边界
                        nonStandardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(nonStandardPool.sidelineDistance, nonStandardPool.terminalDistance, 0)));
                        childrenPools[0].swimmingPool.Transform(Transform.Translation(new Vector3d(nonStandardPool.sidelineDistance, nonStandardPool.terminalDistance + nonStandardPool.lengthPerCourt + nonStandardPool.terminalSpacing, 0)));
                        childrenPools[1].swimmingPool.Transform(Transform.Translation(new Vector3d(nonStandardPool.sidelineDistance + childrenPools[0].widthPerCourt + childrenPools[0].sidelineSpacing, nonStandardPool.terminalDistance + nonStandardPool.lengthPerCourt + nonStandardPool.terminalSpacing, 0)));
                    }
                    else//非标在西，儿童在东南北排列
                    {
                        width = nonStandardPool.sidelineDistance + nonStandardPool.widthPerCourt + nonStandardPool.sidelineSpacing + childrenPools[0].widthPerCourt + childrenPools[0].sidelineDistance;
                        length = nonStandardPool.terminalDistance + childrenPools[0].lengthPerCourt * 2 + childrenPools[0].terminalSpacing + childrenPools[0].terminalDistance;
                        swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, nonStandardPool.height)));
                        //对齐游泳设施及建泳池区边界
                        nonStandardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(nonStandardPool.sidelineDistance, nonStandardPool.terminalDistance, 0)));
                        childrenPools[0].swimmingPool.Transform(Transform.Translation(new Vector3d(nonStandardPool.sidelineDistance + nonStandardPool.widthPerCourt + nonStandardPool.sidelineSpacing, nonStandardPool.terminalDistance, 0)));
                        childrenPools[1].swimmingPool.Transform(Transform.Translation(new Vector3d(nonStandardPool.sidelineDistance + nonStandardPool.widthPerCourt + nonStandardPool.sidelineSpacing, nonStandardPool.terminalDistance + childrenPools[0].lengthPerCourt + childrenPools[0].terminalSpacing, 0)));
                    }
                    //数据同步
                    UpdateCourtAreaRequired();
                    break;
                #endregion

                #region 标及非标及儿童
                case SwimmingPoolAssemblyType.标及非标及儿童:
                    random = Tool.GetSpecificDouble(0, 10);
                    if (random > 0.5)//儿童上，非标中，标下
                    {
                        width = nonStandardPool.sidelineDistance * 2 + nonStandardPool.widthPerCourt;
                        length = nonStandardPool.terminalDistance + nonStandardPool.lengthPerCourt + standardPool.terminalSpacing * 2 + standardPool.lengthPerCourt + childrenPools[0].lengthPerCourt + childrenPools[0].terminalDistance;
                        swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, standardPool.height)));
                        //对齐游泳设施及建泳池区边界
                        standardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance, standardPool.terminalDistance, 0)));
                        nonStandardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance, standardPool.terminalDistance + standardPool.lengthPerCourt + standardPool.terminalSpacing, 0)));
                        childrenPools[0].swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance, standardPool.terminalDistance + standardPool.lengthPerCourt + standardPool.terminalSpacing + nonStandardPool.lengthPerCourt + nonStandardPool.terminalSpacing, 0)));
                    }
                    else//标左，非标+儿童竖向叠落右
                    {
                        width = standardPool.sidelineDistance + standardPool.widthPerCourt + standardPool.sidelineSpacing + nonStandardPool.widthPerCourt + nonStandardPool.sidelineDistance;
                        length = standardPool.terminalDistance * 2 + standardPool.lengthPerCourt;
                        swimmingPoolMinBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(0, width), new Interval(0, length), new Interval(0, standardPool.height)));
                        //对齐游泳设施及建泳池区边界
                        standardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance, standardPool.terminalDistance, 0)));
                        nonStandardPool.swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance + standardPool.widthPerCourt + standardPool.sidelineSpacing, standardPool.terminalDistance, 0)));
                        childrenPools[0].swimmingPool.Transform(Transform.Translation(new Vector3d(standardPool.sidelineDistance + standardPool.widthPerCourt + standardPool.sidelineSpacing, standardPool.terminalDistance + nonStandardPool.lengthPerCourt + nonStandardPool.terminalSpacing, 0)));
                    }
                    //数据同步
                    UpdateCourtAreaRequired();
                    break;
                    #endregion
            }
        }
        //更新泳池区理论面积
        public void UpdateCourtAreaRequired()
        {
            courtAreaRequired = swimmingPoolMinBoundary.Value.Volume / StaticObject.aquaticBuilding.height;
            StaticObject.aquaticBuilding.areaCourtGroupRequired.Add(courtAreaRequired);
        }
        //修正游泳馆群组的边界，额外的面积来自于任务书剩余面积的按比例划分，或没有辅助用房，剩余面积均划分给泳池区
        public void UpdateCourtGroupBoundary()
        {
            if (StaticObject.aquaticBuilding.reductionRatio > 0)
            {
                //获取变动面积值
                auxiliaryAreaRequired = StaticObject.aquaticBuilding.areaAuxiliaryGroupRequired[groupNumber];
                double detaArea = auxiliaryAreaRequired * StaticObject.aquaticBuilding.reductionRatio;
                //更新泳池区、辅助用房实际面积
                courtAreaActual = detaArea + courtAreaRequired;//泳池区
                StaticObject.aquaticBuilding.areaCourtGroupActual.Add(courtAreaActual);
                StaticObject.aquaticBuilding.areaBase.Add(courtAreaActual);//累加基底面积
                auxiliaryAreaActual = auxiliaryAreaRequired - detaArea;//辅助用房
                StaticObject.aquaticBuilding.areaAuxiliaryGroupActual.Add(auxiliaryAreaActual);
                areaActual = courtAreaActual + auxiliaryAreaActual;//场馆单体面积
                StaticObject.aquaticBuilding.areaTotalGroupActual.Add(areaActual);
                //扩大泳池区边界
                ScaleCourt(courtAreaRequired, courtAreaActual);
            }
            else
            {
                //更新泳池区、辅助用房实际面积
                swimmingPoolActualBoundary = swimmingPoolMinBoundary;//场馆外轮廓
                auxiliaryAreaRequired = StaticObject.aquaticBuilding.areaAuxiliaryGroupRequired[groupNumber];//辅助用房
                auxiliaryAreaActual = auxiliaryAreaRequired;
                StaticObject.aquaticBuilding.areaAuxiliaryGroupActual.Add(auxiliaryAreaActual);
                courtAreaActual = courtAreaRequired;//泳池区
                StaticObject.aquaticBuilding.areaCourtGroupActual.Add(courtAreaActual);
                StaticObject.aquaticBuilding.areaBase.Add(courtAreaActual);//累加基底面积
                areaActual = courtAreaActual + auxiliaryAreaActual;//场馆单体面积
                StaticObject.aquaticBuilding.areaTotalGroupActual.Add(areaActual);
            }
        }
        //运动场地根据分配的辅助空间面积缩放
        public void ScaleCourt(double areaBefore, double areaAfter)
        {
            double ratio;
            double detaX;
            double detaY;
            double courtActualWidth;
            double courtActualLength;

            //缩放差值计算
            ratio = Math.Sqrt(areaAfter / areaBefore);
            courtActualWidth = ratio * swimmingPoolMinBoundary.Boundingbox.Max.X;
            courtActualLength = ratio * swimmingPoolMinBoundary.Boundingbox.Max.Y;
            detaX = (courtActualWidth - swimmingPoolMinBoundary.Boundingbox.Max.X) / 2;
            detaY = (courtActualLength - swimmingPoolMinBoundary.Boundingbox.Max.Y) / 2;

            //生成新的场地边界
            swimmingPoolActualBoundary = new GH_Box(new Box(Plane.WorldXY, new Interval(-detaX, swimmingPoolMinBoundary.Boundingbox.Max.X + detaX), new Interval(-detaY, swimmingPoolMinBoundary.Boundingbox.Max.Y + detaY), new Interval(0, StaticObject.aquaticBuilding.height)));

        }
        //创建辅助用房组
        public override void CreateAuxiliaryGroup()
        {
            aquaticBuildingAuxiliaryGroup = new AquaticBuildingAuxiliaryGroup(groupNumber);
            aquaticBuildingAuxiliaryGroup.CreateAuxiliary(8);
        }
        //创建场馆单体边界
        public void CreateGroupBoundary()
        {
            #region 更新boundingbox
            //获取球场的boundingbox
            groupBoundary = new GH_Box(swimmingPoolActualBoundary);
            //获取球场的底面面积
            double xSize = swimmingPoolActualBoundary.Boundingbox.Max.X - swimmingPoolActualBoundary.Boundingbox.Min.X;
            double ySize = swimmingPoolActualBoundary.Boundingbox.Max.Y - swimmingPoolActualBoundary.Boundingbox.Min.Y;
            baseAreaIdeal += xSize * ySize;
            //获取辅助空间的boundingbox
            BoundingBox tempBox = groupBoundary.Boundingbox;
            for (int i = 0; i < aquaticBuildingAuxiliaryGroup.auxiliary.Count; i++)
            {
                //若该辅助用房位于0标高，则计入baseAreaIdeal面积
                if (aquaticBuildingAuxiliaryGroup.auxiliary[i].auxiliaryUnit.Boundingbox.Min.Z == 0)
                {
                    baseAreaIdeal += aquaticBuildingAuxiliaryGroup.auxiliary[i].area;
                }
                //将辅助空间添加至整体BoundingBox中
                tempBox = BoundingBox.Union(tempBox, aquaticBuildingAuxiliaryGroup.auxiliary[i].auxiliaryUnit.Boundingbox);

            }
            Interval x = new Interval(tempBox.Min.X, tempBox.Max.X);
            Interval y = new Interval(tempBox.Min.Y, tempBox.Max.Y);
            groupBoundary = new GH_Box(new Box(Plane.WorldXY, x, y, new Interval(0, StaticObject.aquaticBuilding.height)));
            #endregion

            #region 获取底面中心点
            baseCenter = new Point3d(groupBoundary.Value.Center.X, groupBoundary.Value.Center.Y, 0);
            baseCenter.ToString();
            #endregion

            //更新相关边界、指标
            #region 构建底面、顶面边界
            //以运动场馆groupBoundary的底面为底边线
            Brep baseBrep = groupBoundary.Value.ToBrep();
            BrepFaceList baseBrepList = baseBrep.Faces;
            Brep baseBrep2 = baseBrepList.ExtractFace(5);
            Curve[] baseFrame = baseBrep2.GetWireframe(0);
            Curve[] baseFrame2 = Curve.JoinCurves(baseFrame);
            baseFrame2[0].MakeClosed(0);
            baseFrame2[0].Transform(Transform.Translation(-Vector3d.ZAxis * baseFrame2[0].GetBoundingBox(true).Max.Z));
            baseBoundary = baseFrame2[0];//指定底面边界
            Curve[] ceilingOffset = baseBoundary.Offset(Plane.WorldXY, StaticObject.offset, 1, CurveOffsetCornerStyle.Sharp);
            ceilingOffset[0].MakeClosed(0);//获取顶面轮廓线
            ceilingBoundary = ceilingOffset[0];
            ceilingBoundary.Transform(Transform.Translation(Vector3d.ZAxis * groupBoundary.Boundingbox.Max.Z));//移动到顶面标高
            #endregion

            #region 数据更新
            //更新顶面积、底面积
            baseArea = baseBrep2.GetArea();
            AreaMassProperties compute = AreaMassProperties.Compute(ceilingBoundary);
            ceilingArea = compute.Area;
            //添加由于辅助空间不满而确实的首层面积，获得场馆布局之前真实场馆面积
            double areaDifferent = baseArea - baseAreaIdeal;
            areaActual += areaDifferent;
            #endregion
        }
        //单一场馆统一移动左右元素
        public void Move(Transform transform)
        {
            #region 场馆
            groupBoundary.Transform(transform);
            baseBoundary.Transform(transform);
            ceilingBoundary.Transform(transform);
            baseCenter.Transform(transform);
            #endregion

            #region 泳池
            if (standardPool != null)
            {
                standardPool.swimmingPool.Transform(transform);
            }
            if (nonStandardPool != null)
            {
                nonStandardPool.swimmingPool.Transform(transform);
            }
            if (childrenPools != null)
            {
                for (int i = 0; i < childrenPools.Count; i++)
                {
                    childrenPools[i].swimmingPool.Transform(transform);
                }
            }
            swimmingPoolActualBoundary.Transform(transform);
            #endregion

            #region 辅助
            if (aquaticBuildingAuxiliaryGroup != null)
            {
                for (int i = 0; i < aquaticBuildingAuxiliaryGroup.auxiliary.Count; i++)
                {
                    aquaticBuildingAuxiliaryGroup.auxiliary[i].auxiliaryUnit.Transform(transform);
                }
            }
            #endregion
        }
        //平面布局中的移动
        public void LayoutMove()
        {

        }
        //用于布局到平面内指定点
        public void SetToPoint(Point3d point3d)
        {
            //构建移动向量
            Point3d[] fromPoint = groupBoundary.Value.GetCorners();
            Transform trans;
            trans = Transform.Translation(point3d - fromPoint[0]);
            //逐一移动实体对象
            moveDelegate(trans);
        }
        //将对象拉入边界内
        public int DragIn(GH_Curve curve)
        {
            int failTime = 0;
            int limitation = 5;
            while ((Curve.PlanarCurveCollision(baseBoundary, curve.Value, Plane.WorldXY, 0.1)) && (failTime < limitation))
            {
                Point3d[] vetices = baseBoundary.GetBoundingBox(true).GetCorners();//获取场馆边界点
                List<Vector3d> vectors = new List<Vector3d>();//存储场馆与边线交点
                Vector3d move = Vector3d.Zero;//最终选用的移动变量
                for (int i = 0; i < vetices.Length; i++)
                {
                    if (SiteInfo.siteRetreat.Value.Contains(vetices[i]) == PointContainment.Outside)
                    {
                        double t;
                        SiteInfo.siteRetreat.Value.ClosestPoint(vetices[i], out t);
                        Point3d p = SiteInfo.siteRetreat.Value.PointAt(t);
                        vectors.Add(new Vector3d(p - vetices[i]));
                    }
                }
                foreach (var v in vectors)
                {
                    if (move.Length < v.Length)
                    {
                        move = v;
                    }
                }
                moveDelegate(Transform.Translation(move));
                failTime += 1;
            }
            if (failTime > limitation) { return 2; }
            else { return 0; }
        }

        //设置朝向
        public void SetOrientation()
        {
            double xSize = groupBoundary.Boundingbox.Max.X - groupBoundary.Boundingbox.Min.X;
            double ySize = groupBoundary.Boundingbox.Max.Y - groupBoundary.Boundingbox.Min.Y;
            if (xSize < ySize)
            {
                orientationBox = Orientation.垂直;
            }
            else
            {
                orientationBox = Orientation.水平;
            }
        }
        //场馆实体整体移动
        public void Trans(Transform transform)
        {
            moveDelegate(transform);
        }
        //旋转
        public void MustRotate(double angle)
        {
            Transform transform = Transform.Rotation(angle, groupBoundary.Value.Center);
            #region 场馆
            groupBoundary.Transform(transform);
            baseBoundary.Transform(transform);
            ceilingBoundary.Transform(transform);
            baseCenter.Transform(transform);
            #endregion

            #region 泳池
            if (standardPool != null)
            {
                standardPool.swimmingPool.Transform(transform);
            }
            if (nonStandardPool != null)
            {
                nonStandardPool.swimmingPool.Transform(transform);
            }
            if (childrenPools != null)
            {
                for (int i = 0; i < childrenPools.Count; i++)
                {
                    childrenPools[i].swimmingPool.Transform(transform);
                }
            }
            swimmingPoolActualBoundary.Transform(transform);
            #endregion

            #region 辅助
            if (aquaticBuildingAuxiliaryGroup != null)
            {
                for (int i = 0; i < aquaticBuildingAuxiliaryGroup.auxiliary.Count; i++)
                {
                    aquaticBuildingAuxiliaryGroup.auxiliary[i].auxiliaryUnit.Transform(transform);
                }
            }
            #endregion
        }
        //获取底面一半X、Y尺寸,0=X,1=Y
        public double GetHalfBaseSize(int orientation, GH_Box groupB)
        {
            double detaX = groupB.Value.BoundingBox.Diagonal.X / 2;
            double detaY = groupB.Value.BoundingBox.Diagonal.Y / 2;
            if (orientation == 0) { return detaX; }
            else { return detaY; }
        }
        //获取外轮廓box
        public GH_Box GetBoundaryBox()
        {
            return groupBoundary;
        }
        //获取中心点
        public Point3d GetCenterPoint()
        {
            return baseCenter;
        }
        //创建写入JSON用的简化场馆对象
        public SimplifiedBuilding CreateSimplifiedBuilding()
        {
            //获取游泳池
            List<GH_Rectangle> courts= new List<GH_Rectangle>();
            if (standardPool!=null)
            {
                courts.Add(standardPool.swimmingPool);
            }
            if (nonStandardPool!=null)
            {
                courts.Add(nonStandardPool.swimmingPool);
            }
            if(childrenPools!=null)
            {
                for (int i = 0; i < childrenPools.Count; i++)
                {
                    courts.Add(childrenPools[i].swimmingPool);
                }
            }
            //获取辅助用房
            List<GH_Box> gymAuxiliaries = new List<GH_Box>();
            for (int i = 0; i < aquaticBuildingAuxiliaryGroup.auxiliary.Count; i++)
            {
                gymAuxiliaries.Add(aquaticBuildingAuxiliaryGroup.auxiliary[i].auxiliaryUnit);
            }
            //构造新的简化场馆
            return new SimplifiedBuilding(buildingType, groupBoundary.Value.Center, groupBoundary, gymAuxiliaries, courts);
        }
    }
}