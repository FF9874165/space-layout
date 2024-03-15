using Eto.Forms;
using Rhino.Geometry;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel.Types;
using Rhino.Render.ChangeQueue;
using Rhino.Geometry.Collections;
using Grasshopper.Kernel.Types.Transforms;

namespace Space_Layout
{
    //通用球类运动场馆单体
    public class GeneralCourtBuildingGroup : SportsBuildingGroup, ITrans
    {
        #region 变量
        public GeneralCourtBuilding generalCourtBuilding;//查找所属场馆管理类

        public GeneralCourtGroup generalCourtGroup;//运动场地组
        public GeneralCourtAuxiliaryGroup generalCourtAuxiliaryGroup;//辅助功能组
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
        #endregion
        //获取朝向
        public Orientation Orientation => orientationBox;
        
        //构造函数
        public GeneralCourtBuildingGroup(GeneralCourtAuxiliaryGroup generalCourtAuxiliaryGroup)
        {
            this.generalCourtAuxiliaryGroup = generalCourtAuxiliaryGroup;
        }

        //  构造函数
        public GeneralCourtBuildingGroup(int groupNumber, GeneralCourtBuilding generalCourtBuilding)
        {
            this.generalCourtBuilding = generalCourtBuilding;
            this.buildingType = generalCourtBuilding.buildingType;
            generalCourtGroup = new GeneralCourtGroup(this.generalCourtBuilding, groupNumber);
            this.groupNumber = groupNumber;
            //获取本组任务书要求面积
            if (groupNumber == 0)//第一组
            {
                areaRequired = generalCourtBuilding.area * generalCourtBuilding.generalCourt.splitRatio;
            }
            else//第二组
            {
                areaRequired = generalCourtBuilding.area * (1 - generalCourtBuilding.generalCourt.splitRatio);
            }
        }

        //构造各场馆单体中的场地
        public override void CreateBallCourtGroup(BallCourt ballCourt)
        {
            generalCourtGroup.BallCourtLayout(ballCourt);
            generalCourtGroup.DrawCourt(ballCourt, generalCourtBuilding.isMultifuction);
            moveDelegate += generalCourtGroup.Move;
        }
        //创建场馆内的辅助用房
        public override void CreateAuxiliaryGroup()
        {
            generalCourtAuxiliaryGroup = new GeneralCourtAuxiliaryGroup(this, buildingType);
            generalCourtAuxiliaryGroup.CreateGeneralCourtAuxiliary(this, 8);
            moveDelegate += generalCourtAuxiliaryGroup.Move;
            for (int i = 0; i < generalCourtAuxiliaryGroup.auxiliary.Count; i++)
            {
                //若该辅助用房位于0标高，则计入baseAreaIdeal面积
                if (generalCourtAuxiliaryGroup.auxiliary[i].auxiliaryUnit.Boundingbox.Min.Z == 0)
                {
                    baseAreaIdeal += generalCourtAuxiliaryGroup.auxiliary[i].area;
                }
            }
        }

        //数据输入后，面向用户的体量展示，将多组同类场馆拉开距离
        public override void ShowLayout(SportsBuilding generalCourtBuilding)
        {
            (generalCourtBuilding as GeneralCourtBuilding).generalCourtBuildingGroups[groupNumber].moveDelegate((generalCourtBuilding as GeneralCourtBuilding).moveToOrigin);
            UpdateBoundingBox();
            GetBaseArea(generalCourtBuilding.hasAuxiliary);//更新相关边界、指标
            moveDelegate += Move;//将场馆组对应的实体移动加入代理
        }

        //更新boundingbox
        public void UpdateBoundingBox()
        {
            //更新boundingbox
            #region
            //获取球场的boundingbox
            groupBoundingBox = generalCourtGroup.boundaryActual.Boundingbox;

            //获取辅助空间的boundingbox
            if (generalCourtBuilding.hasAuxiliary)
            {
                for (int i = 0; i < generalCourtAuxiliaryGroup.auxiliary.Count; i++)
                {
                    //将辅助空间添加至整体BoundingBox中
                    groupBoundingBox = BoundingBox.Union(groupBoundingBox, generalCourtAuxiliaryGroup.auxiliary[i].auxiliaryUnit.Boundingbox);
                }
            }
            //构建完整的场馆外轮廓
            groupBoundary = new GH_Box(groupBoundingBox);
            #endregion
        }
        //更新相关边界、指标
        public void GetBaseArea(bool hasAuxiliary)
        {
            #region 获取底面中心点
            baseCenter = new Point3d(groupBoundary.Value.Center.X, groupBoundary.Value.Center.Y, 0);
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
            baseArea = baseBrep2.GetArea();
            AreaMassProperties compute = AreaMassProperties.Compute(ceilingBoundary);
            ceilingArea = compute.Area;
            //添加由于辅助空间不满而确实的首层面积，获得场馆布局之前真实场馆面积
            double areaDifferent = baseArea - baseAreaIdeal;
            areaActual += areaDifferent;
            #endregion
        }
        //空间位置变动
        public void Move(Transform transform)
        {
            groupBoundary.Transform(transform);
            baseBoundary.Transform(transform);
            ceilingBoundary.Transform(transform);
            baseCenter.Transform(transform);
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
            moveDelegate(transform);
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
            //获取辅助用房
            List<GH_Box> gymAuxiliaries = new List<GH_Box>();
            for (int i = 0; i < generalCourtAuxiliaryGroup.auxiliary.Count; i++)
            {
                gymAuxiliaries.Add(generalCourtAuxiliaryGroup.auxiliary[i].auxiliaryUnit);
            }
            //构造新的简化场馆
            return new SimplifiedBuilding(buildingType, groupBoundary.Value.Center, groupBoundary, gymAuxiliaries, generalCourtGroup.courtOutline);
        }
    }
}
